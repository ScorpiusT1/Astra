using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NavStack.Configuration;
using NavStack.Core;
using NavStack.Regions;

namespace NavStack.Services
{
    /// <summary>
    /// 导航管理器实现：通过 itemId 映射到区域与页面键，并委托给区域导航服务
    /// </summary>
    public sealed class NavigationManager : INavigationManager
    {
        private readonly IRegionManager _regionManager;
        private readonly INavigationConfiguration _configuration;

        private readonly Dictionary<string, (string regionName, string pageKey)> _routes = new(StringComparer.Ordinal);
        private string? _currentRegionName;

        /// <summary>
        /// 构造 NavigationManager
        /// </summary>
        /// <param name="regionManager">区域管理器</param>
        /// <param name="configuration">页面注册配置</param>
        public NavigationManager(IRegionManager regionManager, INavigationConfiguration configuration)
        {
            _regionManager = regionManager;
            _configuration = configuration;
        }

        /// <inheritdoc/>
        public async Task<NavigationResult> NavigateAsync(string itemId, NavigationParameters parameters = null)
        {
            if (!_routes.TryGetValue(itemId, out var route))
            {
                return NavigationResult.Failed($"Route for item '{itemId}' not registered.");
            }
            var service = _regionManager.GetNavigationService(route.regionName);
            if (service == null)
            {
                return NavigationResult.Failed($"Region '{route.regionName}' not registered.");
            }
            _currentRegionName = route.regionName;
            return await service.NavigateAsync(route.pageKey, parameters).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public Task<bool> CanNavigateAsync(string itemId)
        {
            if (!_routes.TryGetValue(itemId, out var route)) return Task.FromResult(false);
            return Task.FromResult(_configuration.GetPageRegistration(route.pageKey) != null
                && _regionManager.GetNavigationService(route.regionName) != null);
        }

        /// <inheritdoc/>
        public void RegisterForRegion(string regionName, string itemId, Type viewType, Type viewModelType)
        {
            if (regionName == null) throw new ArgumentNullException(nameof(regionName));
            if (itemId == null) throw new ArgumentNullException(nameof(itemId));
            if (viewType == null) throw new ArgumentNullException(nameof(viewType));
            if (viewModelType == null) throw new ArgumentNullException(nameof(viewModelType));

            // 检查页面是否已在配置中注册（避免重复注册）
            // 如果页面已通过 NavigationModule 注册，则跳过注册步骤，只建立路由映射
            var existingRegistration = _configuration.GetPageRegistration(itemId);
            if (existingRegistration == null)
            {
                // 页面未注册，进行注册（向后兼容：支持直接通过 RegisterForRegion 注册）
                _configuration.RegisterPage(itemId, viewType, viewModelType);
            }
            else
            {
                // 页面已注册，验证类型是否匹配（可选，用于调试）
                if (existingRegistration.ViewType != viewType || existingRegistration.ViewModelType != viewModelType)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[NavigationManager] 警告：页面 '{itemId}' 已注册，但类型不匹配。已注册: {existingRegistration.ViewType.Name}/{existingRegistration.ViewModelType?.Name}, 当前: {viewType.Name}/{viewModelType.Name}");
                }
            }

            // 建立路由映射：itemId -> (regionName, pageKey)
            _routes[itemId] = (regionName, itemId);
        }

        /// <inheritdoc/>
        public void RequestNavigate(string regionName, string pageKey, NavigationParameters parameters = null)
        {
            var service = _regionManager.GetNavigationService(regionName);
            if (service == null) return;
            _currentRegionName = regionName;
            _ = service.NavigateAsync(pageKey, parameters);
        }

        /// <inheritdoc/>
        public bool CanGoBack
        {
            get
            {
                var s = GetCurrentRegionService();
                return s?.CanGoBack == true;
            }
        }

        /// <inheritdoc/>
        public bool CanGoForward
        {
            get
            {
                var s = GetCurrentRegionService();
                return s?.CanGoForward == true;
            }
        }

        /// <inheritdoc/>
        public void GoBack()
        {
            var s = GetCurrentRegionService();
            if (s != null && s.CanGoBack)
            {
                _ = s.GoBackAsync();
            }
        }

        /// <inheritdoc/>
        public void GoForward()
        {
            var s = GetCurrentRegionService();
            if (s != null && s.CanGoForward)
            {
                _ = s.GoForwardAsync();
            }
        }

        private IRegionNavigationService GetCurrentRegionService()
        {
            return _currentRegionName != null ? _regionManager.GetNavigationService(_currentRegionName) : null;
        }

    }
}


