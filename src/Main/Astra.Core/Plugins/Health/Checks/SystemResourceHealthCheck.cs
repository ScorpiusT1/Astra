using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Astra.Core.Plugins.Health
{
    /// <summary>
    /// 系统资源健康检查 - 检查进程内存、CPU 时间和线程数。
    /// </summary>
    public class SystemResourceHealthCheck : IHealthCheck
    {
        public string Name => "SystemResources";

        public Task<HealthCheckResult> CheckHealthAsync(CancellationToken cancellationToken = default)
        {
            var startTime = DateTime.UtcNow;

            try
            {
                var process = Process.GetCurrentProcess();
                var data = new Dictionary<string, object>
                {
                    ["MemoryUsageMB"] = process.WorkingSet64 / 1024 / 1024,
                    ["CpuTime"]       = process.TotalProcessorTime.TotalMilliseconds,
                    ["ThreadCount"]   = process.Threads.Count
                };

                return Task.FromResult(new HealthCheckResult
                {
                    Name     = Name,
                    Status   = HealthStatus.Healthy,
                    Message  = "System resources are normal",
                    Duration = DateTime.UtcNow - startTime,
                    Data     = data
                });
            }
            catch (Exception ex)
            {
                return Task.FromResult(
                    HealthCheckResult.Unhealthy(Name, "System resource check failed", ex, DateTime.UtcNow - startTime));
            }
        }
    }
}
