using System;
using System.Linq;
using Astra.Core.Plugins.Configuration;
using Astra.Core.Plugins.Abstractions;
using Astra.Core.Plugins.Discovery;
using Astra.Core.Plugins.Lifecycle;
using Astra.Core.Plugins.Exceptions;
using Astra.Core.Plugins.Health;
using Astra.Core.Plugins.Management;
using Astra.Core.Plugins.Manifest.Serializers;
using Astra.Core.Plugins.Messaging;
using Astra.Core.Plugins.Recovery;
using Astra.Core.Plugins.Security;
using Astra.Core.Plugins.Services;
using Astra.Core.Plugins.Validation;
using Astra.Core.Plugins.Performance;
using Astra.Core.Plugins.Memory;
using Astra.Core.Plugins.Loading;
using Astra.Core.Plugins.Concurrency;
using Astra.Core.Plugins.Caching;
using Microsoft.Extensions.DependencyInjection;
using Astra.Core.Plugins.Services.Adapters;

namespace Astra.Core.Plugins.Host
{
    /// <summary>
    /// 插件宿主工厂
    /// </summary>
    public static class PluginHostFactory
    {
        /// <summary>
        /// 创建默认宿主
        /// </summary>
        /// <summary>
        /// 使用内部 <see cref="ServiceRegistry"/> 创建默认插件宿主，并按配置装配性能/安全组件。
        /// </summary>
        /// <param name="config">宿主配置</param>
        /// <returns>已构建的宿主实例</returns>
        public static IPluginHost CreateDefaultHost(HostConfiguration config)
        {
            var serviceRegistry = CreateServiceRegistry(config.Services);
			var performanceServices = CreatePerformanceServices(config.Performance);
			ServiceLocator.Initialize(serviceRegistry);
            
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
            
            // 应用性能优化装饰器（配置驱动）
            IPluginHost host = baseHost;
            if (config.Performance.EnableConcurrencyControl && performanceServices.ConcurrencyManager != null)
            {
                host = new ConcurrencyControlledPluginHost(host, performanceServices.ConcurrencyManager);
            }
            if (config.Performance.EnableCaching && performanceServices.CacheManager != null)
            {
                host = new CachedPluginHost(host, performanceServices.CacheManager);
            }
            
            return host;
        }

		/// <summary>
		/// 使用 Microsoft.Extensions.DependencyInjection 的 <see cref="IServiceCollection"/> 创建默认宿主。
		/// 通过 <see cref="ServiceCollectionAdapter"/> 适配到本框架的 <see cref="IServiceRegistry"/>，
		/// 并在未显式注册的情况下补齐必要的核心服务（Logging/Config/Discovery/Loading 等）。
		/// </summary>
		/// <param name="services">服务集合</param>
		/// <param name="config">宿主配置</param>
		/// <param name="externalServiceProvider">可选的外部 ServiceProvider，如果提供，将确保单例服务共享</param>
		public static IPluginHost CreateDefaultHost(IServiceCollection services, HostConfiguration config, IServiceProvider externalServiceProvider = null)
		{
			// ⭐ 如果提供了外部 ServiceProvider，使用它创建适配器（确保单例共享）
			// 否则，适配器会构建自己的 ServiceProvider
			var registry = externalServiceProvider != null
				? new ServiceCollectionAdapter(services, externalServiceProvider)
				: new ServiceCollectionAdapter(services);

			// 确保必要基础服务存在（若外部未注册，则补齐）
			// 注意：在 IServiceCollection 路径下，IServiceScopeFactory 由微软 DI 提供，这里不再强行注册自定义实现
			if (registry.TryResolve<Microsoft.Extensions.Configuration.IConfiguration>() == null)
			{
				var configurationRoot = ConfigurationProviderRegistration.BuildDefaultConfiguration();
				registry.RegisterSingleton<Microsoft.Extensions.Configuration.IConfiguration>(configurationRoot);
			}
			if (registry.TryResolve<IMessageBus>() == null) registry.RegisterSingleton<IMessageBus, MessageBus>();
			if (registry.TryResolve<IPermissionManager>() == null) registry.RegisterSingleton<IPermissionManager, PermissionManager>();
			if (registry.TryResolve<IConfigurationStore>() == null) registry.RegisterSingleton<IConfigurationStore>(new ConfigurationStore());
			if (registry.TryResolve<IErrorLogger>() == null) registry.RegisterSingleton<IErrorLogger>(new FileErrorLogger("plugin-system.log"));
			if (registry.TryResolve<IExceptionHandler>() == null)
			{
				var eh = new ExceptionHandler(registry.Resolve<IErrorLogger>());
				registry.RegisterSingleton<IExceptionHandler>(eh);
			}
			if (registry.TryResolve<IHealthCheckService>() == null)
			{
				var h = new HealthCheckService(registry.Resolve<IErrorLogger>());
				registry.RegisterSingleton<IHealthCheckService>(h);
			}
			if (registry.TryResolve<ISelfHealingService>() == null)
			{
				var sh = new SelfHealingService(registry.Resolve<IErrorLogger>(), registry.Resolve<IHealthCheckService>());
				registry.RegisterSingleton<ISelfHealingService>(sh);
			}
			if (registry.TryResolve<IPluginDiscovery>() == null)
			{
				registry.RegisterSingleton<IManifestSerializer>(new XmlManifestSerializer());
				registry.RegisterSingleton<IManifestSerializer>(new JsonManifestSerializer());
				registry.RegisterSingleton<IManifestSerializer>(new YamlManifestSerializer());

				var serializers = registry.ResolveAll<IManifestSerializer>();
				var baseDiscovery = new FileSystemDiscovery(serializers);
				var discovery = new ParallelPluginDiscovery(baseDiscovery, maxConcurrentDiscoveries: Math.Max(4, config.Performance.MaxConcurrentDiscoveries));
				registry.RegisterSingleton<IPluginDiscovery>(discovery);
			}
			if (registry.TryResolve<IPluginLoader>() == null)
			{
				var loader = new HighPerformancePluginLoader(
					registry.TryResolve<IPerformanceMonitor>(),
					registry.TryResolve<IMemoryManager>(),
					maxConcurrentLoads: Math.Max(2, config.Performance.MaxConcurrentLoads));
				registry.RegisterSingleton<IPluginLoader>(loader);
			}
			
			// 注册验证器（默认规则 + 自定义扩展）
			if (registry.TryResolve<IPluginValidator>() == null)
			{
				var validator = new PluginValidator();

				// 默认验证规则（这些类在 Astra.Core.Plugins.Validation 命名空间中）
				validator.AddRule(new AssemblyExistsRule());
				validator.AddRule(new DependencyValidRule());
				validator.AddRule(new VersionValidRule());

				// 自定义扩展规则
				if (config.Services.EnableDefaultValidationRules)
				{
					foreach (var ruleType in config.Services.ValidationRules)
					{
						validator.AddRule((IValidationRule)Activator.CreateInstance(ruleType));
					}
				}

				registry.RegisterSingleton<IPluginValidator>(validator);
			}

			var perf = CreatePerformanceServices(config.Performance);
			
			// 注册性能服务（如果已创建）
			// 注意：即使未启用性能监控，也注册一个默认实例，避免解析失败
			if (registry.TryResolve<IPerformanceMonitor>() == null)
			{
				if (perf.PerformanceMonitor != null)
				{
					registry.RegisterSingleton<IPerformanceMonitor>(perf.PerformanceMonitor);
				}
				else
				{
					// 注册一个默认的性能监控器（即使未启用，也提供空实现以避免解析失败）
					registry.RegisterSingleton<IPerformanceMonitor>(new PerformanceMonitor());
				}
			}
			if (perf.MemoryManager != null && registry.TryResolve<IMemoryManager>() == null)
			{
				registry.RegisterSingleton<IMemoryManager>(perf.MemoryManager);
			}
			if (perf.ConcurrencyManager != null && registry.TryResolve<IConcurrencyManager>() == null)
			{
				registry.RegisterSingleton<IConcurrencyManager>(perf.ConcurrencyManager);
			}
			if (perf.CacheManager != null && registry.TryResolve<ICacheManager>() == null)
			{
				registry.RegisterSingleton<ICacheManager>(perf.CacheManager);
			}
			
			ServiceLocator.Initialize(registry);

			// 根据 Host 安全配置补充验证规则（签名与白名单）
			try
			{
				var validator = registry.Resolve<IPluginValidator>();
				if (config.Security.RequireSignature)
				{
					validator.AddRule(new Validation.ValidationRules.SignatureValidator(requireSignature: true));
				}
				if (config.Security.Whitelist != null && config.Security.Whitelist.Count > 0)
				{
					validator.AddRule(new Validation.ValidationRules.WhitelistRule(config.Security.Whitelist));
				}
			}
			catch { }

			var baseHost = new PluginHost(
				registry.Resolve<IPluginDiscovery>(),
				registry,
				registry.Resolve<IMessageBus>(),
				registry.Resolve<IPermissionManager>(),
				registry.Resolve<IPluginValidator>(),
				registry.Resolve<IExceptionHandler>(),
				registry.Resolve<IErrorLogger>(),
				registry.Resolve<IHealthCheckService>(),
				registry.Resolve<ISelfHealingService>()
			);

			IPluginHost host = baseHost;
			if (config.Performance.EnableConcurrencyControl && perf.ConcurrencyManager != null)
			{
				host = new ConcurrencyControlledPluginHost(host, perf.ConcurrencyManager);
			}
			if (config.Performance.EnableCaching && perf.CacheManager != null)
			{
				host = new CachedPluginHost(host, perf.CacheManager);
			}

			return host;
		}

        /// <summary>
        /// 创建高性能宿主
        /// </summary>
        /// <summary>
        /// 预置开启所有性能增强（性能监控/内存管理/并发控制/缓存）的宿主工厂方法。
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
        /// <summary>
        /// 预置安全加固（签名校验/沙箱）与指标的宿主工厂方法。
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
        /// <summary>
        /// 轻量模式宿主工厂方法：不加载性能与沙箱组件，适合开发/测试或资源受限场景。
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

        /// <summary>
        /// 构建带有核心服务注册的 <see cref="ServiceRegistry"/>。
        /// 负责注册：<c>IConfiguration</c>、<c>IServiceScopeFactory</c>、日志/异常处理、健康检查、发现/加载、验证等。
        /// </summary>
        private static ServiceRegistry CreateServiceRegistry(ServiceConfiguration config)
        {
            var registry = new ServiceRegistry();
            
            // 注册核心服务
            registry.RegisterSingleton<IServiceRegistry>(registry);
            registry.RegisterSingleton<IServiceScopeFactory>(new RegistryServiceScopeFactory(registry));
            // 注册 IConfiguration（支持 JSON 文件热更新）
            var configurationRoot = ConfigurationProviderRegistration.BuildDefaultConfiguration();
            registry.RegisterSingleton<Microsoft.Extensions.Configuration.IConfiguration>(configurationRoot);
            // 注册开放泛型 IOptionsMonitor<T> 基于 IConfiguration 的实现
            registry.RegisterOpenGeneric(typeof(Microsoft.Extensions.Options.IOptionsMonitor<>), typeof(Astra.Core.Plugins.Configuration.ConfigurationOptionsMonitor<>), Services.ServiceLifetime.Singleton);
            registry.RegisterSingleton<IMessageBus, MessageBus>();
            registry.RegisterSingleton<IPermissionManager, PermissionManager>();
			registry.RegisterSingleton<Security.IPermissionGateway>(() => new Security.PermissionGateway(
				registry.Resolve<IPermissionManager>(), registry.Resolve<IErrorLogger>()));
			registry.RegisterSingleton<Security.ISecurityAuditLogger>(() => new Security.SecurityAuditLogger(registry.Resolve<IErrorLogger>()));
			registry.RegisterSingleton<Security.ISecureFileSystem>(() => new Security.SecureFileSystem(
				registry.Resolve<Security.IPermissionGateway>(),
				registry.Resolve<Security.ISecurityAuditLogger>()));
			registry.RegisterSingleton<Security.ISecureHttpClientFactory>(() => new Security.SecureHttpClientFactory(
				registry.Resolve<Security.IPermissionGateway>(),
				registry.Resolve<Security.ISecurityAuditLogger>()));
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

			// 注册验证器（默认规则 + 自定义扩展）
			registry.RegisterSingleton<IPluginValidator>(() =>
			{
				var validator = new PluginValidator();

				// 默认
				validator.AddRule(new AssemblyExistsRule());
				validator.AddRule(new DependencyValidRule());
				validator.AddRule(new VersionValidRule());

				// 自定义扩展
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
            
            // 如果启用了并发控制或缓存，需要性能监控器
            if (config.EnablePerformanceMonitoring || config.EnableConcurrencyControl || config.EnableCaching)
            {
                services.PerformanceMonitor = new PerformanceMonitor();
            }
            
            if (config.EnableMemoryManagement)
            {
                services.MemoryManager = new MemoryManager();
            }
            
            if (config.EnableConcurrencyControl)
            {
                // 确保 PerformanceMonitor 已创建
                if (services.PerformanceMonitor == null)
                {
                    services.PerformanceMonitor = new PerformanceMonitor();
                }
                services.ConcurrencyManager = new ConcurrencyManager(services.PerformanceMonitor);
            }
            
            if (config.EnableCaching)
            {
                // 确保 PerformanceMonitor 已创建
                if (services.PerformanceMonitor == null)
                {
                    services.PerformanceMonitor = new PerformanceMonitor();
                }
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
