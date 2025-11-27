﻿using System.Text.Json.Serialization;

namespace Astra.Core.Nodes.Models
{
    /// <summary>
    /// 执行结果
    /// 符合单一职责原则：专门负责封装执行结果信息
    /// 符合开闭原则：通过OutputData和ErrorCode支持扩展
    /// </summary>
    public class ExecutionResult
    {
        /// <summary>
        /// 执行是否成功
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// 是否跳过执行
        /// </summary>
        public bool IsSkipped { get; set; }

        /// <summary>
        /// 结果消息
        /// </summary>
        public string? Message { get; set; }

        /// <summary>
        /// 错误码（用于标准化错误处理）
        /// </summary>
        public string ErrorCode { get; set; }

        /// <summary>
        /// 结果类型（更细粒度的分类）
        /// </summary>
        public ExecutionResultType ResultType { get; set; }

        /// <summary>
        /// 输出数据（传递给下游节点的数据）
        /// </summary>
        public Dictionary<string, object> OutputData { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// 执行开始时间
        /// </summary>
        public DateTime? StartTime { get; set; }

        /// <summary>
        /// 执行结束时间
        /// </summary>
        public DateTime? EndTime { get; set; }

        /// <summary>
        /// 执行时长（计算属性）
        /// </summary>
        [JsonIgnore]
        public TimeSpan? Duration => EndTime.HasValue && StartTime.HasValue
            ? EndTime.Value - StartTime.Value
            : null;

        /// <summary>
        /// 异常对象（仅用于运行时，不序列化）
        /// </summary>
        [JsonIgnore]
        public Exception Exception { get; set; }

        // ===== 工厂方法：符合简单工厂模式，提高易用性 =====

        /// <summary>
        /// 创建成功结果
        /// </summary>
        public static ExecutionResult Successful(string message = null, Dictionary<string, object> outputData = null)
        {
            return new ExecutionResult
            {
                Success = true,
                ResultType = ExecutionResultType.Success,
                Message = message ?? "执行成功",
                OutputData = outputData ?? new Dictionary<string, object>()
            };
        }

        /// <summary>
        /// 创建失败结果
        /// </summary>
        public static ExecutionResult Failed(string message, Exception exception = null, string errorCode = null)
        {
            return new ExecutionResult
            {
                Success = false,
                ResultType = ExecutionResultType.Failed,
                Message = message,
                Exception = exception,
                ErrorCode = errorCode ?? "EXEC_FAILED",
                OutputData = new Dictionary<string, object>()
            };
        }

        /// <summary>
        /// 创建跳过结果
        /// </summary>
        public static ExecutionResult Skip(string reason = null)
        {
            return new ExecutionResult
            {
                Success = true,
                IsSkipped = true,
                ResultType = ExecutionResultType.Skipped,
                Message = reason ?? "节点已跳过",
                OutputData = new Dictionary<string, object>()
            };
        }

        /// <summary>
        /// 创建取消结果
        /// </summary>
        public static ExecutionResult Cancel(string message = null)
        {
            return new ExecutionResult
            {
                Success = false,
                ResultType = ExecutionResultType.Cancelled,
                Message = message ?? "执行已取消",
                ErrorCode = "EXEC_CANCELLED",
                OutputData = new Dictionary<string, object>()
            };
        }

        /// <summary>
        /// 创建超时结果
        /// </summary>
        public static ExecutionResult Timeout(string message = null, int timeoutSeconds = 0)
        {
            return new ExecutionResult
            {
                Success = false,
                ResultType = ExecutionResultType.Timeout,
                Message = message ?? $"执行超时（{timeoutSeconds}秒）",
                ErrorCode = "EXEC_TIMEOUT",
                OutputData = new Dictionary<string, object>()
            };
        }

        /// <summary>
        /// 创建验证失败结果
        /// </summary>
        public static ExecutionResult ValidationFailed(string message, string[] errors = null)
        {
            return new ExecutionResult
            {
                Success = false,
                ResultType = ExecutionResultType.ValidationFailed,
                Message = message,
                ErrorCode = "VALIDATION_FAILED",
                OutputData = new Dictionary<string, object>
                {
                    { "ValidationErrors", errors ?? Array.Empty<string>() }
                }
            };
        }

        // ===== 辅助方法：提高易用性 =====

        /// <summary>
        /// 添加输出数据（支持链式调用）
        /// </summary>
        public ExecutionResult WithOutput(string key, object value)
        {
            OutputData[key] = value;
            return this;
        }

        /// <summary>
        /// 批量添加输出数据（支持链式调用）
        /// </summary>
        public ExecutionResult WithOutputs(Dictionary<string, object> data)
        {
            if (data != null)
            {
                foreach (var kvp in data)
                {
                    OutputData[kvp.Key] = kvp.Value;
                }
            }
            return this;
        }

        /// <summary>
        /// 获取输出数据（类型安全）
        /// </summary>
        public T GetOutput<T>(string key, T defaultValue = default)
        {
            if (OutputData.TryGetValue(key, out var value) && value is T typedValue)
            {
                return typedValue;
            }
            return defaultValue;
        }
    }

    /// <summary>
    /// 执行结果类型枚举
    /// 提供更细粒度的结果分类，便于监控和错误处理
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum ExecutionResultType
    {
        /// <summary>
        /// 执行成功
        /// </summary>
        Success,

        /// <summary>
        /// 执行失败
        /// </summary>
        Failed,

        /// <summary>
        /// 已跳过
        /// </summary>
        Skipped,

        /// <summary>
        /// 已取消
        /// </summary>
        Cancelled,

        /// <summary>
        /// 执行超时
        /// </summary>
        Timeout,

        /// <summary>
        /// 验证失败
        /// </summary>
        ValidationFailed
    }
}
