using Astra.Core.Plugins.Concurrency;
using Astra.Core.Plugins.Caching;
using System;

namespace Astra.Core.Plugins.Host.Configuration
{
    /// <summary>
    /// 性能配置构建器 - 专门负责性能相关配置
    /// </summary>
    public class PerformanceConfigurationBuilder
    {
        private readonly PerformanceConfiguration _config;
        private readonly HostBuilder _hostBuilder;

        public PerformanceConfigurationBuilder(PerformanceConfiguration config, HostBuilder hostBuilder)
        {
            _config = config;
            _hostBuilder = hostBuilder;
        }

        /// <summary>
        /// 启用性能监控
        /// </summary>
        public PerformanceConfigurationBuilder EnablePerformanceMonitoring(bool enable = true)
        {
            _config.EnablePerformanceMonitoring = enable;
            return this;
        }

        /// <summary>
        /// 启用内存管理
        /// </summary>
        public PerformanceConfigurationBuilder EnableMemoryManagement(bool enable = true)
        {
            _config.EnableMemoryManagement = enable;
            return this;
        }

        /// <summary>
        /// 启用并发控制
        /// </summary>
        public PerformanceConfigurationBuilder EnableConcurrencyControl(bool enable = true)
        {
            _config.EnableConcurrencyControl = enable;
            return this;
        }

        /// <summary>
        /// 启用缓存
        /// </summary>
        public PerformanceConfigurationBuilder EnableCaching(bool enable = true)
        {
            _config.EnableCaching = enable;
            return this;
        }

        /// <summary>
        /// 启用所有性能优化
        /// </summary>
        public PerformanceConfigurationBuilder EnableAllOptimizations(bool enable = true)
        {
            _config.EnablePerformanceMonitoring = enable;
            _config.EnableMemoryManagement = enable;
            _config.EnableConcurrencyControl = enable;
            _config.EnableCaching = enable;
            return this;
        }

        /// <summary>
        /// 设置并发控制参数
        /// </summary>
        public PerformanceConfigurationBuilder WithConcurrencyControl(int maxConcurrentLoads = 4, int maxConcurrentDiscoveries = 8)
        {
            _config.MaxConcurrentLoads = maxConcurrentLoads;
            _config.MaxConcurrentDiscoveries = maxConcurrentDiscoveries;
            _config.ConcurrencyConfig = new ConcurrencyConfig
            {
                MaxConcurrency = maxConcurrentLoads,
                MaxQueueSize = 100,
                Timeout = TimeSpan.FromMinutes(5),
                EnableRateLimiting = true,
                RequestsPerSecond = 10,
                Strategy = ConcurrencyStrategy.Fair
            };
            return this;
        }

        /// <summary>
        /// 设置缓存选项
        /// </summary>
        public PerformanceConfigurationBuilder WithCaching(CacheOptions cacheOptions = null)
        {
            _config.CacheOptions = cacheOptions ?? new CacheOptions
            {
                Expiration = TimeSpan.FromMinutes(30),
                Priority = CachePriority.Normal,
                EnableCompression = false,
                MaxSize = 1000,
                EvictionPolicy = CacheEvictionPolicy.LRU
            };
            return this;
        }

        /// <summary>
        /// 配置性能
        /// </summary>
        public PerformanceConfigurationBuilder Configure(Action<PerformanceConfiguration> configure)
        {
            configure(_config);
            return this;
        }

        /// <summary>
        /// 返回HostBuilder以继续配置
        /// </summary>
        public HostBuilder And()
        {
            return _hostBuilder;
        }
    }
}
