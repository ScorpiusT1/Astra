using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Astra.Core.Plugins.Abstractions;
using Astra.Core.Plugins.Exceptions;
using Astra.Core.Plugins.Performance;

namespace Astra.Core.Plugins.Concurrency
{
    /// <summary>
    /// 并发管理器接口
    /// </summary>
    public interface IConcurrencyManager
    {
        Task<T> ExecuteWithConcurrencyControl<T>(Func<Task<T>> operation, string operationName, ConcurrencyConfig config = null);
        Task ExecuteWithConcurrencyControl(Func<Task> operation, string operationName, ConcurrencyConfig config = null);
        Task<ConcurrencyReport> GetConcurrencyReportAsync();
        void SetMaxConcurrency(string operationType, int maxConcurrency);
        void SetRateLimit(string operationType, int requestsPerSecond);
    }

    /// <summary>
    /// 并发配置
    /// </summary>
    public class ConcurrencyConfig
    {
        public int MaxConcurrency { get; set; } = 4;
        public int MaxQueueSize { get; set; } = 100;
        public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(5);
        public bool EnableRateLimiting { get; set; } = true;
        public int RequestsPerSecond { get; set; } = 10;
        public ConcurrencyStrategy Strategy { get; set; } = ConcurrencyStrategy.Fair;
    }

    /// <summary>
    /// 并发策略
    /// </summary>
    public enum ConcurrencyStrategy
    {
        Fair,           // 公平调度
        Priority,       // 优先级调度
        RoundRobin,     // 轮询调度
        Weighted        // 加权调度
    }

    /// <summary>
    /// 并发报告
    /// </summary>
    public class ConcurrencyReport
    {
        public DateTime GeneratedAt { get; set; }
        public Dictionary<string, OperationConcurrencyInfo> Operations { get; set; } = new();
        public int TotalActiveOperations { get; set; }
        public int TotalQueuedOperations { get; set; }
        public double AverageWaitTime { get; set; }
        public double AverageExecutionTime { get; set; }
    }

    /// <summary>
    /// 操作并发信息
    /// </summary>
    public class OperationConcurrencyInfo
    {
        public string OperationName { get; set; }
        public int ActiveCount { get; set; }
        public int QueuedCount { get; set; }
        public int MaxConcurrency { get; set; }
        public int TotalExecuted { get; set; }
        public TimeSpan TotalExecutionTime { get; set; }
        public TimeSpan AverageExecutionTime { get; set; }
        public TimeSpan AverageWaitTime { get; set; }
        public int RejectedCount { get; set; }
        public int TimeoutCount { get; set; }
    }

    /// <summary>
    /// 并发管理器实现
    /// </summary>
    public class ConcurrencyManager : IConcurrencyManager
    {
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _semaphores = new();
        private readonly ConcurrentDictionary<string, ConcurrencyConfig> _configs = new();
        private readonly ConcurrentDictionary<string, OperationConcurrencyInfo> _operationStats = new();
        private readonly ConcurrentDictionary<string, RateLimiter> _rateLimiters = new();
        private readonly IPerformanceMonitor _performanceMonitor;
        private readonly object _lock = new object();

        public ConcurrencyManager(IPerformanceMonitor performanceMonitor = null)
        {
            _performanceMonitor = performanceMonitor ?? new PerformanceMonitor();
        }

        /// <summary>
        /// 在并发与速率限制下执行异步操作，并统计等待/执行耗时。
        /// </summary>
        /// <typeparam name="T">返回类型</typeparam>
        /// <param name="operation">要执行的异步操作</param>
        /// <param name="operationName">操作名称（用于指标与配置键）</param>
        /// <param name="config">可选配置，未提供时按 operationName 获取或创建</param>
        public async Task<T> ExecuteWithConcurrencyControl<T>(Func<Task<T>> operation, string operationName, ConcurrencyConfig config = null)
        {
            config ??= GetOrCreateConfig(operationName);
            var semaphore = GetOrCreateSemaphore(operationName, config.MaxConcurrency);
            var rateLimiter = GetOrCreateRateLimiter(operationName, config);
            var stats = GetOrCreateStats(operationName);

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var waitStopwatch = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                // 速率限制
                if (config.EnableRateLimiting)
                {
                    await rateLimiter.WaitAsync();
                }

                // 并发控制
                var flag = await semaphore.WaitAsync(config.Timeout);

                try
                {
                    waitStopwatch.Stop();
                    stats.AverageWaitTime = UpdateAverageTime(stats.AverageWaitTime, waitStopwatch.Elapsed, stats.TotalExecuted);

                    stopwatch.Restart();
                    var result = await operation();
                    stopwatch.Stop();

                    stats.TotalExecuted++;
                    stats.TotalExecutionTime += stopwatch.Elapsed;
                    stats.AverageExecutionTime = TimeSpan.FromTicks(stats.TotalExecutionTime.Ticks / (long)stats.TotalExecuted);

                    _performanceMonitor.RecordOperation(operationName, stopwatch.Elapsed);
                    return result;
                }
                finally
                {
                    semaphore.Release();
                }
            }
            catch (OperationCanceledException) when (waitStopwatch.Elapsed >= config.Timeout)
            {
                stats.TimeoutCount++;
                throw new PluginTimeoutException($"Operation {operationName} timed out", operationName, config.Timeout);
            }
            catch (Exception ex)
            {
                stats.RejectedCount++;
                throw;
            }
            finally
            {
                waitStopwatch.Stop();
                stopwatch.Stop();
            }
        }

        /// <summary>
        /// 在并发与速率限制下执行异步操作（无返回值）。
        /// </summary>
        public async Task ExecuteWithConcurrencyControl(Func<Task> operation, string operationName, ConcurrencyConfig config = null)
        {
            await ExecuteWithConcurrencyControl(async () =>
            {
                await operation();
                return true;
            }, operationName, config);
        }

        /// <summary>
        /// 生成当前并发系统报告（各操作的并发/等待/超时/拒绝统计）。
        /// </summary>
        public async Task<ConcurrencyReport> GetConcurrencyReportAsync()
        {
            return await Task.Run(() =>
            {
                var report = new ConcurrencyReport
                {
                    GeneratedAt = DateTime.UtcNow
                };

                foreach (var kvp in _operationStats)
                {
                    report.Operations[kvp.Key] = kvp.Value;
                }

                report.TotalActiveOperations = _semaphores.Values.Sum(s => s.CurrentCount == 0 ? 1 : 0);
                report.TotalQueuedOperations = _operationStats.Values.Sum(s => s.QueuedCount);
                report.AverageWaitTime = _operationStats.Values.Any() 
                    ? _operationStats.Values.Average(s => s.AverageWaitTime.TotalMilliseconds)
                    : 0;
                report.AverageExecutionTime = _operationStats.Values.Any()
                    ? _operationStats.Values.Average(s => s.AverageExecutionTime.TotalMilliseconds)
                    : 0;

                return report;
            });
        }

        /// <summary>
        /// 设置某类操作的最大并发度，并重建对应信号量。
        /// </summary>
        public void SetMaxConcurrency(string operationType, int maxConcurrency)
        {
            var config = GetOrCreateConfig(operationType);
            config.MaxConcurrency = maxConcurrency;
            
            // 重新创建信号量
            _semaphores.TryRemove(operationType, out var oldSemaphore);
            oldSemaphore?.Dispose();
            
            GetOrCreateSemaphore(operationType, maxConcurrency);
        }

        /// <summary>
        /// 设置某类操作的速率限制（每秒请求数）。
        /// </summary>
        public void SetRateLimit(string operationType, int requestsPerSecond)
        {
            var config = GetOrCreateConfig(operationType);
            config.RequestsPerSecond = requestsPerSecond;
            config.EnableRateLimiting = true;
            
            GetOrCreateRateLimiter(operationType, config);
        }

        private ConcurrencyConfig GetOrCreateConfig(string operationName)
        {
            return _configs.GetOrAdd(operationName, _ => new ConcurrencyConfig());
        }

        private SemaphoreSlim GetOrCreateSemaphore(string operationName, int maxConcurrency)
        {
            return _semaphores.GetOrAdd(operationName, _ => new SemaphoreSlim(maxConcurrency, maxConcurrency));
        }

        private RateLimiter GetOrCreateRateLimiter(string operationName, ConcurrencyConfig config)
        {
            return _rateLimiters.GetOrAdd(operationName, _ => new RateLimiter(config.RequestsPerSecond));
        }

        private OperationConcurrencyInfo GetOrCreateStats(string operationName)
        {
            return _operationStats.GetOrAdd(operationName, _ => new OperationConcurrencyInfo
            {
                OperationName = operationName,
                MaxConcurrency = GetOrCreateConfig(operationName).MaxConcurrency
            });
        }

        private TimeSpan UpdateAverageTime(TimeSpan currentAverage, TimeSpan newTime, int count)
        {
            if (count == 0) return newTime;
            
            var totalTicks = currentAverage.Ticks * count + newTime.Ticks;
            return TimeSpan.FromTicks(totalTicks / (long)(count + 1));
        }

        public void Dispose()
        {
            foreach (var semaphore in _semaphores.Values)
            {
                semaphore?.Dispose();
            }
            
            foreach (var rateLimiter in _rateLimiters.Values)
            {
                rateLimiter?.Dispose();
            }
        }
    }

    /// <summary>
    /// 速率限制器
    /// </summary>
    public class RateLimiter : IDisposable
    {
        private readonly SemaphoreSlim _semaphore;
        private readonly Timer _timer;
        private readonly int _requestsPerSecond;
        private int _currentRequests;
        private bool _disposed = false;

        public RateLimiter(int requestsPerSecond)
        {
            _requestsPerSecond = requestsPerSecond;
            _semaphore = new SemaphoreSlim(requestsPerSecond, requestsPerSecond);
            
            // 每秒重置计数器
            _timer = new Timer(ResetCounter, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
        }

        public async Task WaitAsync()
        {
            await _semaphore.WaitAsync();
        }

        private void ResetCounter(object state)
        {
            if (!_disposed)
            {
                var currentCount = _semaphore.CurrentCount;
                var toRelease = _requestsPerSecond - currentCount;
                
                if (toRelease > 0)
                {
                    _semaphore.Release(toRelease);
                }
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                _timer?.Dispose();
                _semaphore?.Dispose();
            }
        }
    }

    /// <summary>
    /// 并发控制装饰器
    /// </summary>
    public class ConcurrencyControlledPluginHost : IPluginHost
    {
        private readonly IPluginHost _baseHost;
        private readonly IConcurrencyManager _concurrencyManager;

        public ConcurrencyControlledPluginHost(IPluginHost baseHost, IConcurrencyManager concurrencyManager)
        {
            _baseHost = baseHost;
            _concurrencyManager = concurrencyManager;
        }

        public IReadOnlyList<IPlugin> LoadedPlugins => _baseHost.LoadedPlugins;

        public async Task<IPlugin> LoadPluginAsync(string path)
        {
            return await _concurrencyManager.ExecuteWithConcurrencyControl(
                () => _baseHost.LoadPluginAsync(path),
                "LoadPlugin",
                new ConcurrencyConfig { MaxConcurrency = 2, Timeout = TimeSpan.FromMinutes(2) }
            );
        }

        public async Task UnloadPluginAsync(string pluginId)
        {
            await _concurrencyManager.ExecuteWithConcurrencyControl(
                () => _baseHost.UnloadPluginAsync(pluginId),
                "UnloadPlugin",
                new ConcurrencyConfig { MaxConcurrency = 4, Timeout = TimeSpan.FromMinutes(1) }
            );
        }

        public async Task<T> GetServiceAsync<T>() where T : class
        {
            return await _concurrencyManager.ExecuteWithConcurrencyControl(
                () => _baseHost.GetServiceAsync<T>(),
                "GetService",
                new ConcurrencyConfig { MaxConcurrency = 10, Timeout = TimeSpan.FromSeconds(30) }
            );
        }
    }
}
