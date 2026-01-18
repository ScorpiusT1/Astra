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
    public partial class PluginHost : IPluginHost
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
        private readonly Dictionary<string, PluginLoaded> _loadedPlugins = new();
        private readonly Dictionary<string, PluginAssemblyLoadContext> _loadContexts = new();
        private readonly Dictionary<string, IDisposable> _pluginScopes = new();
        private IServiceProvider _sharedServiceProvider; // ⭐ 全局共享的 ServiceProvider（非 readonly，支持后续更新）
        
        // ⚠️ 性能优化：缓存默认上下文中的程序集查找结果，避免重复遍历
        // Key: 程序集路径（规范化后的完整路径），Value: (程序集实例, 文件最后修改时间)
        private static readonly Dictionary<string, (System.Reflection.Assembly Assembly, DateTime LastWriteTime)> _defaultContextAssemblyCache = new(StringComparer.OrdinalIgnoreCase);
        private static readonly object _cacheLock = new object();
        
        // 静态构造函数：初始化缓存失效监听机制
        static PluginHost()
        {
            InitializeCacheInvalidationMechanism();
        }

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
            
            // ⭐ 初始化全局共享的 ServiceProvider
            // 如果 _services 是 ServiceCollectionAdapter，尝试获取其内部的 ServiceProvider
            // 否则，创建一个 RegistryAdapterProvider 作为共享实例
            _sharedServiceProvider = CreateSharedServiceProvider(services);
        }
        
        /// <summary>
        /// 创建全局共享的 ServiceProvider
        /// ⭐ 确保所有插件使用同一个 ServiceProvider 实例，保证单例服务的一致性
        /// </summary>
        private IServiceProvider CreateSharedServiceProvider(IServiceRegistry services)
        {
            // 如果 services 是 ServiceCollectionAdapter，使用其 GetServiceProvider() 方法获取共享的 ServiceProvider
            // 这样可以确保插件系统和主应用使用同一个 ServiceProvider 实例
            if (services is Astra.Core.Plugins.Services.Adapters.ServiceCollectionAdapter adapter)
            {
                return adapter.GetServiceProvider();
            }
            
            // 回退方案：创建 RegistryAdapterProvider 作为共享实例
            return new RegistryAdapterProvider(services);
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
                    // ⚠️ 关键修复：检查程序集是否已在默认上下文中加载（例如在 ApplicationBootstrapper 阶段1中）
                    // 如果已在默认上下文中，复用该程序集，避免重复加载导致的 WPF 资源解析问题
                    var (assembly, loadContext) = await LoadOrReuseAssemblyAsync(descriptor);
                    
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
					
					// ⭐ 使用全局共享的 ServiceProvider，确保所有插件使用同一个实例
					// 这样可以保证单例服务的一致性（如 ILoggerFactory、IOptions 等）
					context.ServiceProvider = _sharedServiceProvider;

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

                    _loadedPlugins[descriptor.Id] = new PluginLoaded
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

                    // ⚠️ 缓存失效：如果插件使用的是默认上下文中的程序集，从缓存中移除
                    // 注意：由于默认上下文中的程序集通常不会被卸载，这里主要是清理缓存以支持重新加载
                    var pluginAssemblyPath = loadedPlugin.Descriptor.AssemblyPath;
                    if (!string.IsNullOrEmpty(pluginAssemblyPath))
                    {
                        ClearAssemblyCache(pluginAssemblyPath);
                        await _logger.LogInfoAsync($"已清理程序集缓存: {pluginAssemblyPath}", pluginId);
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

        /// <summary>
        /// 更新 ServiceCollectionAdapter 的外部 ServiceProvider
        /// ⭐ 用于在主程序构建 ServiceProvider 后，让插件系统使用主程序的全局 ServiceProvider
        /// </summary>
        /// <param name="externalServiceProvider">主程序构建的 ServiceProvider</param>
        public void UpdateExternalServiceProvider(IServiceProvider externalServiceProvider)
        {
            if (externalServiceProvider == null)
            {
                throw new ArgumentNullException(nameof(externalServiceProvider));
            }

            // 如果 _services 是 ServiceCollectionAdapter，更新它的外部 ServiceProvider
            if (_services is Astra.Core.Plugins.Services.Adapters.ServiceCollectionAdapter adapter)
            {
                adapter.SetExternalServiceProvider(externalServiceProvider);
                
                // ⭐ 重新获取 ServiceProvider 并更新 _sharedServiceProvider
                // 这样可以确保 _sharedServiceProvider 指向新的 ServiceProvider 实例
                _sharedServiceProvider = adapter.GetServiceProvider();
            }
        }

        /// <summary>
        /// 加载或复用程序集（性能优化版本）
        /// ⚠️ 关键修复：检查程序集是否已在默认上下文中加载，如果已加载则复用，避免重复加载导致的 WPF 资源解析问题
        /// </summary>
        /// <param name="descriptor">插件描述符</param>
        /// <returns>程序集实例和加载上下文（如果使用隔离上下文则为非 null，如果复用默认上下文则为 null）</returns>
        private async Task<(System.Reflection.Assembly Assembly, PluginAssemblyLoadContext LoadContext)> LoadOrReuseAssemblyAsync(PluginDescriptor descriptor)
        {
            System.Reflection.Assembly assembly = null;
            PluginAssemblyLoadContext loadContext = null;
            
            // ⚠️ 性能优化：使用缓存避免重复遍历所有程序集
            var normalizedPath = System.IO.Path.GetFullPath(descriptor.AssemblyPath);
            
            // 首先检查缓存
            System.Reflection.Assembly defaultContextAssembly = null;
            bool cacheInvalidated = false; // 用于标记是否需要记录缓存失效日志
            
            lock (_cacheLock)
            {
                if (_defaultContextAssemblyCache.TryGetValue(normalizedPath, out var cachedEntry))
                {
                    var cachedAssembly = cachedEntry.Assembly;
                    var cachedLastWriteTime = cachedEntry.LastWriteTime;
                    
                    // 验证缓存的程序集仍然有效
                    if (cachedAssembly != null && !cachedAssembly.IsDynamic && cachedAssembly.Location != null)
                    {
                        // ⚠️ 缓存失效检查：如果文件已被修改，清理缓存
                        if (System.IO.File.Exists(normalizedPath))
                        {
                            var currentLastWriteTime = System.IO.File.GetLastWriteTime(normalizedPath);
                            if (currentLastWriteTime != cachedLastWriteTime)
                            {
                                // 文件已被修改，缓存失效（在 lock 外部记录日志）
                                _defaultContextAssemblyCache.Remove(normalizedPath);
                                cacheInvalidated = true;
                            }
                            else
                            {
                                // 缓存有效
                                defaultContextAssembly = cachedAssembly;
                            }
                        }
                        else
                        {
                            // 文件不存在，缓存无效
                            _defaultContextAssemblyCache.Remove(normalizedPath);
                        }
                    }
                    else
                    {
                        // 程序集引用无效（可能已被卸载），移除缓存
                        _defaultContextAssemblyCache.Remove(normalizedPath);
                    }
                }
            }
            
            // ⚠️ 在 lock 外部执行异步操作（日志记录）
            if (cacheInvalidated)
            {
                await _logger.LogInfoAsync($"程序集文件已更新，缓存失效: {descriptor.Name} ({normalizedPath})", descriptor.Id);
            }
            
            // 如果缓存未命中，遍历默认上下文查找
            if (defaultContextAssembly == null)
            {
                defaultContextAssembly = System.Runtime.Loader.AssemblyLoadContext.Default.Assemblies
                    .FirstOrDefault(a => !a.IsDynamic && 
                                       a.Location != null &&
                                       System.IO.Path.GetFullPath(a.Location).Equals(normalizedPath, StringComparison.OrdinalIgnoreCase));
                
                // 更新缓存（包含文件最后修改时间，用于检测文件更新）
                if (defaultContextAssembly != null && System.IO.File.Exists(normalizedPath))
                {
                    var lastWriteTime = System.IO.File.GetLastWriteTime(normalizedPath);
                    lock (_cacheLock)
                    {
                        _defaultContextAssemblyCache[normalizedPath] = (defaultContextAssembly, lastWriteTime);
                    }
                }
            }
            
            if (defaultContextAssembly != null)
            {
                // ✅ 程序集已在默认上下文中，复用它
                assembly = defaultContextAssembly;
                await _logger.LogInfoAsync($"复用默认上下文中的程序集: {descriptor.Name} ({assembly.GetName().Name})", descriptor.Id);
                
                // ⚠️ 注意：不创建 PluginAssemblyLoadContext，这样可以确保类型和资源都在默认上下文中
                // 这对于 WPF 资源解析至关重要
                // loadContext 保持为 null
            }
            else
            {
                // ⚠️ 程序集未在默认上下文中，使用隔离的加载上下文（可回收）
                loadContext = new PluginAssemblyLoadContext(descriptor.AssemblyPath, isCollectible: true);
                _loadContexts[descriptor.Id] = loadContext;

                // 加载程序集
                var swLoadAsm = System.Diagnostics.Stopwatch.StartNew();
                assembly = loadContext.LoadFromAssemblyPath(descriptor.AssemblyPath);
                swLoadAsm.Stop();
                
                // 记录性能指标
                try 
                { 
                    _services.ResolveOrDefault<IPerformanceMonitor>()?.RecordOperation(descriptor.Id, "load_assembly", swLoadAsm.Elapsed); 
                } 
                catch { }
                
                await _logger.LogInfoAsync($"在隔离上下文中加载程序集: {descriptor.Name} ({assembly.GetName().Name})，耗时: {swLoadAsm.ElapsedMilliseconds}ms", descriptor.Id);
            }
            
            return (assembly, loadContext);
        }

        /// <summary>
        /// 初始化缓存失效机制
        /// 监听程序集卸载事件，自动清理相关缓存
        /// </summary>
        private static void InitializeCacheInvalidationMechanism()
        {
            // ⚠️ 注意：.NET Core/.NET 5+ 中，AssemblyLoadContext 的卸载事件不是直接的 AppDomain 事件
            // 默认上下文中的程序集通常不会被卸载，但我们可以监听文件变化来实现缓存失效
            
            // 由于默认上下文的程序集很少被卸载，我们主要依赖：
            // 1. 插件卸载时的手动清理（在 UnloadPluginAsync 中）
            // 2. 文件修改时间检查（在 LoadOrReuseAssemblyAsync 中）
            // 3. 程序集引用验证（检查 Location 是否为 null）
            
            // 如果需要更主动的缓存失效，可以使用 FileSystemWatcher 监听程序集文件变化
            // 但这可能会带来性能开销，所以暂时不实现
        }

        /// <summary>
        /// 从缓存中清除指定程序集路径的缓存项
        /// </summary>
        /// <param name="assemblyPath">程序集路径（可以是相对或绝对路径）</param>
        public static void ClearAssemblyCache(string assemblyPath)
        {
            if (string.IsNullOrEmpty(assemblyPath))
                return;

            var normalizedPath = System.IO.Path.GetFullPath(assemblyPath);
            
            lock (_cacheLock)
            {
                if (_defaultContextAssemblyCache.Remove(normalizedPath))
                {
                    System.Diagnostics.Debug.WriteLine($"[PluginHost] 已从缓存中移除程序集: {normalizedPath}");
                }
            }
        }

        /// <summary>
        /// 清除所有程序集缓存
        /// ⚠️ 谨慎使用：这会导致所有缓存失效，下次加载插件时会重新遍历程序集列表
        /// </summary>
        public static void ClearAllAssemblyCache()
        {
            lock (_cacheLock)
            {
                var count = _defaultContextAssemblyCache.Count;
                _defaultContextAssemblyCache.Clear();
                System.Diagnostics.Debug.WriteLine($"[PluginHost] 已清除所有程序集缓存（共 {count} 项）");
            }
        }

        /// <summary>
        /// 清理无效的缓存项（程序集已被卸载或文件不存在）
        /// 可以定期调用此方法以清理缓存，避免内存泄漏
        /// </summary>
        public static int CleanupInvalidCacheEntries()
        {
            var removedCount = 0;
            var keysToRemove = new List<string>();
            
            lock (_cacheLock)
            {
                foreach (var kvp in _defaultContextAssemblyCache)
                {
                    var normalizedPath = kvp.Key;
                    var cachedEntry = kvp.Value;
                    var cachedAssembly = cachedEntry.Assembly;
                    
                    // 检查程序集引用是否有效
                    bool isValid = cachedAssembly != null && 
                                   !cachedAssembly.IsDynamic && 
                                   cachedAssembly.Location != null;
                    
                    // 检查文件是否存在
                    if (isValid && !System.IO.File.Exists(normalizedPath))
                    {
                        isValid = false;
                    }
                    
                    if (!isValid)
                    {
                        keysToRemove.Add(normalizedPath);
                    }
                }
                
                foreach (var key in keysToRemove)
                {
                    _defaultContextAssemblyCache.Remove(key);
                    removedCount++;
                }
            }
            
            if (removedCount > 0)
            {
                System.Diagnostics.Debug.WriteLine($"[PluginHost] 已清理 {removedCount} 个无效的缓存项");
            }
            
            return removedCount;
        }
    }
}
