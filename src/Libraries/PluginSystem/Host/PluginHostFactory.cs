using System;
using System.Linq;
using Addins.Configuration;
using Addins.Core.Abstractions;
using Addins.Core.Discovery;
using Addins.Core.Lifecycle;
using Addins.Exceptions;
using Addins.Health;
using Addins.Management;
using Addins.Manifest.Serializers;
using Addins.Messaging;
using Addins.Recovery;
using Addins.Security;
using Addins.Services;
using Addins.Validation;
using Addins.Performance;
using Addins.Memory;
using Addins.Core.Loading;
using Addins.Concurrency;
using Addins.Caching;

namespace Addins.Host
{
    /// <summary>
    /// 插件宿主工厂
    /// </summary>
    public static class PluginHostFactory
    {
        /// <summary>
        /// 创建默认宿主
        /// </summary>
        public static IPluginHost CreateDefaultHost(HostConfiguration config)
        {
            var serviceRegistry = CreateServiceRegistry(config.Services);
            var performanceServices = CreatePerformanceServices(config.Performance);
            
            var baseHost = new PluginHost(
                serviceRegistry.Resolve<IPluginDiscovery>(),
                serviceRegistry,
                serviceRegistry.Resolve<IMessageBus>(),
                serviceRegistry.Resolve<IPermissionManager>(),
                serviceRegistry.Resolve<IPluginValidator>(),
                serviceRegistry.Resolve<IExceptionHandler>(),
                serviceRegistry.Resolve<IErrorLogger>(),
                serviceRegistry.Resolve<IHealthCheckService>(),
                serviceRegistry.Resolve<ISelfHealingService>()
            );
            
            // 应用性能优化装饰器
            var optimizedHost = new ConcurrencyControlledPluginHost(baseHost, performanceServices.ConcurrencyManager);
            var cachedHost = new CachedPluginHost(optimizedHost, performanceServices.CacheManager);
            
            return cachedHost;
        }

        /// <summary>
        /// 创建高性能宿主
        /// </summary>
        public static IPluginHost CreateHighPerformanceHost(HostConfiguration config)
        {
            config.Performance.EnablePerformanceMonitoring = true;
            config.Performance.EnableMemoryManagement = true;
            config.Performance.EnableConcurrencyControl = true;
            config.Performance.EnableCaching = true;
            config.Performance.MaxConcurrentLoads = 8;
            config.Performance.MaxConcurrentDiscoveries = 16;
            
            return CreateDefaultHost(config);
        }

        /// <summary>
        /// 创建安全宿主
        /// </summary>
        public static IPluginHost CreateSecureHost(HostConfiguration config)
        {
            config.Security.RequireSignature = true;
            config.Security.EnableSandbox = true;
            config.Security.SandboxType = SandboxType.AppDomain;
            
            return CreateDefaultHost(config);
        }

        /// <summary>
        /// 创建轻量级宿主
        /// </summary>
        public static IPluginHost CreateLightweightHost(HostConfiguration config)
        {
            // 轻量级配置
            config.Performance.EnablePerformanceMonitoring = false;
            config.Performance.EnableMemoryManagement = false;
            config.Performance.EnableConcurrencyControl = false;
            config.Performance.EnableCaching = false;
            config.Security.EnableSandbox = false;
            
            return CreateDefaultHost(config);
        }

        /// <summary>
        /// 创建开发环境宿主
        /// </summary>
        public static IPluginHost CreateDevelopmentHost(HostConfiguration config)
        {
            // 开发环境配置
            config.EnableHotReload = true;
            config.Security.RequireSignature = false;
            config.Security.EnableSandbox = false;
            config.Performance.EnablePerformanceMonitoring = true;
            
            return CreateDefaultHost(config);
        }

        /// <summary>
        /// 创建生产环境宿主
        /// </summary>
        public static IPluginHost CreateProductionHost(HostConfiguration config)
        {
            // 生产环境配置
            config.EnableHotReload = false;
            config.Security.RequireSignature = true;
            config.Security.EnableSandbox = true;
            config.Performance.EnableAllOptimizations();
            
            return CreateDefaultHost(config);
        }

        private static ServiceRegistry CreateServiceRegistry(ServiceConfiguration config)
        {
            var registry = new ServiceRegistry();
            
            // 注册核心服务
            registry.RegisterSingleton<IServiceRegistry>(registry);
            registry.RegisterSingleton<IMessageBus, MessageBus>();
            registry.RegisterSingleton<IPermissionManager, PermissionManager>();
            registry.RegisterSingleton<IConfigurationStore>(new ConfigurationStore());
            registry.RegisterSingleton(new PluginLifecycleManager());

            // 注册异常处理服务
            registry.RegisterSingleton<IErrorLogger>(new FileErrorLogger("plugin-system.log"));
            registry.RegisterSingleton<IExceptionHandler>(() => new ExceptionHandler(registry.Resolve<IErrorLogger>()));
            registry.RegisterSingleton<IHealthCheckService>(() => new HealthCheckService(registry.Resolve<IErrorLogger>()));
            registry.RegisterSingleton<ISelfHealingService>(() => new SelfHealingService(
                registry.Resolve<IErrorLogger>(), 
                registry.Resolve<IHealthCheckService>()));

            // 注册清单序列化器
            if (config.EnableDefaultSerializers)
            {
                registry.RegisterSingleton<IManifestSerializer>(new XmlManifestSerializer());
                registry.RegisterSingleton<IManifestSerializer>(new JsonManifestSerializer());
                registry.RegisterSingleton<IManifestSerializer>(new YamlManifestSerializer());
            }

            // 注册自定义序列化器
            foreach (var serializerType in config.ManifestSerializers)
            {
                registry.RegisterSingleton<IManifestSerializer>((IManifestSerializer)Activator.CreateInstance(serializerType));
            }

            // 注册高性能插件发现服务
            registry.RegisterSingleton<IPluginDiscovery>(() =>
            {
                var serializers = registry.ResolveAll<IManifestSerializer>();
                var baseDiscovery = new FileSystemDiscovery(serializers);
                return new ParallelPluginDiscovery(baseDiscovery, maxConcurrentDiscoveries: 8);
            });

            // 注册高性能插件加载器
            registry.RegisterSingleton<IPluginLoader>(() => new HighPerformancePluginLoader(
                registry.Resolve<IPerformanceMonitor>(),
                registry.Resolve<IMemoryManager>(),
                maxConcurrentLoads: 4));

            // 注册验证器
            registry.RegisterSingleton<IPluginValidator>(() =>
            {
                var validator = new PluginValidator();

                // 添加默认验证规则
                if (config.EnableDefaultValidationRules)
                {
                    validator.AddRule(new AssemblyExistsRule());
                    validator.AddRule(new DependencyValidRule());
                    validator.AddRule(new VersionValidRule());
                }

                // 添加自定义验证规则
                foreach (var ruleType in config.ValidationRules)
                {
                    validator.AddRule((IValidationRule)Activator.CreateInstance(ruleType));
                }

                return validator;
            });

            return registry;
        }

        private static PerformanceServices CreatePerformanceServices(PerformanceConfiguration config)
        {
            var services = new PerformanceServices();
            
            if (config.EnablePerformanceMonitoring)
            {
                services.PerformanceMonitor = new PerformanceMonitor();
            }
            
            if (config.EnableMemoryManagement)
            {
                services.MemoryManager = new MemoryManager();
            }
            
            if (config.EnableConcurrencyControl)
            {
                services.ConcurrencyManager = new ConcurrencyManager(services.PerformanceMonitor);
            }
            
            if (config.EnableCaching)
            {
                services.CacheManager = new CacheManager(services.PerformanceMonitor);
            }
            
            return services;
        }
    }

    /// <summary>
    /// 性能服务集合
    /// </summary>
    public class PerformanceServices
    {
        public IPerformanceMonitor PerformanceMonitor { get; set; }
        public IMemoryManager MemoryManager { get; set; }
        public IConcurrencyManager ConcurrencyManager { get; set; }
        public ICacheManager CacheManager { get; set; }
    }
}
