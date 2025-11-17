using Astra.Core.Access.Data;
using Astra.Core.Access.Extensions;
using Astra.Core.Access.Services;
using Astra.Core.Devices.Events;
using Astra.Core.Devices.Management;
using Astra.Core.Logs;
using Astra.Core.Plugins.Messaging;
using Astra.Models;
using Astra.Services.Authorization;
using Astra.Services.Dialogs;
using Astra.Services.Navigation;
using Astra.Services.Session;
using Astra.Views;
using Microsoft.Extensions.DependencyInjection;
using NavStack.Extensions;
using System.Diagnostics;
using System.IO;

namespace Astra.Services.Startup
{
    /// <summary>
    /// 服务注册配置器 - 集中管理所有服务注册
    /// </summary>
    public class ServiceRegistrationConfigurator
    {
        public void ConfigureServices(IServiceCollection services)
        {
            RegisterLoggingServices(services);
            RegisterAccessGuardServices(services);
            RegisterNavigationServices(services);
            RegisterViews(services);
            RegisterApplicationServices(services);
            RegisterDeviceServices(services);
            RegisterRefactoredViewModels(services);
        }

        /// <summary>
        /// 注册日志服务
        /// </summary>
        private void RegisterLoggingServices(IServiceCollection services)
        {
            // 注册 ILogger（单例）
            // 使用开发环境配置，支持控制台输出和文件输出
            services.AddSingleton<ILogger>(provider =>
            {
                // 创建日志文件路径（在应用程序目录下的 Logs 文件夹）
                var logDirectory = Path.Combine(
                    System.AppDomain.CurrentDomain.BaseDirectory,
                    "Logs");
                
                // 确保日志目录存在
                if (!Directory.Exists(logDirectory))
                {
                    Directory.CreateDirectory(logDirectory);
                }
                
                var logFilePath = Path.Combine(logDirectory, "application.log");
                
                // 使用 LogConfigBuilder 创建配置
                var config = LogConfig.CreateBuilder()
                    .WithName("Application")
                    .WithLevel(LogLevel.Debug)
                    .WithConsole(true)
                    .WithFile(logFilePath)
                    .WithAsyncMode(true)
                    .WithDefaultTriggerUIEvent(true)
                    .Build();
                
                return new Logger(config);
            });

            Debug.WriteLine("✅ 日志服务注册完成");
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
        /// 注册视图
        /// </summary>
        private void RegisterViews(IServiceCollection services)
        {
            services.AddTransient<MainView>();
            // ❗ 旧版 MainViewModel 已被重构，不再注册
            // services.AddSingleton<MainViewModel>();
            services.AddTransient<ConfigView>();
            services.AddTransient<DebugView>();
           
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
        /// 注册设备管理服务
        /// </summary>
        private void RegisterDeviceServices(IServiceCollection services)
        {
            // 注册设备使用跟踪服务（单例）
            services.AddSingleton<IDeviceUsageService, DeviceUsageService>();

            // 注册设备事件发布器（单例）
            // 注意：DeviceEventPublisher 需要 IMessageBus，如果未注册则使用 NullDeviceEventPublisher
            services.AddSingleton<IDeviceEventPublisher>(provider =>
            {
                try
                {
                    // 尝试从插件系统获取 IMessageBus
                    var messageBus = provider.GetService<IMessageBus>();
                    if (messageBus != null)
                    {
                        return new DeviceEventPublisher(messageBus);
                    }
                }
                catch
                {
                    // 如果获取失败，使用空实现
                }
                return NullDeviceEventPublisher.Instance;
            });

            // 注册设备管理器（单例）
            // ⚠️ 注意：由于 ServiceCollectionAdapter 每次构建新的 ServiceProvider 时会创建新的单例实例，
            // 插件系统和主应用可能会使用不同的 DeviceManager 实例。
            // 解决方案：插件应该通过 App.ServiceProvider 获取主应用的 DeviceManager 实例（已在插件中实现）。
            services.AddSingleton<IDeviceManager>(provider =>
            {
                // 尝试获取 ILogger（可选）
                var logger = provider.GetService<ILogger>();
                
                // 获取 IDeviceUsageService（已注册）
                var usageService = provider.GetService<IDeviceUsageService>();
                
                // 获取 IDeviceEventPublisher（已注册）
                var eventPublisher = provider.GetService<IDeviceEventPublisher>();
                
                return new DeviceManager(logger, usageService, eventPublisher);
            });

            Debug.WriteLine("✅ 设备管理服务注册完成");
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
            services.AddTransient<ViewModels.ConfigViewModel>();
            services.AddTransient<ViewModels.DebugViewModel>();

            Debug.WriteLine("✅ 重构后的 ViewModels 注册完成");
        }
    }
}
