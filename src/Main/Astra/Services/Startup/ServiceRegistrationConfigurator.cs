using Astra.Core.Access.Data;
using Astra.Core.Access.Extensions;
using Astra.Core.Access.Services;
using Astra.Models;
using Astra.Services.Authorization;
using Astra.Services.Dialogs;
using Astra.Services.Navigation;
using Astra.Services.Session;
using Astra.Views;
using Microsoft.Extensions.DependencyInjection;
using NavStack.Extensions;
using System.Diagnostics;

namespace Astra.Services.Startup
{
    /// <summary>
    /// 服务注册配置器 - 集中管理所有服务注册
    /// </summary>
    public class ServiceRegistrationConfigurator
    {
        public void ConfigureServices(IServiceCollection services)
        {
            RegisterAccessGuardServices(services);
            RegisterNavigationServices(services);
            RegisterViewsAndViewModels(services);
            RegisterApplicationServices(services);
            RegisterRefactoredViewModels(services);
        }

        /// <summary>
        /// 注册 Access 服务
        /// </summary>
        private void RegisterAccessGuardServices(IServiceCollection services)
        {
            services.AddAccessGuard();

            // 明确注册 UserManagementService，解决构造函数歧义问题
            services.AddScoped<IUserManagementService>(provider =>
            {
                var context = provider.GetRequiredService<AccessGuardDbContext>();
                return new UserManagementService(context);
            });

            Debug.WriteLine("✅ Access 服务注册完成");
        }

        /// <summary>
        /// 注册导航服务
        /// </summary>
        private void RegisterNavigationServices(IServiceCollection services)
        {
            services.AddStackNav((moduleManager, config) =>
            {
                moduleManager.RegisterModule(new NavigationModule());
                // 可在此处注册其他模块
            });

            Debug.WriteLine("✅ 导航服务注册完成");
        }

        /// <summary>
        /// 注册视图（不再注册旧版 ViewModel）
        /// </summary>
        private void RegisterViewsAndViewModels(IServiceCollection services)
        {
            services.AddTransient<MainView>();
            // ❗ 旧版 MainViewModel 已被重构，不再注册
            // services.AddSingleton<MainViewModel>();

            Debug.WriteLine("✅ 视图注册完成");
        }

        /// <summary>
        /// 注册应用程序服务（重构后的服务层）
        /// </summary>
        private void RegisterApplicationServices(IServiceCollection services)
        {
            // ⭐ 注册 UserSessionService 并配置自动退出
            services.AddSingleton<IUserSessionService>(provider =>
            {
                var sessionService = new UserSessionService();
                // 启用自动退出功能，5分钟超时
                sessionService.ConfigureAutoLogout(enabled: true, timeoutMinutes: 5);
                return sessionService;
            });
            
            services.AddSingleton<IPermissionService, PermissionService>();
            services.AddSingleton<IDialogFactory, DialogFactory>();
            services.AddSingleton<IDialogService, DialogService>();
            
            // ⭐ 注册导航权限服务
            services.AddSingleton<INavigationPermissionService, NavigationPermissionService>();

            Debug.WriteLine("✅ 应用程序服务注册完成");
        }

        /// <summary>
        /// 注册重构后的 ViewModels
        /// </summary>
        private void RegisterRefactoredViewModels(IServiceCollection services)
        {
            services.AddTransient<ViewModels.PermissionViewModel>();
            services.AddTransient<ViewModels.UserMenuViewModel>();
            services.AddTransient<ViewModels.MainViewModel>();
            services.AddSingleton<ViewModels.MainViewViewModel>();

            Debug.WriteLine("✅ 重构后的 ViewModels 注册完成");
        }
    }
}
