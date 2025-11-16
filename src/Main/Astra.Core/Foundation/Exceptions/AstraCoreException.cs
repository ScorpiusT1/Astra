using System;
using System.Collections.Generic;

// ⚠️ 迁移说明：
//   - 新创建：通用异常基类
//   - 位置：Astra.Core/Foundation/Exceptions/AstraCoreException.cs
//   - 命名空间：Astra.Core.Foundation.Exceptions（已更新为与文件夹匹配）
//   - 原因：为所有模块提供统一的异常基类

namespace Astra.Core.Foundation.Exceptions
{
    /// <summary>
    /// Astra 核心异常基类
    /// 
    /// ✅ 设计说明：
    ///   - 提供统一的异常基类，所有模块异常可以继承此类
    ///   - 包含时间戳和上下文信息，便于异常追踪和诊断
    ///   - 支持错误码和扩展数据
    /// </summary>
    public class AstraCoreException : Exception
    {
        /// <summary>
        /// 错误码（0表示未知错误）
        /// </summary>
        public int ErrorCode { get; set; }

        /// <summary>
        /// 异常时间戳
        /// </summary>
        public DateTime Timestamp { get; }

        /// <summary>
        /// 扩展上下文信息
        /// </summary>
        public Dictionary<string, object> Context { get; }

        /// <summary>
        /// 模块名称（发生异常的模块）
        /// </summary>
        public string ModuleName { get; set; }

        /// <summary>
        /// 操作名称（发生异常的操作）
        /// </summary>
        public string Operation { get; set; }

        public AstraCoreException(string message)
            : base(message)
        {
            Timestamp = DateTime.UtcNow;
            Context = new Dictionary<string, object>();
            ErrorCode = 0;
        }

        public AstraCoreException(string message, Exception innerException)
            : base(message, innerException)
        {
            Timestamp = DateTime.UtcNow;
            Context = new Dictionary<string, object>();
            ErrorCode = 0;
        }

        public AstraCoreException(string message, int errorCode, Exception innerException = null)
            : base(message, innerException)
        {
            Timestamp = DateTime.UtcNow;
            Context = new Dictionary<string, object>();
            ErrorCode = errorCode;
        }

        public AstraCoreException(string message, string moduleName, string operation, Exception innerException = null)
            : base(message, innerException)
        {
            Timestamp = DateTime.UtcNow;
            Context = new Dictionary<string, object>();
            ErrorCode = 0;
            ModuleName = moduleName;
            Operation = operation;
        }

        /// <summary>
        /// 添加上下文信息
        /// </summary>
        public AstraCoreException WithContext(string key, object value)
        {
            Context[key] = value;
            return this;
        }

        /// <summary>
        /// 设置错误码
        /// </summary>
        public AstraCoreException WithErrorCode(int errorCode)
        {
            ErrorCode = errorCode;
            return this;
        }

        public override string ToString()
        {
            var parts = new List<string>
            {
                $"[{Timestamp:yyyy-MM-dd HH:mm:ss.fff}]",
                $"ErrorCode: {ErrorCode}",
                $"Message: {Message}"
            };

            if (!string.IsNullOrEmpty(ModuleName))
                parts.Add($"Module: {ModuleName}");

            if (!string.IsNullOrEmpty(Operation))
                parts.Add($"Operation: {Operation}");

            if (Context.Count > 0)
                parts.Add($"Context: {string.Join(", ", Context)}");

            if (InnerException != null)
                parts.Add($"InnerException: {InnerException}");

            return string.Join(" | ", parts);
        }
    }
}

