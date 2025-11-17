using Astra.Core.Plugins.Configuration;
using Astra.Core.Plugins.Abstractions;
using Astra.Core.Plugins.Discovery;
using Astra.Core.Plugins.Loading;
using Astra.Core.Plugins.Models;
using Astra.Core.Plugins.Dependencies;
using Astra.Core.Plugins.Exceptions;
using Astra.Core.Plugins.Health;
using Astra.Core.Plugins.Messaging;
using Astra.Core.Plugins.Recovery;
using Astra.Core.Plugins.Security;
using Astra.Core.Plugins.Services;
using Astra.Core.Plugins.Validation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Diagnostics;
using Astra.Core.Plugins.Performance;
using Astra.Core.Plugins.Caching;
using Astra.Core.Plugins.Concurrency;
using System.Security;

namespace Astra.Core.Plugins.Host
{
    /// <summary>
	/// 插件宿主 - 管理插件的发现、验证、加载、启停、健康检查与卸载的中枢。
	/// 负责：
	/// - 与 <see cref="IPluginDiscovery"/> 协作发现插件清单；
	/// - 通过 <see cref="IPluginValidator"/> 验证插件合法性与依赖关系；
	/// - 使用 <see cref="PluginAssemblyLoadContext"/> 隔离加载插件程序集并支持 GC 回收；
	/// - 为每个插件创建独立 <see cref="IServiceScopeFactory"/> 作用域，注入 <see cref="IPluginContext"/>；
	/// - 统一调度插件 <see cref="IPlugin.InitializeAsync"/> / <see cref="IPlugin.OnEnableAsync"/> / <see cref="IPlugin.OnDisable"/> 生命周期；
	/// - 通过 <see cref="IHealthCheckService"/> 注册插件健康检查；
	/// - 利用 <see cref="IErrorLogger"/> 与安全审计器记录信息与异常。
    /// </summary>
    public class PluginHost : IPluginHost
    {
        private readonly IPluginDiscovery _discovery;
        private readonly IServiceRegistry _services;
        private readonly IMessageBus _messageBus;
        private readonly IPermissionManager _permissionManager;
        private readonly IPluginValidator _validator;
        private readonly IExceptionHandler _exceptionHandler;
        private readonly IErrorLogger _logger;
        private readonly IHealthCheckService _healthCheckService;
        private readonly ISelfHealingService _selfHealingService;
        private readonly Dictionary<string, LoadedPlugin> _loadedPlugins = new();
        private readonly Dictionary<string, PluginAssemblyLoadContext> _loadContexts = new();
        private readonly Dictionary<string, IDisposable> _pluginScopes = new();

        public IReadOnlyList<IPlugin> LoadedPlugins =>
            _loadedPlugins.Values.Select(p => p.Instance).ToList();

        public PluginHost(
            IPluginDiscovery discovery,
            IServiceRegistry services,
            IMessageBus messageBus,
            IPermissionManager permissionManager,
            IPluginValidator validator,
            IExceptionHandler exceptionHandler = null,
            IErrorLogger logger = null,
            IHealthCheckService healthCheckService = null,
            ISelfHealingService selfHealingService = null)
        {
            _discovery = discovery;
            _services = services;
            _messageBus = messageBus;
            _permissionManager = permissionManager;
            _validator = validator;
            _exceptionHandler = exceptionHandler ?? new ExceptionHandler();
            _logger = logger ?? new ConsoleErrorLogger();
            _healthCheckService = healthCheckService ?? new HealthCheckService(_logger);
            _selfHealingService = selfHealingService ?? new SelfHealingService(_logger, _healthCheckService);
        }

        /// <summary>
        /// 从指定目录发现插件并按依赖顺序加载。
        /// 包含：发现→验证→依赖图构建→拓扑排序→逐个加载。
        /// </summary>
        /// <param name="pluginDirectory">插件根目录</param>
        public async Task DiscoverAndLoadPluginsAsync(string pluginDirectory)
        {
            // 1. 发现插件（指标埋点）
            var activitySource = new ActivitySource("Astra.Plugins");
            using var activity = activitySource.StartActivity("DiscoverPlugins");
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var descriptors = await _discovery.DiscoverAsync(pluginDirectory);
            stopwatch.Stop();
            try { _services.ResolveOrDefault<IPerformanceMonitor>()?.RecordOperation("discover", stopwatch.Elapsed); } catch { }

            // 2. 验证插件
            var validDescriptors = new List<PluginDescriptor>();
            foreach (var descriptor in descriptors)
            {
                var validationResult = await _validator.ValidateAsync(descriptor);
                if (validationResult.IsValid)
                {
                    validDescriptors.Add(descriptor);
                }
                else
                {
                    Console.WriteLine($"Plugin validation failed: {descriptor.Id}");
                    foreach (var error in validationResult.Errors)
                    {
                        Console.WriteLine($"  - {error}");
                    }
                }
            }

            // 3. 构建依赖图并排序
            var graph = new DependencyGraph();
            foreach (var descriptor in validDescriptors)
            {
                graph.AddPlugin(descriptor);
            }

            foreach (var descriptor in validDescriptors)
            {
                foreach (var dep in descriptor.Dependencies)
                {
                    if (validDescriptors.Any(d => d.Id == dep.PluginId))
                    {
                        graph.AddDependency(descriptor.Id, dep.PluginId);
                    }
                    else if (!dep.IsOptional)
                    {
                        throw new InvalidOperationException(
                            $"Required dependency not found: {dep.PluginId} for plugin {descriptor.Id}");
                    }
                }
            }

            var sortedDescriptors = graph.TopologicalSort();

            // 4. 按依赖顺序加载插件（并发控制优先由装饰器处理，若未启用则串行）
            // 并发控制：如系统未启用装饰器，则在此按配置限制并发
            var concurrency = _services.ResolveOrDefault<IConcurrencyManager>();
            if (concurrency == null)
            {
                foreach (var descriptor in sortedDescriptors)
                {
                    await LoadPluginAsync(descriptor);
                }
            }
            else
            {
                foreach (var descriptor in sortedDescriptors)
                {
                    await concurrency.ExecuteWithConcurrencyControl(
                        () => LoadPluginAsync(descriptor), 
                        operationName: $"load:{descriptor.Id}",
                        config: null);
                }
            }
        }

        /// <summary>
        /// 通过程序集路径定位描述符并加载插件。
        /// </summary>
        /// <param name="path">插件主程序集路径</param>
        /// <returns>已加载的插件实例</returns>
        /// <summary>
        /// 通过主程序集路径直接加载单个插件（将自动查找其清单并执行初始化与启用）。
        /// </summary>
        /// <param name="path">插件主程序集文件的完整路径</param>
        /// <returns>已加载并启用的插件实例</returns>
        public async Task<IPlugin> LoadPluginAsync(string path)
        {
            var descriptors = await _discovery.DiscoverAsync(Path.GetDirectoryName(path));
            var descriptor = descriptors.FirstOrDefault(d => d.AssemblyPath == path);

            if (descriptor == null)
                throw new InvalidOperationException($"Plugin not found: {path}");

            return await LoadPluginAsync(descriptor);
        }

        /// <summary>
        /// 使用给定描述符加载插件：创建可回收加载上下文、实例化、初始化、启用、注册健康检查。
        /// </summary>
        /// <param name="descriptor">插件描述符</param>
        /// <returns>插件实例</returns>
        /// <summary>
        /// 内部：基于 <paramref name="descriptor"/> 执行加载流程（包含初始化与启用）。
        /// </summary>
        /// <param name="descriptor">插件描述符</param>
        /// <returns>插件实例</returns>
        private async Task<IPlugin> LoadPluginAsync(PluginDescriptor descriptor)
        {
            if (_loadedPlugins.ContainsKey(descriptor.Id))
            {
                return _loadedPlugins[descriptor.Id].Instance;
            }

            var config = new ExceptionHandlingConfig
            {
                Strategy = ExceptionHandlingStrategy.Retry,
                MaxRetryAttempts = 3,
                RetryDelay = TimeSpan.FromSeconds(2),
                EnableCircuitBreaker = true
            };

            return await _exceptionHandler.HandleAsync(async () =>
            {
                try
                {
					// 创建隔离的加载上下文（可回收）
					var loadContext = new PluginAssemblyLoadContext(descriptor.AssemblyPath, isCollectible: true);
                    _loadContexts[descriptor.Id] = loadContext;

                    // 加载程序集
                    var swLoadAsm = System.Diagnostics.Stopwatch.StartNew();
                    var assembly = loadContext.LoadFromAssemblyPath(descriptor.AssemblyPath);
                    swLoadAsm.Stop();
                    try { _services.ResolveOrDefault<IPerformanceMonitor>()?.RecordOperation(descriptor.Id, "load_assembly", swLoadAsm.Elapsed); } catch { }
                    var pluginType = assembly.GetType(descriptor.TypeName);

                    if (pluginType == null)
                        throw new PluginLoadException($"Plugin type not found: {descriptor.TypeName}", descriptor.Id, descriptor.AssemblyPath, descriptor.TypeName);

					// 创建插件实例（表达式树工厂）
					var plugin = PluginActivatorFactory.Create(pluginType);
                    if (plugin == null)
                        throw new PluginLoadException($"Type does not implement IPlugin: {descriptor.TypeName}", descriptor.Id, descriptor.AssemblyPath, descriptor.TypeName);

                    // 授予权限（若配置提供默认权限上限，则进行约束）
                    try
                    {
                        var sec = _services.ResolveOrDefault<HostConfiguration>()?.Security;
                        if (sec != null && sec.DefaultPermissions != null && sec.DefaultPermissions.Count > 0)
                        {
                            var allowed = PluginPermissions.None;
                            foreach (var p in sec.DefaultPermissions) allowed |= p;
                            if ((descriptor.Permissions & ~allowed) != 0)
                            {
                                throw new SecurityException($"Plugin requests permissions beyond allowed: {descriptor.Permissions & ~allowed}");
                            }
                        }
                        _permissionManager.GrantPermission(descriptor.Id, descriptor.Permissions);
                        await _logger.LogInfoAsync($"Permissions granted: {descriptor.Permissions}", descriptor.Id);
                    }
                    catch (Exception exPerm)
                    {
                        throw new PluginLoadException(
                            $"Permission grant failed: {Security.SafeExceptionFormatter.Sanitize(exPerm.Message)}",
                            descriptor.Id,
                            descriptor.AssemblyPath,
                            descriptor.TypeName,
                            exPerm);
                    }

					// 创建插件上下文
					var context = new PluginContext
					{
						Services = _services,
						MessageBus = _messageBus,
						EventBus = _messageBus,
						Host = this,
						PluginDirectory = Path.GetDirectoryName(descriptor.AssemblyPath)
					};

					// 填充标准 ServiceProvider 与 Configuration/Logger（尽力而为）
					try { context.ConfigurationRoot = _services.Resolve<IConfiguration>(); } catch { context.ConfigurationRoot = null; }
					try
					{
						// 为插件构造一个可解析的 IServiceProvider 适配层
						context.ServiceProvider = new RegistryAdapterProvider(_services);
					}
					catch
					{
						context.ServiceProvider = null;
					}
					try
					{
						var loggerFactory = context.ServiceProvider?.GetService(typeof(ILoggerFactory)) as ILoggerFactory;
						context.Logger = (ILogger?)loggerFactory?.CreateLogger(descriptor.Name) ?? NullLogger.Instance;
					}
					catch
					{
						context.Logger = NullLogger.Instance;
					}
					try { context.PermissionGateway = _services.ResolveOrDefault<Security.IPermissionGateway>(); } catch { context.PermissionGateway = null; }

                    // 创建插件作用域（用于 Scoped/Transient 资源的受控生命周期）
                    var scopeFactory = _services.Resolve<IServiceScopeFactory>();
                    var scope = scopeFactory?.CreateScope();
                    if (scope != null)
                    {
                        _pluginScopes[descriptor.Id] = scope;
                    }

                    // 初始化插件（在作用域内）
                    descriptor.State = PluginState.Initializing;
                    try
                    {
                        var swInit = System.Diagnostics.Stopwatch.StartNew();
                        await plugin.InitializeAsync(context, CancellationToken.None);
                        swInit.Stop();
                        try { _services.ResolveOrDefault<IPerformanceMonitor>()?.RecordOperation(descriptor.Id, "initialize", swInit.Elapsed); } catch { }
                    }
                    catch (Exception ex)
                    {
                        throw new PluginInitializationException($"Plugin initialization failed: {ex.Message}", descriptor.Id, ex);
                    }

                    descriptor.State = PluginState.Running;
					try
					{
                        var swEnable = System.Diagnostics.Stopwatch.StartNew();
                        await plugin.OnEnableAsync(CancellationToken.None);
                        swEnable.Stop();
                        try { _services.ResolveOrDefault<IPerformanceMonitor>()?.RecordOperation(descriptor.Id, "enable", swEnable.Elapsed); } catch { }
                    }
                    catch (Exception ex)
                    {
                        throw new PluginStartException($"Plugin start failed: {ex.Message}", descriptor.Id, ex);
                    }

                    _loadedPlugins[descriptor.Id] = new LoadedPlugin
                    {
                        Descriptor = descriptor,
                        Instance = plugin,
                        LoadContext = loadContext
                    };

                    // 注册健康检查
                    _healthCheckService.RegisterHealthCheck(new PluginHealthCheck(this, descriptor.Id));

                    await _logger.LogInfoAsync($"Plugin loaded successfully: {descriptor.Name} v{descriptor.Version}", descriptor.Id);
                    return plugin;
                }
                catch (PluginSystemException)
                {
                    throw; // 重新抛出插件系统异常
                }
                catch (Exception ex)
                {
                    throw new PluginLoadException($"Unexpected error loading plugin: {Security.SafeExceptionFormatter.Sanitize(ex.Message)}", descriptor.Id, descriptor.AssemblyPath, descriptor.TypeName, ex);
                }
            }, "LoadPlugin", descriptor.Id, config);
        }

        /// <summary>
        /// 卸载指定插件：调用禁用、释放作用域、Dispose、卸载加载上下文并注销健康检查。
        /// </summary>
        /// <param name="pluginId">插件标识</param>
        public async Task UnloadPluginAsync(string pluginId)
        {
            if (!_loadedPlugins.TryGetValue(pluginId, out var loadedPlugin))
                return;

            var config = new ExceptionHandlingConfig
            {
                Strategy = ExceptionHandlingStrategy.LogAndContinue,
                EnableCircuitBreaker = false
            };

            await _exceptionHandler.HandleAsync(async () =>
            {
                try
                {
                    loadedPlugin.Descriptor.State = PluginState.Stopping;
                    
					try
					{
						await loadedPlugin.Instance.OnDisableAsync(CancellationToken.None);
                    }
                    catch (Exception ex)
                    {
                        throw new PluginStopException($"Plugin stop failed: {ex.Message}", pluginId, ex);
                    }

                    // 释放插件作用域（确保 Scoped/Transient IDisposable 释放）
                    if (_pluginScopes.TryGetValue(pluginId, out var scopeHandle))
                    {
                        try { scopeHandle.Dispose(); } catch { }
                        _pluginScopes.Remove(pluginId);
                    }

                    try
                    {
                        loadedPlugin.Instance.Dispose();
                    }
                    catch (Exception ex)
                    {
                        await _logger.LogWarningAsync($"Plugin dispose failed: {ex.Message}", pluginId);
                    }

                    if (_loadContexts.TryGetValue(pluginId, out var loadContext))
                    {
                        try
                        {
                            loadContext.Unload();
                        }
                        catch (Exception ex)
                        {
                            await _logger.LogWarningAsync($"Load context unload failed: {ex.Message}", pluginId);
                        }
                        _loadContexts.Remove(pluginId);
                    }

                    _loadedPlugins.Remove(pluginId);
                    loadedPlugin.Descriptor.State = PluginState.Unloading;

                    // 取消注册健康检查
                    _healthCheckService.UnregisterHealthCheck($"Plugin-{pluginId}");

                    await _logger.LogInfoAsync($"Plugin unloaded successfully: {pluginId}");
                }
                catch (PluginSystemException)
                {
                    throw; // 重新抛出插件系统异常
                }
                catch (Exception ex)
                {
                    throw new PluginUnloadException($"Unexpected error unloading plugin: {Security.SafeExceptionFormatter.Sanitize(ex.Message)}", pluginId, ex);
                }
            }, "UnloadPlugin", pluginId, config);
        }

        /// <summary>
        /// 从宿主服务注册表解析服务。
        /// </summary>
        public Task<T> GetServiceAsync<T>() where T : class
        {
            return Task.FromResult(_services.Resolve<T>());
        }

        private class LoadedPlugin
        {
			/// <summary>
			/// 插件的静态描述信息（来自清单解析）。
			/// </summary>
            public PluginDescriptor Descriptor { get; set; }
			/// <summary>
			/// 插件运行时实例。
			/// </summary>
            public IPlugin Instance { get; set; }
			/// <summary>
			/// 该插件对应的可回收加载上下文。
			/// </summary>
            public PluginAssemblyLoadContext LoadContext { get; set; }
        }

        private class PluginContext : IPluginContext
        {
			/// <summary>
			/// 宿主服务注册表（兼容存量接口）。
			/// </summary>
            public IServiceRegistry Services { get; set; }
			/// <summary>
			/// 消息总线（插件间通信）。
			/// </summary>
            public IMessageBus MessageBus { get; set; }
			/// <summary>
			/// 简易配置存储（与 IConfiguration 并存）。
			/// </summary>
            public IConfigurationStore Configuration { get; set; }
			/// <summary>
			/// 标准 .NET 服务提供器（用于 ILogger/IOptions 等）。
			/// </summary>
			public IServiceProvider ServiceProvider { get; set; }
			/// <summary>
			/// 标准配置根（支持热重载）。
			/// </summary>
			public IConfiguration ConfigurationRoot { get; set; }
			/// <summary>
			/// 日志实例（按插件命名）。
			/// </summary>
			public ILogger Logger { get; set; }
			/// <summary>
			/// 事件总线别名（与 MessageBus 等价）。
			/// </summary>
			public IMessageBus EventBus { get; set; }
			/// <summary>
			/// 插件物理目录路径。
			/// </summary>
			public string PluginDirectory { get; set; }
			/// <summary>
			/// 宿主接口。
			/// </summary>
            public IPluginHost Host { get; set; }
			/// <summary>
			/// 权限网关（敏感操作统一校验入口）。
			/// </summary>
			public Security.IPermissionGateway PermissionGateway { get; set; }
        }

		private sealed class RegistryAdapterProvider : IServiceProvider
		{
			private readonly IServiceRegistry _registry;
			/// <summary>
			/// 使用 <see cref="IServiceRegistry"/> 作为后端实现的 <see cref="IServiceProvider"/> 适配器，
			/// 便于插件按标准方式请求依赖。
			/// </summary>
			public RegistryAdapterProvider(IServiceRegistry registry) { _registry = registry; }
			/// <summary>
			/// 解析服务，若未注册则返回 null。
			/// </summary>
			public object GetService(Type serviceType)
			{
				try { return _registry.Resolve(serviceType); } catch { return null; }
			}
		}
    }
}
