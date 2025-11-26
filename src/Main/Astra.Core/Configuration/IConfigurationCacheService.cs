using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Astra.Core.Configuration
{
    /// <summary>
    /// 配置缓存服务接口 - 符合单一职责原则（SRP）
    /// 仅负责配置的缓存管理
    /// </summary>
    public interface IConfigurationCacheService
    {
        /// <summary>
        /// 获取缓存的配置
        /// </summary>
        T Get<T>(string configId) where T : class, IConfig;

        /// <summary>
        /// 尝试获取缓存的配置
        /// </summary>
        bool TryGet<T>(string configId, out T config) where T : class, IConfig;

        /// <summary>
        /// 设置配置缓存
        /// </summary>
        void Set<T>(T config) where T : class, IConfig;

        /// <summary>
        /// 设置配置缓存（非泛型版本）
        /// </summary>
        void Set(IConfig config);

        /// <summary>
        /// 移除缓存的配置
        /// </summary>
        bool Remove<T>(string configId) where T : class, IConfig;

        /// <summary>
        /// 移除缓存的配置（非泛型版本）
        /// </summary>
        bool Remove(IConfig config);

        /// <summary>
        /// 清除所有缓存
        /// </summary>
        void Clear();

        /// <summary>
        /// 清除指定类型的缓存
        /// </summary>
        void Clear<T>() where T : class, IConfig;

        /// <summary>
        /// 检查配置是否在缓存中
        /// </summary>
        bool Contains<T>(string configId) where T : class, IConfig;

        /// <summary>
        /// 获取缓存统计信息
        /// </summary>
        CacheStatistics GetStatistics();
    }

    /// <summary>
    /// 缓存统计信息
    /// </summary>
    public class CacheStatistics
    {
        /// <summary>
        /// 缓存项总数
        /// </summary>
        public int TotalCount { get; set; }

        /// <summary>
        /// 缓存命中次数
        /// </summary>
        public long HitCount { get; set; }

        /// <summary>
        /// 缓存未命中次数
        /// </summary>
        public long MissCount { get; set; }

        /// <summary>
        /// 缓存命中率
        /// </summary>
        public double HitRate => HitCount + MissCount == 0 ? 0 : (double)HitCount / (HitCount + MissCount);
    }

    /// <summary>
    /// 配置缓存服务实现 - 使用线程安全的ConcurrentDictionary
    /// 符合单一职责原则，仅负责缓存操作
    /// </summary>
    public class ConfigurationCacheService : IConfigurationCacheService
    {
        private readonly ConcurrentDictionary<string, object> _cache = new ConcurrentDictionary<string, object>();
        private long _hitCount = 0;
        private long _missCount = 0;
        private readonly bool _enabled;

        public ConfigurationCacheService(bool enabled = true)
        {
            _enabled = enabled;
        }

        /// <summary>
        /// 生成缓存键
        /// </summary>
        private string GetCacheKey<T>(string configId) where T : class, IConfig
        {
            return $"{typeof(T).FullName}:{configId}";
        }

        public T Get<T>(string configId) where T : class, IConfig
        {
            if (!_enabled) return null;

            var key = GetCacheKey<T>(configId);
            if (_cache.TryGetValue(key, out var value))
            {
                Interlocked.Increment(ref _hitCount);
                return value as T;
            }

            Interlocked.Increment(ref _missCount);
            return null;
        }

        public bool TryGet<T>(string configId, out T config) where T : class, IConfig
        {
            config = Get<T>(configId);
            return config != null;
        }

        public void Set<T>(T config) where T : class, IConfig
        {
            if (!_enabled || config == null) return;

            var key = GetCacheKey<T>(config.ConfigId);
            _cache[key] = config;
        }

        /// <summary>
        /// 设置配置缓存（非泛型版本）
        /// </summary>
        public void Set(IConfig config)
        {
            if (!_enabled || config == null) return;

            var configType = config.GetType();
            var key = $"{configType.FullName}:{config.ConfigId}";
            _cache[key] = config;
        }

        public bool Remove<T>(string configId) where T : class, IConfig
        {
            if (!_enabled) return false;

            var key = GetCacheKey<T>(configId);
            return _cache.TryRemove(key, out _);
        }

        /// <summary>
        /// 移除缓存的配置（非泛型版本）
        /// </summary>
        public bool Remove(IConfig config)
        {
            if (!_enabled || config == null) return false;

            var configType = config.GetType();
            var key = $"{configType.FullName}:{config.ConfigId}";
            return _cache.TryRemove(key, out _);
        }

        public void Clear()
        {
            _cache.Clear();
            Interlocked.Exchange(ref _hitCount, 0);
            Interlocked.Exchange(ref _missCount, 0);
        }

        public void Clear<T>() where T : class, IConfig
        {
            var prefix = $"{typeof(T).FullName}:";
            var keysToRemove = _cache.Keys.Where(k => k.StartsWith(prefix)).ToList();
            
            foreach (var key in keysToRemove)
            {
                _cache.TryRemove(key, out _);
            }
        }

        public bool Contains<T>(string configId) where T : class, IConfig
        {
            if (!_enabled) return false;

            var key = GetCacheKey<T>(configId);
            return _cache.ContainsKey(key);
        }

        public CacheStatistics GetStatistics()
        {
            return new CacheStatistics
            {
                TotalCount = _cache.Count,
                HitCount = Interlocked.Read(ref _hitCount),
                MissCount = Interlocked.Read(ref _missCount)
            };
        }
    }
}
