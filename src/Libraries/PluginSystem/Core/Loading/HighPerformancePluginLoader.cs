using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Addins.Core.Abstractions;
using Addins.Core.Discovery;
using Addins.Core.Loading;
using Addins.Core.Models;
using Addins.Dependencies;
using Addins.Performance;
using Addins.Memory;
using Addins.Exceptions;

namespace Addins.Core.Loading
{
    /// <summary>
    /// 高性能插件加载器
    /// </summary>
    public class HighPerformancePluginLoader : IPluginLoader
    {
        private readonly ConcurrentDictionary<string, Assembly> _assemblyCache = new();
        private readonly ConcurrentDictionary<string, Type[]> _typeCache = new();
        private readonly ConcurrentDictionary<string, PluginDescriptor> _descriptorCache = new();
        private readonly ConcurrentDictionary<string, PluginLoadContext> _loadContexts = new();
        private readonly SemaphoreSlim _loadingSemaphore;
        private readonly IPerformanceMonitor _performanceMonitor;
        private readonly IMemoryManager _memoryManager;
        private readonly object _lock = new object();

        public HighPerformancePluginLoader(
            IPerformanceMonitor performanceMonitor = null,
            IMemoryManager memoryManager = null,
            int maxConcurrentLoads = 4)
        {
            _performanceMonitor = performanceMonitor ?? new PerformanceMonitor();
            _memoryManager = memoryManager ?? new MemoryManager();
            _loadingSemaphore = new SemaphoreSlim(maxConcurrentLoads, maxConcurrentLoads);
        }

        public async Task<IPlugin> LoadPluginAsync(PluginDescriptor descriptor)
        {
            await _loadingSemaphore.WaitAsync();
            try
            {
                return await LoadPluginInternalAsync(descriptor);
            }
            finally
            {
                _loadingSemaphore.Release();
            }
        }

        public async Task<IPlugin> LoadAsync(PluginDescriptor descriptor)
        {
            return await LoadPluginAsync(descriptor);
        }

        public async Task UnloadAsync(string pluginId)
        {
            await Task.Run(() =>
            {
                // 清理缓存
                ClearCache(pluginId);
                
                // 清理加载上下文
                _loadContexts.TryRemove(pluginId, out _);
                
                // 从内存管理器注销
                _memoryManager.UnregisterPlugin(pluginId);
            });
        }

        public async Task<IPlugin> ReloadAsync(string pluginId)
        {
            // 先卸载
            await UnloadAsync(pluginId);
            
            // 重新加载（需要重新获取descriptor）
            // 这里简化处理，实际应该从某个地方重新获取descriptor
            throw new NotImplementedException("ReloadAsync需要重新获取PluginDescriptor");
        }

        public PluginLoadContext GetLoadContext(string pluginId)
        {
            // 返回缓存的加载上下文或创建新的
            if (_loadContexts.TryGetValue(pluginId, out var context))
            {
                return context;
            }
            
            return new PluginLoadContext(pluginId);
        }

        private async Task<IPlugin> LoadPluginInternalAsync(PluginDescriptor descriptor)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            try
            {
                // 检查缓存
                if (_descriptorCache.TryGetValue(descriptor.Id, out var cachedDescriptor))
                {
                    if (IsDescriptorUpToDate(cachedDescriptor, descriptor))
                    {
                        return await LoadFromCacheAsync(descriptor);
                    }
                }

                // 并行加载组件
                var loadTasks = new List<Task>
                {
                    LoadAssemblyAsync(descriptor),
                    LoadDependenciesAsync(descriptor),
                    ValidatePluginAsync(descriptor)
                };

                await Task.WhenAll(loadTasks);

                // 创建插件实例
                var plugin = await CreatePluginInstanceAsync(descriptor);
                
                // 更新缓存
                _descriptorCache[descriptor.Id] = descriptor;
                
                // 保存加载上下文
                var loadContext = new PluginLoadContext(descriptor.Id);
                _loadContexts[descriptor.Id] = loadContext;
                
                stopwatch.Stop();
                _performanceMonitor.RecordOperation($"LoadPlugin_{descriptor.Id}", stopwatch.Elapsed);
                
                return plugin;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _performanceMonitor.RecordOperation($"LoadPlugin_Failed_{descriptor.Id}", stopwatch.Elapsed);
                throw new PluginLoadException($"Failed to load plugin {descriptor.Id}", descriptor.Id, descriptor.AssemblyPath, descriptor.TypeName, ex);
            }
        }

        private async Task<Assembly> LoadAssemblyAsync(PluginDescriptor descriptor)
        {
            return await Task.Run(() =>
            {
                // 检查程序集缓存
                if (_assemblyCache.TryGetValue(descriptor.AssemblyPath, out var cachedAssembly))
                {
                    return cachedAssembly;
                }

                // 加载程序集
                var assembly = Assembly.LoadFrom(descriptor.AssemblyPath);
                _assemblyCache[descriptor.AssemblyPath] = assembly;
                
                // 注册到内存管理器
                _memoryManager.RegisterPlugin(descriptor.Id, new WeakReference(assembly));
                
                return assembly;
            });
        }

        private async Task LoadDependenciesAsync(PluginDescriptor descriptor)
        {
            await Task.Run(() =>
            {
                if (descriptor.Dependencies?.Any() == true)
                {
                    // 并行加载依赖
                    var dependencyTasks = descriptor.Dependencies.Select(dep => 
                        Task.Run(() => LoadDependencyAsync(dep))).ToArray();
                    
                    Task.WaitAll(dependencyTasks);
                }
            });
        }

        private void LoadDependencyAsync(DependencyInfo dependency)
        {
            try
            {
                // 检查依赖是否已加载
                var loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies();
                var existingAssembly = loadedAssemblies.FirstOrDefault(a => 
                    a.GetName().Name == dependency.PluginId && 
                    dependency.VersionRange?.IsInRange(a.GetName().Version) == true);

                if (existingAssembly == null)
                {
                    // 加载依赖程序集
                    var dependencyPath = FindDependencyPath(dependency);
                    if (!string.IsNullOrEmpty(dependencyPath))
                    {
                        Assembly.LoadFrom(dependencyPath);
                    }
                }
            }
            catch (Exception ex)
            {
                throw new PluginDependencyException($"Failed to load dependency {dependency.PluginId}", dependency.PluginId, dependency.PluginId, null, ex);
            }
        }

        private async Task ValidatePluginAsync(PluginDescriptor descriptor)
        {
            await Task.Run(() =>
            {
                // 验证程序集文件存在
                if (!File.Exists(descriptor.AssemblyPath))
                {
                    throw new PluginValidationException($"Assembly file not found: {descriptor.AssemblyPath}", descriptor.Id, new List<string> { "Assembly file not found" });
                }

                // 验证类型存在
                var assembly = _assemblyCache.GetValueOrDefault(descriptor.AssemblyPath);
                if (assembly != null)
                {
                    var pluginType = assembly.GetType(descriptor.TypeName);
                    if (pluginType == null)
                    {
                        throw new PluginValidationException($"Plugin type not found: {descriptor.TypeName}", descriptor.Id, new List<string> { "Plugin type not found" });
                    }

                    if (!typeof(IPlugin).IsAssignableFrom(pluginType))
                    {
                        throw new PluginValidationException($"Type does not implement IPlugin: {descriptor.TypeName}", descriptor.Id, new List<string> { "Type does not implement IPlugin" });
                    }
                }
            });
        }

        private async Task<IPlugin> CreatePluginInstanceAsync(PluginDescriptor descriptor)
        {
            return await Task.Run(() =>
            {
                var assembly = _assemblyCache[descriptor.AssemblyPath];
                var pluginType = assembly.GetType(descriptor.TypeName);
                
                if (pluginType == null)
                {
                    throw new PluginLoadException($"Plugin type not found: {descriptor.TypeName}", descriptor.Id, descriptor.AssemblyPath, descriptor.TypeName);
                }

                var instance = Activator.CreateInstance(pluginType) as IPlugin;
                if (instance == null)
                {
                    throw new PluginLoadException($"Failed to create plugin instance", descriptor.Id, descriptor.AssemblyPath, descriptor.TypeName);
                }

                return instance;
            });
        }

        private async Task<IPlugin> LoadFromCacheAsync(PluginDescriptor descriptor)
        {
            return await Task.Run(() =>
            {
                var assembly = _assemblyCache.GetValueOrDefault(descriptor.AssemblyPath);
                if (assembly == null)
                {
                    throw new PluginLoadException("Cached assembly not found", descriptor.Id, descriptor.AssemblyPath, descriptor.TypeName);
                }

                var pluginType = assembly.GetType(descriptor.TypeName);
                return Activator.CreateInstance(pluginType) as IPlugin;
            });
        }

        private bool IsDescriptorUpToDate(PluginDescriptor cached, PluginDescriptor current)
        {
            return cached.AssemblyPath == current.AssemblyPath &&
                   cached.TypeName == current.TypeName &&
                   cached.Version == current.Version &&
                   File.GetLastWriteTime(cached.AssemblyPath) <= File.GetLastWriteTime(current.AssemblyPath);
        }

        private string FindDependencyPath(DependencyInfo dependency)
        {
            // 在常见路径中查找依赖
            var searchPaths = new[]
            {
                Path.GetDirectoryName(dependency.PluginId),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "plugins"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "lib"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "dependencies")
            };

            foreach (var searchPath in searchPaths)
            {
                if (Directory.Exists(searchPath))
                {
                    var dllPath = Path.Combine(searchPath, $"{dependency.PluginId}.dll");
                    if (File.Exists(dllPath))
                    {
                        return dllPath;
                    }
                }
            }

            return null;
        }

        public void ClearCache()
        {
            _assemblyCache.Clear();
            _typeCache.Clear();
            _descriptorCache.Clear();
            _loadContexts.Clear();
        }

        public void ClearCache(string pluginId)
        {
            var keysToRemove = _assemblyCache.Keys.Where(key => key.Contains(pluginId)).ToList();
            foreach (var key in keysToRemove)
            {
                _assemblyCache.TryRemove(key, out _);
            }

            _descriptorCache.TryRemove(pluginId, out _);
            _loadContexts.TryRemove(pluginId, out _);
        }

        public async Task<LoadingPerformanceReport> GetLoadingPerformanceReportAsync()
        {
            return await Task.Run(() =>
            {
                var report = new LoadingPerformanceReport
                {
                    GeneratedAt = DateTime.UtcNow,
                    CachedAssemblies = _assemblyCache.Count,
                    CachedTypes = _typeCache.Values.Sum(types => types.Length),
                    CachedDescriptors = _descriptorCache.Count,
                    AvailableSemaphoreSlots = _loadingSemaphore.CurrentCount
                };

                return report;
            });
        }

        public void Dispose()
        {
            _loadingSemaphore?.Dispose();
            ClearCache();
        }
    }

    /// <summary>
    /// 加载性能报告
    /// </summary>
    public class LoadingPerformanceReport
    {
        public DateTime GeneratedAt { get; set; }
        public int CachedAssemblies { get; set; }
        public int CachedTypes { get; set; }
        public int CachedDescriptors { get; set; }
        public int AvailableSemaphoreSlots { get; set; }
    }

    /// <summary>
    /// 并行插件发现器
    /// </summary>
    public class ParallelPluginDiscovery : IPluginDiscovery
    {
        private readonly IPluginDiscovery _baseDiscovery;
        private readonly SemaphoreSlim _discoverySemaphore;
        private readonly IPerformanceMonitor _performanceMonitor;

        public ParallelPluginDiscovery(IPluginDiscovery baseDiscovery, int maxConcurrentDiscoveries = 8)
        {
            _baseDiscovery = baseDiscovery;
            _discoverySemaphore = new SemaphoreSlim(maxConcurrentDiscoveries, maxConcurrentDiscoveries);
            _performanceMonitor = new PerformanceMonitor();
        }

        public async Task<IEnumerable<PluginDescriptor>> DiscoverAsync(string path)
        {
            await _discoverySemaphore.WaitAsync();
            try
            {
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                
                try
                {
                    var result = await _baseDiscovery.DiscoverAsync(path);
                    stopwatch.Stop();
                    _performanceMonitor.RecordOperation($"DiscoverPlugins_{path}", stopwatch.Elapsed);
                    
                    return result;
                }
                catch (Exception ex)
                {
                    stopwatch.Stop();
                    _performanceMonitor.RecordOperation($"DiscoverPlugins_Failed_{path}", stopwatch.Elapsed);
                    throw;
                }
            }
            finally
            {
                _discoverySemaphore.Release();
            }
        }

        public void Dispose()
        {
            _discoverySemaphore?.Dispose();
        }
    }
}
