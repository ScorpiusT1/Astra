using Astra.Core.Access.Data;
using Astra.Core.Access.Extensions;
using Astra.Core.Access.Services;
using Astra.Core.Configuration;
using Astra.Core.Devices.Events;
using Astra.Core.Devices.Management;
using Astra.Core.Logs;
using Astra.Core.Plugins.Abstractions;
using Astra.Core.Plugins.Caching;
using Astra.Core.Plugins.Configuration;
using Astra.Core.Plugins.Concurrency;
using Astra.Core.Plugins.Exceptions;
using Astra.Core.Plugins.Health;
using Astra.Core.Plugins.Host;
using Astra.Core.Plugins.Manifest.Serializers;
using Astra.Core.Plugins.Memory;
using Astra.Core.Plugins.Messaging;
using Astra.Core.Plugins.Performance;
using Astra.Core.Plugins.Recovery;
using Astra.Core.Plugins.Security;
using Astra.Core.Plugins.Validation;
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
using Astra.Core.Plugins.Loading;
using Astra.Core.Plugins.Discovery;

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
            RegisterDeviceConfigServices(services);
            RegisterPluginServices(services);
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
            // ⭐ 主窗口应该是单例，整个应用程序只有一个实例
            services.AddSingleton<MainView>();
            
            // 其他视图可以保持 Transient（每次导航时创建新实例）
            services.AddTransient<ConfigView>();
            services.AddTransient<DebugView>();
            services.AddTransient<HomeView>();
            services.AddTransient<SequenceView>();
            services.AddTransient<PermissionView>();
           
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
        /// 注册设备配置服务
        /// </summary>
        private void RegisterDeviceConfigServices(IServiceCollection services)
        {
            // 注册配置管理器（单例，统一管理所有配置）
            // 注意：配置是独立的，不依赖设备。设备是根据配置创建的，而不是从设备获取配置。
            services.AddSingleton<ConfigurationManager>(provider =>
            {
                var configManager = new ConfigurationManager();
                
                // 注意：配置管理器不订阅设备管理器事件
                // 配置和设备是分离的：先有配置，然后根据配置创建设备
                // 设备注销时，配置仍然保留在 ConfigurationManager 中（可以用于重新创建设备）

                return configManager;
            });

            Debug.WriteLine("✅ 配置管理器注册完成");
        }

        /// <summary>
        /// 注册插件服务
        /// ⭐ 在服务注册阶段只注册插件需要的服务到 IServiceCollection，IPluginHost 在 ServiceProvider 构建后手动创建
        /// </summary>
        private void RegisterPluginServices(IServiceCollection services)
        {
            // ⭐ 不在注册阶段创建 IPluginHost，而是在 ServiceProvider 构建后手动创建
            // 这样可以确保在 ServiceProvider 构建之前就有 IPluginHost 的注册描述符（虽然是空的）
            // 实际创建会在 ApplicationBootstrapper 中，ServiceProvider 构建完成后进行
            
            // 先确保插件需要的服务都注册到 IServiceCollection 中
            // 注意：这里不能传入 ServiceProvider（因为还未构建），只能注册服务描述符
            EnsurePluginServicesRegisteredBeforeBuild(services);

            Debug.WriteLine("✅ 插件服务注册完成（IPluginHost 将在 ServiceProvider 构建后创建）");
        }

        /// <summary>
        /// 在 ServiceProvider 构建之前，确保所有插件需要的服务都注册到 IServiceCollection 中
        /// ⭐ 注意：此时 ServiceProvider 还未构建，只能注册服务描述符，不能创建实例
        /// </summary>
        private void EnsurePluginServicesRegisteredBeforeBuild(IServiceCollection services)
        {
            // 检查服务是否已在 IServiceCollection 中注册
            bool IsServiceRegistered<T>() => services.Any(s => s.ServiceType == typeof(T));

            // 确保 IConfiguration 存在
            if (!IsServiceRegistered<Microsoft.Extensions.Configuration.IConfiguration>())
            {
                var configurationRoot = ConfigurationProviderRegistration.BuildDefaultConfiguration();
                services.AddSingleton<Microsoft.Extensions.Configuration.IConfiguration>(configurationRoot);
            }

            // 确保 IMessageBus 存在
            if (!IsServiceRegistered<IMessageBus>())
            {
                services.AddSingleton<IMessageBus, MessageBus>();
            }

            // 确保 IPermissionManager 存在
            if (!IsServiceRegistered<IPermissionManager>())
            {
                services.AddSingleton<IPermissionManager, PermissionManager>();
            }

            // 确保 IConfigurationStore 存在
            if (!IsServiceRegistered<IConfigurationStore>())
            {
                services.AddSingleton<IConfigurationStore>(new ConfigurationStore());
            }

            // ⭐ 确保 IErrorLogger 存在（这是关键服务，其他服务依赖它）
            if (!IsServiceRegistered<IErrorLogger>())
            {
                services.AddSingleton<IErrorLogger>(new FileErrorLogger("plugin-system.log"));
            }

            // ⭐ 确保 IExceptionHandler 存在（依赖 IErrorLogger）
            if (!IsServiceRegistered<IExceptionHandler>())
            {
                services.AddSingleton<IExceptionHandler>(sp =>
                {
                    var logger = sp.GetService<IErrorLogger>() ?? new FileErrorLogger("plugin-system.log");
                    return new ExceptionHandler(logger);
                });
            }

            // ⭐ 确保 IHealthCheckService 存在（依赖 IErrorLogger）
            if (!IsServiceRegistered<IHealthCheckService>())
            {
                services.AddSingleton<IHealthCheckService>(sp =>
                {
                    var logger = sp.GetService<IErrorLogger>() ?? new FileErrorLogger("plugin-system.log");
                    return new HealthCheckService(logger);
                });
            }

            // ⭐ 确保 ISelfHealingService 存在（依赖 IErrorLogger 和 IHealthCheckService）
            if (!IsServiceRegistered<ISelfHealingService>())
            {
                services.AddSingleton<ISelfHealingService>(sp =>
                {
                    var logger = sp.GetService<IErrorLogger>() ?? new FileErrorLogger("plugin-system.log");
                    var healthCheck = sp.GetService<IHealthCheckService>();
                    if (healthCheck == null)
                    {
                        healthCheck = new HealthCheckService(logger);
                    }
                    return new SelfHealingService(logger, healthCheck);
                });
            }

            // ⭐ 注册插件核心服务（IPluginValidator、IPluginDiscovery、IPluginLoader 等）
            // 这些服务在 PluginHostFactory.CreateDefaultHost 中需要解析，必须提前注册到主应用的 IServiceCollection 中
            
            // 1. 注册 IPluginValidator（插件验证器）
            if (!IsServiceRegistered<IPluginValidator>())
            {
                services.AddSingleton<IPluginValidator>(sp =>
                {
                    var validator = new PluginValidator();
                    // 默认验证规则（PluginValidator 构造函数已经添加了这些规则，这里只是为了确保）
                    // validator.AddRule(new AssemblyExistsRule());
                    // validator.AddRule(new DependencyValidRule());
                    // validator.AddRule(new VersionValidRule());
                    return validator;
                });
            }

            // 2. 注册 IManifestSerializer（清单序列化器） - 多个实现
            if (!services.Any(s => s.ServiceType == typeof(IManifestSerializer)))
            {
                services.AddSingleton<IManifestSerializer>(sp => new XmlManifestSerializer());
                services.AddSingleton<IManifestSerializer>(sp => new JsonManifestSerializer());
                services.AddSingleton<IManifestSerializer>(sp => new YamlManifestSerializer());
            }

            // 3. 注册 IPluginDiscovery（插件发现服务）
            if (!IsServiceRegistered<IPluginDiscovery>())
            {
                services.AddSingleton<IPluginDiscovery>(sp =>
                {
                    // 获取所有 IManifestSerializer 实现
                    var serializers = sp.GetServices<IManifestSerializer>().ToList();
                    var baseDiscovery = new FileSystemDiscovery(serializers);
                    // 使用默认的并发数（8）
                    var discovery = new ParallelPluginDiscovery(baseDiscovery, maxConcurrentDiscoveries: 8);
                    return discovery;
                });
            }

            // 4. 注册 IPerformanceMonitor（性能监控器）
            if (!IsServiceRegistered<IPerformanceMonitor>())
            {
                services.AddSingleton<IPerformanceMonitor>(sp => new PerformanceMonitor());
            }

            // 5. 注册 IMemoryManager（内存管理器）
            if (!IsServiceRegistered<IMemoryManager>())
            {
                services.AddSingleton<IMemoryManager>(sp => new MemoryManager());
            }

            // 6. 注册 IConcurrencyManager（并发管理器）
            if (!IsServiceRegistered<IConcurrencyManager>())
            {
                services.AddSingleton<IConcurrencyManager>(sp =>
                {
                    var performanceMonitor = sp.GetService<IPerformanceMonitor>() ?? new PerformanceMonitor();
                    return new ConcurrencyManager(performanceMonitor);
                });
            }

            // 7. 注册 ICacheManager（缓存管理器）
            if (!IsServiceRegistered<ICacheManager>())
            {
                services.AddSingleton<ICacheManager>(sp =>
                {
                    var performanceMonitor = sp.GetService<IPerformanceMonitor>() ?? new PerformanceMonitor();
                    return new CacheManager(performanceMonitor);
                });
            }

            // 8. 注册 IPluginLoader（插件加载器）
            if (!IsServiceRegistered<IPluginLoader>())
            {
                services.AddSingleton<IPluginLoader>(sp =>
                {
                    var performanceMonitor = sp.GetService<IPerformanceMonitor>();
                    var memoryManager = sp.GetService<IMemoryManager>();
                    // 使用默认的并发加载数（4）
                    var loader = new HighPerformancePluginLoader(
                        performanceMonitor,
                        memoryManager,
                        maxConcurrentLoads: 4);
                    return loader;
                });
            }

            // ⭐ 注册 IPluginHost 服务（使用工厂方法，在第一次解析时创建）
            // 工厂方法会接收主应用的 ServiceProvider，用它来创建 IPluginHost
            // 这样可以确保 IPluginHost 在 ServiceProvider 构建之前就注册到 IServiceCollection 中
            // 但实际创建延迟到第一次解析时，此时 ServiceProvider 已经构建完成，可以使用主应用的 ServiceProvider
            services.AddSingleton<IPluginHost>(provider =>
            {
                // 创建插件宿主配置
                var config = new HostConfiguration
                {
                    PluginDirectory = Path.Combine(
                        System.AppDomain.CurrentDomain.BaseDirectory,
                        "Plugins"),
                    EnableHotReload = false,
                    RequireSignature = false,
                    Services = new ServiceConfiguration
                    {
                        EnableDefaultSerializers = true,
                        EnableDefaultValidationRules = true
                    },
                    Performance = new PerformanceConfiguration
                    {
                        EnablePerformanceMonitoring = true,
                        EnableMemoryManagement = true,
                        EnableConcurrencyControl = true,
                        EnableCaching = true,
                        MaxConcurrentLoads = 4,
                        MaxConcurrentDiscoveries = 8
                    },
                    Security = new SecurityConfiguration
                    {
                        RequireSignature = false,
                        EnableSandbox = true,
                        SandboxType = SandboxType.AppDomain
                    }
                };

                // ⭐ 使用 PluginHostFactory 创建插件宿主
                // ⭐ 传入主应用的 ServiceProvider（provider 参数就是主应用的 ServiceProvider）
                // 这样可以确保插件系统和主应用使用同一个 ServiceProvider 实例，保证单例服务共享
                var pluginHost = PluginHostFactory.CreateDefaultHost(services, config, provider);
                
                Debug.WriteLine("✅ IPluginHost 已通过工厂方法创建并注册到 ServiceProvider");
                
                return pluginHost;
            });
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
