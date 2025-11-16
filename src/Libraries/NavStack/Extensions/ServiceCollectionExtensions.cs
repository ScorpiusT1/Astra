using Microsoft.Extensions.DependencyInjection;
using NavStack.Configuration;
using NavStack.Core;
using NavStack.Regions;
using NavStack.Services;
using NavStack.Modularity;
using System;
using NavStack.Authorization;

namespace NavStack.Extensions
{
    /// <summary>
    /// 服务集合扩展 - 添加模块化支持
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// 添加NavStack导航框架（支持模块化）
        /// </summary>
        public static IServiceCollection AddStackNav(
            this IServiceCollection services,
            Action<INavigationConfiguration> configure = null)
        {
            // 注册配置
            var configuration = new NavigationConfiguration();
            configure?.Invoke(configuration);
            services.AddSingleton<INavigationConfiguration>(configuration);

            // 注册核心服务
            services.AddSingleton<IFrameNavigationService, FrameNavigationService>();
            services.AddSingleton<IWindowNavigationService, WindowNavigationService>();
			services.AddSingleton<INavigationTreeService, NavigationTreeService>();
			services.AddSingleton<IDragDropService, DragDropService>();
			services.AddSingleton<INavigationAuthorizationService, NavigationAuthorizationService>();
			services.AddSingleton<INavigationManager, NavigationManager>();
            services.AddSingleton<IRegionManager, RegionManager>();

            // 注册模块管理器
            services.AddSingleton<NavigationModuleManager>();
            services.AddSingleton<NavigationModuleLoader>();
           
            services.AddSingleton<NavigationModuleLifecycleManager>();
            services.AddSingleton<NavigationMenuAggregator>();

            // 注册所有页面和ViewModel
            foreach (var registration in configuration.GetAllRegistrations())
            {
                RegisterPageAndViewModel(services, registration);
            }

            return services;
        }

        /// <summary>
        /// 添加NavStack导航框架（支持模块化配置）
        /// </summary>
        public static IServiceCollection AddStackNav(
            this IServiceCollection services,
            Action<NavigationModuleManager, INavigationConfiguration> configureModules)
        {
            var configuration = new NavigationConfiguration();
            var moduleManager = new NavigationModuleManager();

            // 配置模块
            configureModules?.Invoke(moduleManager, configuration);

            // 初始化所有模块
            moduleManager.InitializeModules(configuration, services);

            // 注册配置和服务
            services.AddSingleton<INavigationConfiguration>(configuration);
            services.AddSingleton<IFrameNavigationService, FrameNavigationService>();
            services.AddSingleton<IWindowNavigationService, WindowNavigationService>();
			services.AddSingleton<INavigationTreeService, NavigationTreeService>();
			services.AddSingleton<IDragDropService, DragDropService>();
			services.AddSingleton<INavigationAuthorizationService, NavigationAuthorizationService>();
			services.AddSingleton<INavigationManager, NavigationManager>();
            services.AddSingleton<IRegionManager, RegionManager>();
            services.AddSingleton(moduleManager);

            // 注册模块生命周期管理
            var lifecycleManager = new NavigationModuleLifecycleManager();

            foreach (var module in moduleManager.GetModules().OfType<INavigationModuleLifecycle>())
            {
                lifecycleManager.RegisterLifecycleModule(module);
            }

            services.AddSingleton(lifecycleManager);

            // 注册菜单聚合器
            services.AddSingleton(sp =>
            {
                var modules = sp.GetService<NavigationModuleManager>()?.GetModules() ?? new List<INavigationModule>();
                return new NavigationMenuAggregator(modules);
            });

            // 注册所有页面和ViewModel
            foreach (var registration in configuration.GetAllRegistrations())
            {
                RegisterPageAndViewModel(services, registration);
            }

            return services;
        }

        /// <summary>
        /// 使用模块自动发现
        /// </summary>
        public static IServiceCollection AddStackNavWithAutoDiscovery(
            this IServiceCollection services,
            Action<NavigationModuleLoader> configureLoader = null)
        {
            var configuration = new NavigationConfiguration();
            var moduleManager = new NavigationModuleManager();

            // 自动发现模块
            var modules = NavigationModuleLoader.LoadFromCurrentDomain();

            foreach (var module in modules)
            {
                moduleManager.RegisterModule(module);
            }

            configureLoader?.Invoke(NavigationModuleLoader.LoadFromCurrentDomain().FirstOrDefault() as NavigationModuleLoader);

            // 初始化模块
            moduleManager.InitializeModules(configuration, services);

            // 注册服务
            return services.AddStackNav(config => { });
        }

        private static void RegisterPageAndViewModel(IServiceCollection services, PageRegistration registration)
        {
            if (registration.IsSingleton)
            {
                services.AddSingleton(registration.ViewType);

                if (registration.ViewModelType != null)
                {
                    services.AddSingleton(registration.ViewModelType);
                }
            }
            else
            {
                services.AddTransient(registration.ViewType);
                if (registration.ViewModelType != null)
                {
                    services.AddTransient(registration.ViewModelType);
                }
            }
        }
    }
}
