using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using NavStack.Core;
using NavStack.Services;
using NavStack.Regions;
using NavStack.Modularity;
using Astra.Services.Session;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Astra.Services.Navigation;
using Astra.Utilities;
using Astra.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Astra.Services.Monitoring;
using Astra.Core.Access.Models;

namespace Astra.ViewModels
{
    /// <summary>
    /// 重构后的 MainViewModel - 职责：导航管理 + 权限控制
    /// 
    /// ✅ 职责单一：仅负责导航相关功能
    /// ✅ 用户管理功能已移至 UserMenuViewModel
    /// ✅ 会话管理已移至 UserSessionService
    /// ⭐ 新增：集成导航权限管理
    /// </summary>
    public partial class MainViewModel : ObservableObject
    {
		private readonly INavigationManager _navigationManager;
		private readonly IRegionManager _regionManager;
        private readonly NavigationMenuAggregator _menuAggregator;
        private readonly NavigationModuleManager _moduleManager;
        private readonly IUserSessionService _sessionService;
        private readonly INavigationPermissionService _permissionService;
		private readonly INavigationGuard _navigationGuard;
        private readonly IMessenger _messenger;
		private readonly ILogger<MainViewModel> _logger;
		private readonly string _defaultPageKey;
		private readonly ITelemetryService _telemetry;

        #region 可观察属性 - 导航相关

        [ObservableProperty]
        private string _pageTitle = "首页";

        [ObservableProperty]
        private string _pageSubtitle = "欢迎使用自动化测试平台";

        [ObservableProperty]
        private bool _canGoBack;

        [ObservableProperty]
        private bool _canGoForward;

        [ObservableProperty]
        private string _currentPageKey = "Home";

        [ObservableProperty]
        private int _loadedModuleCount;

        [ObservableProperty]
        private int _menuItemCount;

        [ObservableProperty]
        private NavigationMenuItem _selectedMenuItem;

        #endregion

        #region 可观察属性 - 用户与会话（供头像与菜单绑定）

        [ObservableProperty]
        private string _currentUserName;

        [ObservableProperty]
        private UserRole _currentUserRole;

        [ObservableProperty]
        private bool _isLoggedIn;

        [ObservableProperty]
        private bool _isUserMenuOpen;

        #endregion

        public ObservableCollection<NavigationMenuItem> MenuItems { get; }
		private NavigationMenuItem _lastSelectedItem;

		public MainViewModel(
			INavigationManager navigationManager,
			IRegionManager regionManager,
            NavigationMenuAggregator menuAggregator,
            NavigationModuleManager moduleManager,
            IUserSessionService sessionService,
            INavigationPermissionService permissionService,
            IMessenger messenger = null,
			ILogger<MainViewModel> logger = null,
			INavigationGuard navigationGuard = null,
			IOptions<AppNavOptions> navOptions = null,
			ITelemetryService telemetry = null)
        {
			_navigationManager = navigationManager ?? throw new ArgumentNullException(nameof(navigationManager));
			_regionManager = regionManager ?? throw new ArgumentNullException(nameof(regionManager));
            _menuAggregator = menuAggregator ?? throw new ArgumentNullException(nameof(menuAggregator));
            _moduleManager = moduleManager ?? throw new ArgumentNullException(nameof(moduleManager));
            _sessionService = sessionService ?? throw new ArgumentNullException(nameof(sessionService));
            _permissionService = permissionService ?? throw new ArgumentNullException(nameof(permissionService));
            _messenger = messenger ?? WeakReferenceMessenger.Default;
			_logger = logger ?? NullLogger<MainViewModel>.Instance;
			_navigationGuard = navigationGuard ?? new NavigationGuard(_permissionService);
			_defaultPageKey = string.IsNullOrWhiteSpace(navOptions?.Value?.DefaultPage) ? NavigationKeys.Home : navOptions.Value.DefaultPage;
			_telemetry = telemetry;

            MenuItems = new ObservableCollection<NavigationMenuItem>();

			// 初始时尝试订阅；若区域尚未注册，可在 App 启动后调用 EnsureRegionSubscriptions 再订阅
			EnsureRegionSubscriptions();

            // 监听用户会话变化
            _messenger.Register<UserSessionChangedMessage>(this, OnUserSessionChanged);

            // 初始化头像与会话展示
            RefreshUserHeaderState();

            LoadMenuItems();
            UpdateStatistics();

			// 如果此时尚未选中任何菜单（例如区域尚未触发导航事件），强制将首页设为选中
			EnsureDefaultHomeSelection();
        }

        /// <summary>
        /// 加载菜单项
        /// </summary>
        private void LoadMenuItems()
        {
            try
            {
                var allMenuItems = _telemetry == null
					? _menuAggregator.GetAllMenuItems().ToList()
					: _telemetry.TrackDurationAsync("MenuAggregation", async () =>
					{
						// 包装同步为异步调用
						return await Task.Run(() => _menuAggregator.GetAllMenuItems().ToList());
					}).GetAwaiter().GetResult();

				// 简单变更检测，避免无差别重建导致的 UI 抖动
				bool needReload = MenuItems.Count != allMenuItems.Count
					|| !MenuItems.Zip(allMenuItems, (a, b) => ReferenceEquals(a, b) || a?.NavigationKey == b?.NavigationKey).All(x => x);

				if (needReload)
				{
					MenuItems.Clear();
					foreach (var item in allMenuItems)
					{
						MenuItems.Add(item);
					}
				}

                // 根据当前用户权限更新菜单可见性
                UpdateMenuVisibility();

                MenuItemCount = MenuItems.Count;
                _logger.LogInformation("[MainViewModel] 加载了 {MenuItemCount} 个菜单项", MenuItemCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[MainViewModel] 加载菜单失败");
            }
        }

        /// <summary>
        /// 更新统计信息
        /// </summary>
        private void UpdateStatistics()
        {
            try
            {
                var modules = _moduleManager.GetModules().ToList();
                LoadedModuleCount = modules.Count;
                _logger.LogInformation("[MainViewModel] 加载了 {LoadedModuleCount} 个模块", LoadedModuleCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[MainViewModel] 更新统计失败");
            }
        }

        /// <summary>
        /// 刷新菜单
        /// </summary>
        [RelayCommand]
        public void RefreshMenu()
        {
            LoadMenuItems();
            UpdateStatistics();
            _logger.LogInformation("[MainViewModel] 菜单已刷新");
        }

        /// <summary>
        /// 更新菜单可见性（根据当前用户权限）
        /// </summary>
        private void UpdateMenuVisibility()
        {
            var currentUser = _sessionService.CurrentUser;
            if (_telemetry == null)
			{
				_navigationGuard.ApplyVisibility(currentUser, MenuItems);
			}
			else
			{
				_telemetry.TrackDurationAsync("MenuVisibility.Apply", async () =>
				{
					await Task.Run(() => _navigationGuard.ApplyVisibility(currentUser, MenuItems));
				}).GetAwaiter().GetResult();
			}
            
            // 同步更新用户区展示（角色/名称/登录态）
            RefreshUserHeaderState();
            // 权限变化后，若当前无选中项，确保首页选中
            EnsureDefaultHomeSelection();

            _logger.LogInformation(
                "[MainViewModel] 菜单可见性已更新, 用户: {Username}, 可见菜单数: {Visible}/{Total}",
				currentUser?.Username ?? "未登录",
				MenuItems.Count(m => m.IsVisible),
				MenuItems.Count);
        }

        /// <summary>
        /// 检查当前页面权限，如果权限不足则自动导航到首页
        /// </summary>
        private async Task CheckCurrentPagePermissionAsync()
        {
            try
            {
                // 如果当前页面是首页，无需检查
                if (string.IsNullOrEmpty(CurrentPageKey) || CurrentPageKey == _defaultPageKey)
                {
                    return;
                }

                // 查找当前页面对应的菜单项
                var currentMenuItem = MenuItems.FirstOrDefault(m => m.NavigationKey == CurrentPageKey);
                if (currentMenuItem == null)
                {
                    _logger.LogWarning("[MainViewModel] 未找到当前页面对应的菜单项: {CurrentPageKey}", CurrentPageKey);
                    return;
                }

                // 检查当前用户是否有权限访问当前页面
                var currentUser = _sessionService.CurrentUser;
                bool hasPermission = _navigationGuard.CanNavigate(currentUser, currentMenuItem);

                if (!hasPermission)
                {
                    _logger.LogWarning(
                        "[MainViewModel] 当前页面权限不足，自动导航到首页. 当前页面: {CurrentPageKey}, 用户: {Username}",
						CurrentPageKey, currentUser?.Username ?? "未登录");

                    // 自动导航到安全回退页
					var fallback = _navigationGuard.GetSafeFallbackPage(currentUser, MenuItems, _defaultPageKey);
                    await NavigateAsync(fallback);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[MainViewModel] 检查当前页面权限失败");
            }
        }

        /// <summary>
        /// 初始化导航 - 在Frame设置后调用
        /// </summary>
        public async Task InitializeNavigationAsync()
        {
            try
            {
				// 确保区域服务事件已订阅（若 App 注册在本方法之后）
				var regionService = _regionManager.GetNavigationService(RegionNames.MainRegion);

				if (regionService != null)
				{
					regionService.Navigated -= OnNavigated;
					regionService.NavigationFailed -= OnNavigationFailed;
					regionService.Navigated += OnNavigated;
					regionService.NavigationFailed += OnNavigationFailed;
				}

                await NavigateAsync(NavigationKeys.Home);
                _logger.LogInformation("[MainViewModel] 导航初始化完成");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[MainViewModel] 导航初始化失败");
            }
        }

        #region 导航命令

        /// <summary>
        /// 导航到指定页面
        /// </summary>
        [RelayCommand]
        private async Task NavigateAsync(object parameter)
        {
            string pageKey = null;
            NavigationMenuItem menuItem = null;

            if (parameter is string key)
            {
                pageKey = key;
                menuItem = MenuItems.FirstOrDefault(m => m.NavigationKey == pageKey);
            }
            else if (parameter is NavigationMenuItem item)
            {
                menuItem = item;
                pageKey = item.NavigationKey;
            }

            if (string.IsNullOrEmpty(pageKey))
                return;

            try
            {
                // ⭐ 在导航前验证权限
                if (menuItem == null)
                {
                    // 菜单中未找到时，尝试从聚合器里查找一次（保证非菜单入口也能校验权限）
                    try
                    {
                        var fromAgg = _menuAggregator.GetAllMenuItems().FirstOrDefault(m => m.NavigationKey == pageKey);
                        if (fromAgg != null)
                        {
                            menuItem = fromAgg;
                        }
                    }
                    catch { }
                }

                if (menuItem != null)
                {
                    var currentUser = _sessionService.CurrentUser;

                    if (!_navigationGuard.CanNavigate(currentUser, menuItem))
                    {
                        _logger.LogWarning("[MainViewModel] 权限不足，无法导航到: {PageKey}", pageKey);
                        return;
                    }
                }

                _logger.LogInformation("[MainViewModel] 导航到: {PageKey}", pageKey);
				await _navigationManager.NavigateAsync(pageKey);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[MainViewModel] 导航失败");
            }
        }

        /// <summary>
        /// 打开权限配置/权限相关页面
        /// </summary>
        [RelayCommand]
        public async Task OpenPermissionAsync()
        {
            await NavigateAsync(NavigationKeys.Permission);
        }

        /// <summary>
        /// 后退
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanGoBack))]
        private async Task GoBackAsync()
        {
            try
            {
				_navigationManager.GoBack();
                _logger.LogInformation("[MainViewModel] 后退成功");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[MainViewModel] 后退失败");
            }
        }

        /// <summary>
        /// 前进
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanGoForward))]
        private async Task GoForwardAsync()
        {
            try
            {
				_navigationManager.GoForward();
                _logger.LogInformation("[MainViewModel] 前进成功");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[MainViewModel] 前进失败");
            }
        }

        #endregion

        #region 用户会话事件处理

        /// <summary>
        /// 处理用户会话变化消息
        /// </summary>
        private async void OnUserSessionChanged(object recipient, UserSessionChangedMessage message)
        {
            // 当用户登录/登出时，更新菜单可见性
            UpdateMenuVisibility();
            
            // ⭐ 检查当前页面权限，如果权限不足则自动导航到首页
            await CheckCurrentPagePermissionAsync();
            
            // 刷新头像与用户信息绑定
            RefreshUserHeaderState();
            // 会话切换后，优先将首页设为选中以匹配默认导航
            SetSelectedByKey(_defaultPageKey);

            _logger.LogInformation("[MainViewModel] 用户会话变化: {Username}, 原因: {Reason}",
				message.CurrentUser?.Username ?? "未登录", message.Reason);
        }

        #endregion

        /// <summary>
        /// 刷新头像区的用户状态（名称、角色、登录态）
        /// </summary>
        private void RefreshUserHeaderState()
        {
            var user = _sessionService.CurrentUser;
            IsLoggedIn = user != null;
            CurrentUserName = user?.Username ?? "未登录";
            CurrentUserRole = user?.Role ?? UserRole.Operator;
        }

        /// <summary>
        /// 显示登录对话（未登录时的按钮）
        /// </summary>
        [RelayCommand]
        private void ShowLogin()
        {
            // 预留：在外层通过命令绑定的交互触发登录窗口
            _logger.LogInformation("[MainViewModel] 触发登录请求");
        }

        #region 事件处理

        /// <summary>
        /// 导航完成事件处理
        /// </summary>
        private void OnNavigated(object sender, NavigationEventArgs e)
        {
            try
            {
                CurrentPageKey = e.Context.NavigationUri;
				CanGoBack = _navigationManager.CanGoBack;
				CanGoForward = _navigationManager.CanGoForward;

                GoBackCommand.NotifyCanExecuteChanged();
                GoForwardCommand.NotifyCanExecuteChanged();

                UpdatePageTitle(CurrentPageKey);
                UpdateSelectedMenuItem(CurrentPageKey);

                _logger.LogInformation("[MainViewModel] 导航完成: {CurrentPageKey}", CurrentPageKey);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[MainViewModel] 处理导航事件失败");
            }
        }

        /// <summary>
        /// 导航失败事件处理
        /// </summary>
        private void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
        {
            _logger.LogError(e.Exception, "[MainViewModel] 导航失败");
        }

        /// <summary>
        /// 更新页面标题
        /// </summary>
        private void UpdatePageTitle(string pageKey)
        {
            var menuItem = MenuItems.FirstOrDefault(m => m.NavigationKey == pageKey);
            PageTitle = menuItem?.Title ?? pageKey;
            PageSubtitle = menuItem?.Description ?? "系统功能页面";
        }

        /// <summary>
        /// 更新选中的菜单项
        /// </summary>
        private void UpdateSelectedMenuItem(string pageKey)
        {
            try
            {
                // 设置当前选中（只对差异项变更，减少 O(n) 遍历）
                var menuItem = MenuItems.FirstOrDefault(m => m.NavigationKey == pageKey);

                if (menuItem != null)
                {
					if (_lastSelectedItem != null && !ReferenceEquals(_lastSelectedItem, menuItem))
					{
						_lastSelectedItem.IsSelected = false;
					}

					menuItem.IsSelected = true;
                    SelectedMenuItem = menuItem;
					_lastSelectedItem = menuItem;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[MainViewModel] 更新选中菜单失败");
            }
        }

        #endregion

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
			var regionService = _regionManager.GetNavigationService(RegionNames.MainRegion);
			if (regionService != null)
			{
				regionService.Navigated -= OnNavigated;
				regionService.NavigationFailed -= OnNavigationFailed;
			}

            // 取消消息监听
            _messenger?.Unregister<UserSessionChangedMessage>(this);
        }

		/// <summary>
		/// 外部触发：按页面键设置当前选中（用于应用启动后的默认导航选中态）
		/// </summary>
		/// <param name="pageKey">页面键，如 Home/Config/Debug/Sequence</param>
		public void SetSelectedByKey(string pageKey)
		{
			if (string.IsNullOrWhiteSpace(pageKey)) return;

			CurrentPageKey = pageKey;

			// 查询导航管理器当前状态以更新前进/后退可用性
			try
			{
				CanGoBack = _navigationManager.CanGoBack;
				CanGoForward = _navigationManager.CanGoForward;
				GoBackCommand.NotifyCanExecuteChanged();
				GoForwardCommand.NotifyCanExecuteChanged();
			}
			catch { }

			UpdatePageTitle(pageKey);
			UpdateSelectedMenuItem(pageKey);
		}

		
		/// <summary>
		/// 确保已订阅 MainRegion 导航事件（在区域注册完成后调用）
		/// </summary>
		public void EnsureRegionSubscriptions()
		{
			if (_regionSubscribed) return;
			var regionService = _regionManager.GetNavigationService(RegionNames.MainRegion);
			if (regionService != null)
			{
				regionService.Navigated += OnNavigated;
				regionService.NavigationFailed += OnNavigationFailed;
				_regionSubscribed = true;
			}
		}

		/// <summary>
		/// 若尚无选中项，强制将首页设为选中（用于启动阶段兜底）
		/// </summary>
		private void EnsureDefaultHomeSelection()
		{
			if (MenuItems == null || MenuItems.Count == 0) return;
			if (MenuItems.Any(m => m.IsSelected)) return;

			var home = MenuItems.FirstOrDefault(m => string.Equals(m.NavigationKey, _defaultPageKey, StringComparison.OrdinalIgnoreCase));
			if (home != null)
			{
				home.IsSelected = true;
				SelectedMenuItem = home;
				CurrentPageKey = home.NavigationKey;
				_lastSelectedItem = home;
			}
		}

        private bool _regionSubscribed;

    }
}
