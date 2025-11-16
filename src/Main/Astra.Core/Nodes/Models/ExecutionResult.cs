using System.Text.Json.Serialization;

namespace Astra.Core.Nodes.Models
{

    public class ExecutionResult
    {
        public bool Success { get; set; }
        public bool IsSkipped { get; set; }
        public string? Message { get; set; }
        public Dictionary<string, object> OutputData { get; set; } = new Dictionary<string, object>();
        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }

        [JsonIgnore]
        public TimeSpan? Duration => EndTime.HasValue && StartTime.HasValue
            ? EndTime.Value - StartTime.Value
            : null;

        [JsonIgnore]
        public Exception Exception { get; set; }

        // 静态工厂方法不设置时间
        public static ExecutionResult Successful(string message = null, Dictionary<string, object> outputData = null)
        {
            return new ExecutionResult
            {
                Success = true,
                Message = message ?? "执行成功",
                OutputData = outputData ?? new Dictionary<string, object>()
                // ✅ 不设置时间，由调用者控制
            };
        }

        public static ExecutionResult Failed(string message, Exception exception = null)
        {
            return new ExecutionResult
            {
                Success = false,
                Message = message,
                Exception = exception,
                OutputData = new Dictionary<string, object>()
            };
        }

        public static ExecutionResult Skip(string reason = null)
        {
            return new ExecutionResult
            {
                Success = true,
                IsSkipped = true,
                Message = reason ?? "节点已跳过",
                OutputData = new Dictionary<string, object>()
            };
        }

        public static ExecutionResult Cancel(string message = null)
        {
            return new ExecutionResult
            {
                Success = false,
                Message = message ?? "执行已取消",
                OutputData = new Dictionary<string, object>()
            };
        }
    }
}
