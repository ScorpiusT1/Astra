using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace Addins.Performance
{
    /// <summary>
    /// 性能指标接口
    /// </summary>
    public interface IPerformanceMonitor
    {
        Task<PerformanceMetrics> GetMetricsAsync(string pluginId);
        void RecordOperation(string operation, TimeSpan duration);
        void RecordOperation(string pluginId, string operation, TimeSpan duration);
        void RecordMemoryUsage(long bytes);
        void RecordMemoryUsage(string pluginId, long bytes);
        void RecordCpuUsage(double percentage);
        void RecordCpuUsage(string pluginId, double percentage);
        Task<SystemPerformanceMetrics> GetSystemMetricsAsync();
    }

    /// <summary>
    /// 性能指标数据
    /// </summary>
    public class PerformanceMetrics
    {
        public string PluginId { get; set; }
        public DateTime Timestamp { get; set; }
        public long MemoryUsage { get; set; }
        public double CpuUsage { get; set; }
        public Dictionary<string, OperationMetrics> Operations { get; set; } = new();
        public TimeSpan TotalExecutionTime { get; set; }
        public int TotalOperations { get; set; }
        public double AverageOperationTime { get; set; }
    }

    /// <summary>
    /// 操作指标
    /// </summary>
    public class OperationMetrics
    {
        public string OperationName { get; set; }
        public int CallCount { get; set; }
        public TimeSpan TotalTime { get; set; }
        public TimeSpan AverageTime { get; set; }
        public TimeSpan MinTime { get; set; }
        public TimeSpan MaxTime { get; set; }
        public DateTime LastCall { get; set; }
    }

    /// <summary>
    /// 系统性能指标
    /// </summary>
    public class SystemPerformanceMetrics
    {
        public DateTime Timestamp { get; set; }
        public long TotalMemory { get; set; }
        public long AvailableMemory { get; set; }
        public double CpuUsage { get; set; }
        public int PluginCount { get; set; }
        public Dictionary<string, PerformanceMetrics> PluginMetrics { get; set; } = new();
    }

    /// <summary>
    /// 性能监控器实现
    /// </summary>
    public class PerformanceMonitor : IPerformanceMonitor
    {
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, OperationMetrics>> _operationMetrics = new();
        private readonly ConcurrentDictionary<string, long> _memoryUsage = new();
        private readonly ConcurrentDictionary<string, double> _cpuUsage = new();
        private readonly ConcurrentDictionary<string, DateTime> _lastUpdate = new();
        private readonly object _lock = new object();

        public async Task<PerformanceMetrics> GetMetricsAsync(string pluginId)
        {
            return await Task.Run(() =>
            {
                var metrics = new PerformanceMetrics
                {
                    PluginId = pluginId,
                    Timestamp = DateTime.UtcNow,
                    MemoryUsage = _memoryUsage.GetValueOrDefault(pluginId, 0),
                    CpuUsage = _cpuUsage.GetValueOrDefault(pluginId, 0),
                    Operations = new Dictionary<string, OperationMetrics>()
                };

                if (_operationMetrics.TryGetValue(pluginId, out var operations))
                {
                    foreach (var operation in operations)
                    {
                        metrics.Operations[operation.Key] = operation.Value;
                    }

                    metrics.TotalOperations = operations.Values.Sum(op => op.CallCount);
                    metrics.TotalExecutionTime = TimeSpan.FromTicks(operations.Values.Sum(op => op.TotalTime.Ticks));
                    metrics.AverageOperationTime = metrics.TotalOperations > 0 
                        ? metrics.TotalExecutionTime.TotalMilliseconds / metrics.TotalOperations
                        : 0;
                }

                return metrics;
            });
        }

        public void RecordOperation(string operation, TimeSpan duration)
        {
            RecordOperation("system", operation, duration);
        }

        public void RecordOperation(string pluginId, string operation, TimeSpan duration)
        {
            var operations = _operationMetrics.GetOrAdd(pluginId, _ => new ConcurrentDictionary<string, OperationMetrics>());
            
            operations.AddOrUpdate(operation, 
                new OperationMetrics
                {
                    OperationName = operation,
                    CallCount = 1,
                    TotalTime = duration,
                    AverageTime = duration,
                    MinTime = duration,
                    MaxTime = duration,
                    LastCall = DateTime.UtcNow
                },
                (key, existing) =>
                {
                    existing.CallCount++;
                    existing.TotalTime += duration;
                    existing.AverageTime = TimeSpan.FromTicks(existing.TotalTime.Ticks / (long)existing.CallCount);
                    existing.MinTime = duration < existing.MinTime ? duration : existing.MinTime;
                    existing.MaxTime = duration > existing.MaxTime ? duration : existing.MaxTime;
                    existing.LastCall = DateTime.UtcNow;
                    return existing;
                });

            _lastUpdate.AddOrUpdate(pluginId, DateTime.UtcNow, (key, existing) => DateTime.UtcNow);
        }

        public void RecordMemoryUsage(long bytes)
        {
            RecordMemoryUsage("system", bytes);
        }

        public void RecordMemoryUsage(string pluginId, long bytes)
        {
            _memoryUsage.AddOrUpdate(pluginId, bytes, (key, existing) => bytes);
            _lastUpdate.AddOrUpdate(pluginId, DateTime.UtcNow, (key, existing) => DateTime.UtcNow);
        }

        public void RecordCpuUsage(double percentage)
        {
            RecordCpuUsage("system", percentage);
        }

        public void RecordCpuUsage(string pluginId, double percentage)
        {
            _cpuUsage.AddOrUpdate(pluginId, percentage, (key, existing) => percentage);
            _lastUpdate.AddOrUpdate(pluginId, DateTime.UtcNow, (key, existing) => DateTime.UtcNow);
        }

        public async Task<SystemPerformanceMetrics> GetSystemMetricsAsync()
        {
            return await Task.Run(() =>
            {
                var process = Process.GetCurrentProcess();
                var systemMetrics = new SystemPerformanceMetrics
                {
                    Timestamp = DateTime.UtcNow,
                    TotalMemory = GC.GetTotalMemory(false),
                    AvailableMemory = GC.GetTotalMemory(false) - GC.GetTotalMemory(true),
                    CpuUsage = GetCpuUsage(),
                    PluginCount = _operationMetrics.Count
                };

                foreach (var pluginId in _operationMetrics.Keys)
                {
                    var metrics = GetMetricsAsync(pluginId).Result;
                    systemMetrics.PluginMetrics[pluginId] = metrics;
                }

                return systemMetrics;
            });
        }

        private double GetCpuUsage()
        {
            try
            {
                var process = Process.GetCurrentProcess();
                return process.TotalProcessorTime.TotalMilliseconds / Environment.ProcessorCount;
            }
            catch
            {
                return 0;
            }
        }
    }

    /// <summary>
    /// 性能监控装饰器
    /// </summary>
    public class PerformanceMonitoringDecorator<T> where T : class
    {
        private readonly T _instance;
        private readonly IPerformanceMonitor _monitor;
        private readonly string _pluginId;

        public PerformanceMonitoringDecorator(T instance, IPerformanceMonitor monitor, string pluginId)
        {
            _instance = instance;
            _monitor = monitor;
            _pluginId = pluginId;
        }

        public T Instance => _instance;

        public void RecordOperation(string operationName, Action operation)
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                operation();
            }
            finally
            {
                stopwatch.Stop();
                _monitor.RecordOperation(_pluginId, operationName, stopwatch.Elapsed);
            }
        }

        public TReturn RecordOperation<TReturn>(string operationName, Func<TReturn> operation)
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                return operation();
            }
            finally
            {
                stopwatch.Stop();
                _monitor.RecordOperation(_pluginId, operationName, stopwatch.Elapsed);
            }
        }

        public async Task RecordOperationAsync(string operationName, Func<Task> operation)
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                await operation();
            }
            finally
            {
                stopwatch.Stop();
                _monitor.RecordOperation(_pluginId, operationName, stopwatch.Elapsed);
            }
        }

        public async Task<TReturn> RecordOperationAsync<TReturn>(string operationName, Func<Task<TReturn>> operation)
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                return await operation();
            }
            finally
            {
                stopwatch.Stop();
                _monitor.RecordOperation(_pluginId, operationName, stopwatch.Elapsed);
            }
        }
    }
}
