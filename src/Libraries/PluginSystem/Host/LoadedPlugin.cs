using Addins.Configuration;
using Addins.Core.Abstractions;
using Addins.Core.Discovery;
using Addins.Core.Loading;
using Addins.Core.Models;
using Addins.Dependencies;
using Addins.Exceptions;
using Addins.Health;
using Addins.Messaging;
using Addins.Recovery;
using Addins.Security;
using Addins.Services;
using Addins.Validation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Addins.Host
{
    /// <summary>
    /// 插件宿主 - 核心协调器
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
        private readonly Dictionary<string, PluginLoadContext> _loadContexts = new();

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

        public async Task DiscoverAndLoadPluginsAsync(string pluginDirectory)
        {
            // 1. 发现插件
            var descriptors = await _discovery.DiscoverAsync(pluginDirectory);

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

            // 4. 按依赖顺序加载插件
            foreach (var descriptor in sortedDescriptors)
            {
                await LoadPluginAsync(descriptor);
            }
        }

        public async Task<IPlugin> LoadPluginAsync(string path)
        {
            var descriptors = await _discovery.DiscoverAsync(Path.GetDirectoryName(path));
            var descriptor = descriptors.FirstOrDefault(d => d.AssemblyPath == path);

            if (descriptor == null)
                throw new InvalidOperationException($"Plugin not found: {path}");

            return await LoadPluginAsync(descriptor);
        }

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
                    // 创建隔离的加载上下文
                    var loadContext = new PluginLoadContext(descriptor.AssemblyPath, isCollectible: true);
                    _loadContexts[descriptor.Id] = loadContext;

                    // 加载程序集
                    var assembly = loadContext.LoadFromAssemblyPath(descriptor.AssemblyPath);
                    var pluginType = assembly.GetType(descriptor.TypeName);

                    if (pluginType == null)
                        throw new PluginLoadException($"Plugin type not found: {descriptor.TypeName}", descriptor.Id, descriptor.AssemblyPath, descriptor.TypeName);

                    // 创建插件实例
                    var plugin = Activator.CreateInstance(pluginType) as IPlugin;
                    if (plugin == null)
                        throw new PluginLoadException($"Type does not implement IPlugin: {descriptor.TypeName}", descriptor.Id, descriptor.AssemblyPath, descriptor.TypeName);

                    // 授予权限
                    _permissionManager.GrantPermission(descriptor.Id, descriptor.Permissions);

                    // 创建插件上下文
                    var context = new PluginContext
                    {
                        Services = _services,
                        MessageBus = _messageBus,
                        Host = this
                    };

                    // 初始化插件
                    descriptor.State = PluginState.Initializing;
                    try
                    {
                        await plugin.InitializeAsync(context);
                    }
                    catch (Exception ex)
                    {
                        throw new PluginInitializationException($"Plugin initialization failed: {ex.Message}", descriptor.Id, ex);
                    }

                    descriptor.State = PluginState.Running;
                    try
                    {
                        await plugin.StartAsync();
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
                    throw new PluginLoadException($"Unexpected error loading plugin: {ex.Message}", descriptor.Id, descriptor.AssemblyPath, descriptor.TypeName, ex);
                }
            }, "LoadPlugin", descriptor.Id, config);
        }

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
                        await loadedPlugin.Instance.StopAsync();
                    }
                    catch (Exception ex)
                    {
                        throw new PluginStopException($"Plugin stop failed: {ex.Message}", pluginId, ex);
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
                    throw new PluginUnloadException($"Unexpected error unloading plugin: {ex.Message}", pluginId, ex);
                }
            }, "UnloadPlugin", pluginId, config);
        }

        public Task<T> GetServiceAsync<T>() where T : class
        {
            return Task.FromResult(_services.Resolve<T>());
        }

        private class LoadedPlugin
        {
            public PluginDescriptor Descriptor { get; set; }
            public IPlugin Instance { get; set; }
            public PluginLoadContext LoadContext { get; set; }
        }

        private class PluginContext : IPluginContext
        {
            public IServiceRegistry Services { get; set; }
            public IMessageBus MessageBus { get; set; }
            public IConfigurationStore Configuration { get; set; }
            public IPluginHost Host { get; set; }
        }
    }
}
