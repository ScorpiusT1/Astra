using Addins.Core.Abstractions;
using Addins.Exceptions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Addins.Health
{
    /// <summary>
    /// 健康检查结果
    /// </summary>
    public class HealthCheckResult
    {
        public string Name { get; set; }
        public HealthStatus Status { get; set; }
        public string Message { get; set; }
        public TimeSpan Duration { get; set; }
        public Dictionary<string, object> Data { get; set; }
        public Exception Exception { get; set; }
        public DateTime Timestamp { get; set; }

        public HealthCheckResult()
        {
            Data = new Dictionary<string, object>();
            Timestamp = DateTime.UtcNow;
        }

        public static HealthCheckResult Healthy(string name, string message = null, TimeSpan? duration = null)
        {
            return new HealthCheckResult
            {
                Name = name,
                Status = HealthStatus.Healthy,
                Message = message,
                Duration = duration ?? TimeSpan.Zero
            };
        }

        public static HealthCheckResult Unhealthy(string name, string message, Exception exception = null, TimeSpan? duration = null)
        {
            return new HealthCheckResult
            {
                Name = name,
                Status = HealthStatus.Unhealthy,
                Message = message,
                Exception = exception,
                Duration = duration ?? TimeSpan.Zero
            };
        }

        public static HealthCheckResult Degraded(string name, string message, TimeSpan? duration = null)
        {
            return new HealthCheckResult
            {
                Name = name,
                Status = HealthStatus.Degraded,
                Message = message,
                Duration = duration ?? TimeSpan.Zero
            };
        }
    }

    public enum HealthStatus
    {
        Healthy,
        Degraded,
        Unhealthy
    }

    /// <summary>
    /// 健康检查接口
    /// </summary>
    public interface IHealthCheck
    {
        string Name { get; }
        Task<HealthCheckResult> CheckHealthAsync(CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// 健康检查服务接口
    /// </summary>
    public interface IHealthCheckService
    {
        Task<HealthReport> CheckHealthAsync(CancellationToken cancellationToken = default);
        void RegisterHealthCheck(IHealthCheck healthCheck);
        void UnregisterHealthCheck(string name);
        Task StartAsync();
        Task StopAsync();
    }

    /// <summary>
    /// 健康报告
    /// </summary>
    public class HealthReport
    {
        public HealthStatus OverallStatus { get; set; }
        public TimeSpan TotalDuration { get; set; }
        public List<HealthCheckResult> Results { get; set; }
        public DateTime Timestamp { get; set; }

        public HealthReport()
        {
            Results = new List<HealthCheckResult>();
            Timestamp = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// 健康检查服务实现
    /// </summary>
    public class HealthCheckService : IHealthCheckService
    {
        private readonly Dictionary<string, IHealthCheck> _healthChecks = new();
        private readonly IErrorLogger _logger;
        private readonly Timer _timer;
        private readonly TimeSpan _checkInterval;
        private bool _isRunning = false;

        public HealthCheckService(IErrorLogger logger = null, TimeSpan? checkInterval = null)
        {
            _logger = logger ?? new ConsoleErrorLogger();
            _checkInterval = checkInterval ?? TimeSpan.FromMinutes(1);
            _timer = new Timer(PerformHealthChecks, null, Timeout.Infinite, Timeout.Infinite);
        }

        public async Task<HealthReport> CheckHealthAsync(CancellationToken cancellationToken = default)
        {
            var report = new HealthReport();
            var startTime = DateTime.UtcNow;

            foreach (var healthCheck in _healthChecks.Values)
            {
                try
                {
                    var result = await healthCheck.CheckHealthAsync(cancellationToken);
                    report.Results.Add(result);
                }
                catch (Exception ex)
                {
                    var errorResult = HealthCheckResult.Unhealthy(healthCheck.Name, "Health check failed", ex);
                    report.Results.Add(errorResult);
                    
                    await _logger.LogErrorAsync(ex, $"Health check failed: {healthCheck.Name}");
                }
            }

            report.TotalDuration = DateTime.UtcNow - startTime;
            report.OverallStatus = DetermineOverallStatus(report.Results);

            return report;
        }

        public void RegisterHealthCheck(IHealthCheck healthCheck)
        {
            _healthChecks[healthCheck.Name] = healthCheck;
        }

        public void UnregisterHealthCheck(string name)
        {
            _healthChecks.Remove(name);
        }

        public async Task StartAsync()
        {
            _isRunning = true;
            _timer.Change(TimeSpan.Zero, _checkInterval);
            await _logger.LogInfoAsync("Health check service started");
        }

        public async Task StopAsync()
        {
            _isRunning = false;
            _timer.Change(Timeout.Infinite, Timeout.Infinite);
            await _logger.LogInfoAsync("Health check service stopped");
        }

        private async void PerformHealthChecks(object state)
        {
            if (!_isRunning) return;

            try
            {
                var report = await CheckHealthAsync();
                
                if (report.OverallStatus == HealthStatus.Unhealthy)
                {
                    await _logger.LogWarningAsync($"System health check failed: {report.OverallStatus}");
                }
            }
            catch (Exception ex)
            {
                await _logger.LogErrorAsync(ex, "Health check service error");
            }
        }

        private HealthStatus DetermineOverallStatus(List<HealthCheckResult> results)
        {
            if (results.All(r => r.Status == HealthStatus.Healthy))
                return HealthStatus.Healthy;

            if (results.Any(r => r.Status == HealthStatus.Unhealthy))
                return HealthStatus.Unhealthy;

            return HealthStatus.Degraded;
        }
    }

    /// <summary>
    /// 插件健康检查
    /// </summary>
    public class PluginHealthCheck : IHealthCheck
    {
        private readonly IPluginHost _host;
        private readonly string _pluginId;

        public string Name => $"Plugin-{_pluginId}";

        public PluginHealthCheck(IPluginHost host, string pluginId)
        {
            _host = host;
            _pluginId = pluginId;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(CancellationToken cancellationToken = default)
        {
            var startTime = DateTime.UtcNow;

            try
            {
                var plugin = _host.LoadedPlugins.FirstOrDefault(p => p.Id == _pluginId);
                if (plugin == null)
                {
                    return HealthCheckResult.Unhealthy(Name, "Plugin not found");
                }

                // 这里可以添加更复杂的健康检查逻辑
                // 例如：检查插件是否响应、检查资源使用情况等

                var duration = DateTime.UtcNow - startTime;
                return HealthCheckResult.Healthy(Name, "Plugin is running", duration);
            }
            catch (Exception ex)
            {
                var duration = DateTime.UtcNow - startTime;
                return HealthCheckResult.Unhealthy(Name, "Plugin health check failed", ex, duration);
            }
        }
    }

    /// <summary>
    /// 系统资源健康检查
    /// </summary>
    public class SystemResourceHealthCheck : IHealthCheck
    {
        public string Name => "SystemResources";

        public async Task<HealthCheckResult> CheckHealthAsync(CancellationToken cancellationToken = default)
        {
            var startTime = DateTime.UtcNow;

            try
            {
                var process = System.Diagnostics.Process.GetCurrentProcess();
                var memoryUsage = process.WorkingSet64;
                var cpuUsage = process.TotalProcessorTime;

                var data = new Dictionary<string, object>
                {
                    ["MemoryUsageMB"] = memoryUsage / 1024 / 1024,
                    ["CpuTime"] = cpuUsage.TotalMilliseconds,
                    ["ThreadCount"] = process.Threads.Count
                };

                var duration = DateTime.UtcNow - startTime;
                return new HealthCheckResult
                {
                    Name = Name,
                    Status = HealthStatus.Healthy,
                    Message = "System resources are normal",
                    Duration = duration,
                    Data = data
                };
            }
            catch (Exception ex)
            {
                var duration = DateTime.UtcNow - startTime;
                return HealthCheckResult.Unhealthy(Name, "System resource check failed", ex, duration);
            }
        }
    }
}
