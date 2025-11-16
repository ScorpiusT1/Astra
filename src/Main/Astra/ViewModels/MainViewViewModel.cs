using Astra.Core.Access;
using Astra.Core.Access.Models;
using Astra.Services.Session;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using NavStack.Modularity;
using System;

namespace Astra.ViewModels
{
    /// <summary>
    /// MainView 的复合 ViewModel
    /// 
    /// ✅ 组合模式：组合多个职责分离的 ViewModel
    /// ✅ 导航功能：MainViewModel
    /// ✅ 用户菜单功能：UserMenuViewModel
    /// ✅ 会话状态：通过 UserSessionService 共享
    /// </summary>
    public partial class MainViewViewModel : ObservableObject
    {
        /// <summary>
        /// 导航相关功能（页面导航、菜单管理）
        /// </summary>
        public MainViewModel Navigation { get; }

        /// <summary>
        /// 用户菜单相关功能（登录、切换用户、修改密码等）
        /// </summary>
        public UserMenuViewModel UserMenu { get; }

        // ⭐ 代理导航相关属性和命令（使XAML绑定更简洁）
        /// <summary>
        /// 菜单项集合（代理到 Navigation.MenuItems）
        /// </summary>
        public System.Collections.ObjectModel.ObservableCollection<NavigationMenuItem> MenuItems => Navigation.MenuItems;

        /// <summary>
        /// 导航命令（代理到 Navigation.NavigateCommand）
        /// </summary>
        public AsyncRelayCommand<object> NavigateCommand => (AsyncRelayCommand<object>)Navigation.NavigateCommand;

        /// <summary>
        /// 用户会话服务（共享的用户状态）
        /// </summary>
        private readonly IUserSessionService _sessionService;

        /// <summary>
        /// 消息服务
        /// </summary>
        private readonly IMessenger _messenger;

        /// <summary>
        /// 当前用户名（来自会话服务）
        /// </summary>
        public string CurrentUserName => _sessionService?.CurrentUser?.Username ?? "未登录";

        /// <summary>
        /// 当前用户角色（来自会话服务）
        /// </summary>
        public UserRole CurrentUserRole =>
            _sessionService?.CurrentUser?.Role ?? UserRole.Operator;

        /// <summary>
        /// 是否已登录（来自会话服务）
        /// </summary>
        public bool IsLoggedIn => _sessionService?.IsLoggedIn ?? false;

        /// <summary>
        /// 是否为管理员（来自会话服务）
        /// </summary>
        public bool IsAdministrator => _sessionService?.IsAdministrator ?? false;

        /// <summary>
        /// 用户菜单是否打开
        /// </summary>
        [ObservableProperty]
        private bool _isUserMenuOpen;

        /// <summary>
        /// 是否显示自动退出倒计时
        /// </summary>
        [ObservableProperty]
        private bool _showLogoutCountdown;

        /// <summary>
        /// 剩余自动退出时间（秒）
        /// </summary>
        [ObservableProperty]
        private int _remainingLogoutSeconds;

        public MainViewViewModel(
            MainViewModel navigationViewModel,
            UserMenuViewModel userMenuViewModel,
            IUserSessionService sessionService,
            IMessenger messenger = null)
        {
            Navigation = navigationViewModel ?? throw new ArgumentNullException(nameof(navigationViewModel));
            UserMenu = userMenuViewModel ?? throw new ArgumentNullException(nameof(userMenuViewModel));
            _sessionService = sessionService ?? throw new ArgumentNullException(nameof(sessionService));
            _messenger = messenger ?? WeakReferenceMessenger.Default;

            // ⚠️ 关键修复：监听会话变化消息，而不是PropertyChanged事件
            // UserSessionService使用消息机制通知状态变化
            _messenger.Register<UserSessionChangedMessage>(this, OnUserSessionChanged);
            _messenger.Register<AutoLogoutWarningMessage>(this, OnAutoLogoutWarning);

            System.Diagnostics.Debug.WriteLine("[MainViewViewModel] 复合ViewModel初始化完成");
        }

        /// <summary>
        /// 处理用户会话变化消息
        /// </summary>
        private void OnUserSessionChanged(object recipient, UserSessionChangedMessage message)
        {
            // 当会话状态改变时，通知所有依赖的属性更新
            OnPropertyChanged(nameof(CurrentUserName));
            OnPropertyChanged(nameof(CurrentUserRole));
            OnPropertyChanged(nameof(IsLoggedIn));
            OnPropertyChanged(nameof(IsAdministrator));

            System.Diagnostics.Debug.WriteLine(
                $"[MainViewViewModel] 会话状态更新: {message.CurrentUser?.Username ?? "未登录"}, " +
                $"原因: {message.Reason}");
        }

        /// <summary>
        /// 处理自动退出警告消息
        /// </summary>
        private void OnAutoLogoutWarning(object recipient, AutoLogoutWarningMessage message)
        {
            ShowLogoutCountdown = true;
            RemainingLogoutSeconds = message.RemainingSeconds;

            // 如果倒计时结束，隐藏显示
            if (message.RemainingSeconds <= 0)
            {
                ShowLogoutCountdown = false;
            }
        }

        /// <summary>
        /// 初始化（在视图加载后调用）
        /// </summary>
        public async System.Threading.Tasks.Task InitializeAsync()
        {
            await Navigation.InitializeNavigationAsync();
            System.Diagnostics.Debug.WriteLine("[MainViewViewModel] 初始化完成");
        }

        /// <summary>
        /// 更新用户活动时间（用于自动退出计时器）
        /// </summary>
        public void UpdateActivity()
        {
            _sessionService?.UpdateActivity();
        }

        /// <summary>
        /// 显示登录对话框命令（代理到 UserMenu）
        /// </summary>
        public RelayCommand ShowLoginCommand => (RelayCommand)UserMenu.ShowLoginCommand;

        /// <summary>
        /// 切换用户命令（代理到 UserMenu）
        /// </summary>
        public RelayCommand SwitchUserCommand => (RelayCommand)UserMenu.SwitchUserCommand;

        /// <summary>
        /// 修改密码命令（代理到 UserMenu）
        /// </summary>
        public RelayCommand ChangePasswordCommand => (RelayCommand)UserMenu.ChangePasswordCommand;

        /// <summary>
        /// 退出登录命令（代理到 UserMenu）
        /// </summary>
        public RelayCommand LogoutCommand => (RelayCommand)UserMenu.LogoutCommand;

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            // 取消注册消息
            _messenger.Unregister<UserSessionChangedMessage>(this);
            _messenger.Unregister<AutoLogoutWarningMessage>(this);

            Navigation?.Dispose();
            UserMenu?.Dispose();
        }
    }
}
