using Astra.Core.Plugins.Abstractions;
using Astra.Core.Plugins.Performance;
using System.Collections.Concurrent;

namespace Astra.Core.Plugins.Caching
{
    /// <summary>
    /// 缓存管理器接口
    /// </summary>
    public interface ICacheManager
    {
        Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, CacheOptions options = null);
        Task SetAsync<T>(string key, T value, CacheOptions options = null);
        Task<T> GetAsync<T>(string key);
        Task<bool> RemoveAsync(string key);
        Task ClearAsync();
        Task<CacheReport> GetCacheReportAsync();
        void SetDefaultOptions(CacheOptions options);
    }

    /// <summary>
    /// 缓存选项
    /// </summary>
    public class CacheOptions
    {
        public TimeSpan Expiration { get; set; } = TimeSpan.FromMinutes(30);
        public TimeSpan? SlidingExpiration { get; set; }
        public CachePriority Priority { get; set; } = CachePriority.Normal;
        public bool EnableCompression { get; set; } = false;
        public int MaxSize { get; set; } = 1000;
        public CacheEvictionPolicy EvictionPolicy { get; set; } = CacheEvictionPolicy.LRU;
    }

    /// <summary>
    /// 缓存优先级
    /// </summary>
    public enum CachePriority
    {
        Low = 0,
        Normal = 1,
        High = 2,
        Critical = 3
    }

    /// <summary>
    /// 缓存驱逐策略
    /// </summary>
    public enum CacheEvictionPolicy
    {
        LRU,        // 最近最少使用
        LFU,        // 最少使用频率
        FIFO,       // 先进先出
        TTL         // 基于过期时间
    }

    /// <summary>
    /// 缓存报告
    /// </summary>
    public class CacheReport
    {
        public DateTime GeneratedAt { get; set; }
        public int TotalItems { get; set; }
        public long TotalSize { get; set; }
        public int HitCount { get; set; }
        public int MissCount { get; set; }
        public double HitRatio { get; set; }
        public Dictionary<CachePriority, int> ItemsByPriority { get; set; } = new();
        public List<CacheItemInfo> TopItems { get; set; } = new();
    }

    /// <summary>
    /// 缓存项信息
    /// </summary>
    public class CacheItemInfo
    {
        public string Key { get; set; }
        public string Type { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime LastAccessed { get; set; }
        public int AccessCount { get; set; }
        public long Size { get; set; }
        public CachePriority Priority { get; set; }
        public TimeSpan? Expiration { get; set; }
    }

    /// <summary>
    /// 缓存项
    /// </summary>
    internal class CacheItem
    {
        public object Value { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime LastAccessed { get; set; }
        public DateTime ExpiresAt { get; set; }
        public TimeSpan? SlidingExpiration { get; set; }
        public int AccessCount { get; set; }
        public CachePriority Priority { get; set; }
        public long Size { get; set; }
        public string Type { get; set; }

        public bool IsExpired => DateTime.UtcNow > ExpiresAt;
        
        public void UpdateAccess()
        {
            LastAccessed = DateTime.UtcNow;
            AccessCount++;
            
            if (SlidingExpiration.HasValue)
            {
                ExpiresAt = DateTime.UtcNow.Add(SlidingExpiration.Value);
            }
        }
    }

    /// <summary>
    /// 缓存管理器实现
    /// </summary>
    public class CacheManager : ICacheManager, IDisposable
    {
        private readonly ConcurrentDictionary<string, CacheItem> _cache = new();
        private readonly Timer _cleanupTimer;
        private readonly IPerformanceMonitor _performanceMonitor;
        private readonly object _lock = new object();
        private CacheOptions _defaultOptions = new CacheOptions();
        private int _hitCount = 0;
        private int _missCount = 0;
        private bool _disposed = false;

        public CacheManager(IPerformanceMonitor performanceMonitor = null)
        {
            _performanceMonitor = performanceMonitor ?? new PerformanceMonitor();
            
            // 每5分钟执行一次清理
            _cleanupTimer = new Timer(PerformCleanup, null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
        }

        /// <summary>
        /// 从缓存获取键值，未命中则执行工厂生成并存入缓存。
        /// </summary>
        public async Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, CacheOptions options = null)
        {
            options ??= _defaultOptions;
            
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            try
            {
                // 尝试从缓存获取
                if (_cache.TryGetValue(key, out var cachedItem) && !cachedItem.IsExpired)
                {
                    cachedItem.UpdateAccess();
                    Interlocked.Increment(ref _hitCount);
                    
                    stopwatch.Stop();
                    _performanceMonitor.RecordOperation($"Cache_Hit_{key}", stopwatch.Elapsed);
                    
                    return (T)cachedItem.Value;
                }

                // 缓存未命中，执行工厂方法
                Interlocked.Increment(ref _missCount);
                var value = await factory();
                
                // 存储到缓存
                await SetAsync(key, value, options);
                
                stopwatch.Stop();
                _performanceMonitor.RecordOperation($"Cache_Miss_{key}", stopwatch.Elapsed);
                
                return value;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _performanceMonitor.RecordOperation($"Cache_Error_{key}", stopwatch.Elapsed);
                throw;
            }
        }

        /// <summary>
        /// 设置缓存值并应用过期与驱逐策略。
        /// </summary>
        public async Task SetAsync<T>(string key, T value, CacheOptions options = null)
        {
            options ??= _defaultOptions;
            
            await Task.Run(() =>
            {
                var cacheItem = new CacheItem
                {
                    Value = value,
                    CreatedAt = DateTime.UtcNow,
                    LastAccessed = DateTime.UtcNow,
                    ExpiresAt = DateTime.UtcNow.Add(options.Expiration),
                    SlidingExpiration = options.SlidingExpiration,
                    AccessCount = 1,
                    Priority = options.Priority,
                    Size = EstimateSize(value),
                    Type = typeof(T).Name
                };

                _cache.AddOrUpdate(key, cacheItem, (k, existing) => cacheItem);
                
                // 检查缓存大小限制
                EnforceSizeLimit(options.MaxSize, options.EvictionPolicy);
            });
        }

        /// <summary>
        /// 直接尝试获取缓存值（不触发工厂）。
        /// </summary>
        public async Task<T> GetAsync<T>(string key)
        {
            return await Task.Run(() =>
            {
                if (_cache.TryGetValue(key, out var cachedItem) && !cachedItem.IsExpired)
                {
                    cachedItem.UpdateAccess();
                    Interlocked.Increment(ref _hitCount);
                    return (T)cachedItem.Value;
                }

                Interlocked.Increment(ref _missCount);
                return default(T);
            });
        }

        /// <summary>
        /// 移除指定键的缓存项。
        /// </summary>
        public async Task<bool> RemoveAsync(string key)
        {
            return await Task.FromResult(_cache.TryRemove(key, out _));
        }

        /// <summary>
        /// 清空所有缓存。
        /// </summary>
        public async Task ClearAsync()
        {
            await Task.Run(() => _cache.Clear());
        }

        /// <summary>
        /// 获取当前缓存状态报告。
        /// </summary>
        public async Task<CacheReport> GetCacheReportAsync()
        {
            return await Task.Run(() =>
            {
                var report = new CacheReport
                {
                    GeneratedAt = DateTime.UtcNow,
                    TotalItems = _cache.Count,
                    HitCount = _hitCount,
                    MissCount = _missCount,
                    HitRatio = _hitCount + _missCount > 0 ? (double)_hitCount / (_hitCount + _missCount) : 0
                };

                // 按优先级分组
                foreach (var kvp in _cache)
                {
                    var priority = kvp.Value.Priority;
                    report.ItemsByPriority[priority] = report.ItemsByPriority.GetValueOrDefault(priority, 0) + 1;
                    report.TotalSize += kvp.Value.Size;
                }

                // 获取访问次数最多的项目
                report.TopItems = _cache
                    .OrderByDescending(kvp => kvp.Value.AccessCount)
                    .Take(10)
                    .Select(kvp => new CacheItemInfo
                    {
                        Key = kvp.Key,
                        Type = kvp.Value.Type,
                        CreatedAt = kvp.Value.CreatedAt,
                        LastAccessed = kvp.Value.LastAccessed,
                        AccessCount = kvp.Value.AccessCount,
                        Size = kvp.Value.Size,
                        Priority = kvp.Value.Priority,
                        Expiration = kvp.Value.ExpiresAt - DateTime.UtcNow
                    })
                    .ToList();

                return report;
            });
        }

        /// <summary>
        /// 设置默认缓存选项。
        /// </summary>
        public void SetDefaultOptions(CacheOptions options)
        {
            _defaultOptions = options ?? new CacheOptions();
        }

        private void EnforceSizeLimit(int maxSize, CacheEvictionPolicy policy)
        {
            if (_cache.Count <= maxSize) return;

            var itemsToRemove = _cache.Count - maxSize;
            var itemsToEvict = GetItemsToEvict(itemsToRemove, policy);

            foreach (var key in itemsToEvict)
            {
                _cache.TryRemove(key, out _);
            }
        }

        private List<string> GetItemsToEvict(int count, CacheEvictionPolicy policy)
        {
            return policy switch
            {
                CacheEvictionPolicy.LRU => _cache
                    .OrderBy(kvp => kvp.Value.LastAccessed)
                    .Take(count)
                    .Select(kvp => kvp.Key)
                    .ToList(),
                    
                CacheEvictionPolicy.LFU => _cache
                    .OrderBy(kvp => kvp.Value.AccessCount)
                    .Take(count)
                    .Select(kvp => kvp.Key)
                    .ToList(),
                    
                CacheEvictionPolicy.FIFO => _cache
                    .OrderBy(kvp => kvp.Value.CreatedAt)
                    .Take(count)
                    .Select(kvp => kvp.Key)
                    .ToList(),
                    
                CacheEvictionPolicy.TTL => _cache
                    .OrderBy(kvp => kvp.Value.ExpiresAt)
                    .Take(count)
                    .Select(kvp => kvp.Key)
                    .ToList(),
                    
                _ => new List<string>()
            };
        }

        private long EstimateSize(object value)
        {
            if (value == null) return 0;

            try
            {
                // 简单的大小估算
                var type = value.GetType();
                if (type.IsPrimitive)
                {
                    return GetPrimitiveSize(type);
                }
                
                if (value is string str)
                {
                    return str.Length * 2; // Unicode
                }
                
                if (value is Array array)
                {
                    return array.Length * IntPtr.Size;
                }
                
                // 默认估算
                return 1024;
            }
            catch
            {
                return 1024;
            }
        }

        private long GetPrimitiveSize(Type type)
        {
            if (type == typeof(bool)) return 1;
            if (type == typeof(byte)) return 1;
            if (type == typeof(sbyte)) return 1;
            if (type == typeof(short)) return 2;
            if (type == typeof(ushort)) return 2;
            if (type == typeof(int)) return 4;
            if (type == typeof(uint)) return 4;
            if (type == typeof(long)) return 8;
            if (type == typeof(ulong)) return 8;
            if (type == typeof(float)) return 4;
            if (type == typeof(double)) return 8;
            if (type == typeof(decimal)) return 16;
            if (type == typeof(char)) return 2;
            return IntPtr.Size;
        }

        private void PerformCleanup(object state)
        {
            if (_disposed) return;

            var expiredKeys = _cache
                .Where(kvp => kvp.Value.IsExpired)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in expiredKeys)
            {
                _cache.TryRemove(key, out _);
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                _cleanupTimer?.Dispose();
                _cache.Clear();
            }
        }
    }

    /// <summary>
    /// 插件缓存装饰器
    /// </summary>
    public class CachedPluginHost : IPluginHost
    {
        private readonly IPluginHost _baseHost;
        private readonly ICacheManager _cacheManager;

        public CachedPluginHost(IPluginHost baseHost, ICacheManager cacheManager)
        {
            _baseHost = baseHost;
            _cacheManager = cacheManager;
        }

        public IReadOnlyList<IPlugin> LoadedPlugins => _baseHost.LoadedPlugins;

        public async Task<IPlugin> LoadPluginAsync(string path)
        {
            var cacheKey = $"plugin_{path}";
            
            return await _cacheManager.GetOrSetAsync(
                cacheKey,
                () => _baseHost.LoadPluginAsync(path),
                new CacheOptions
                {
                    Expiration = TimeSpan.FromHours(1),
                    Priority = CachePriority.High,
                    MaxSize = 100
                }
            );
        }

        public async Task UnloadPluginAsync(string pluginId)
        {
            await _baseHost.UnloadPluginAsync(pluginId);
            
            // 从缓存中移除
            var cacheKey = $"plugin_{pluginId}";
            await _cacheManager.RemoveAsync(cacheKey);
        }

        public async Task<T> GetServiceAsync<T>() where T : class
        {
            var cacheKey = $"service_{typeof(T).Name}";
            
            return await _cacheManager.GetOrSetAsync(
                cacheKey,
                () => _baseHost.GetServiceAsync<T>(),
                new CacheOptions
                {
                    Expiration = TimeSpan.FromMinutes(30),
                    Priority = CachePriority.Normal,
                    MaxSize = 500
                }
            );
        }

        public async Task DiscoverAndLoadPluginsAsync(string pluginDirectory)
        {
            // 发现和加载插件不需要缓存，直接委托给基础宿主
            await _baseHost.DiscoverAndLoadPluginsAsync(pluginDirectory);
        }
    }
}
