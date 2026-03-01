namespace Astra.Core.Plugins.Health
{
    /// <summary>
    /// 健康检查结果（共享契约：小而稳定）
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
}

