using System;
using System.Collections.Generic;

// ⚠️ 迁移说明：
//   - 原位置：Astra.Core/Devices/Common/OperationResult.cs
//   - 新位置：Astra.Core/Foundation/Common/OperationResult.cs
//   - 命名空间：Astra.Core.Foundation.Common（已更新为与文件夹匹配）

namespace Astra.Core.Foundation.Common
{
    /// <summary>
    /// 操作结果类（无数据返回）
    /// 
    /// ✅ 迁移说明：
    ///   - 文件已移动到 Foundation/Common/
    ///   - 命名空间已更新为：Astra.Core.Foundation.Common
    ///   - 建议新代码使用：using Astra.Core.Foundation.Common;
    /// </summary>
    public class OperationResult
    {
        /// <summary>
        /// 操作是否成功
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// 错误码（0表示成功）
        /// </summary>
        public int ErrorCode { get; set; }

        /// <summary>
        /// 错误消息
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// 详细信息
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// 异常信息
        /// </summary>
        public Exception Exception { get; set; }

        /// <summary>
        /// 操作时间戳
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// 扩展数据
        /// </summary>
        public Dictionary<string, object> ExtendedData { get; set; }

        public OperationResult()
        {
            Timestamp = DateTime.Now;
            ExtendedData = new Dictionary<string, object>();
        }

        #region 静态工厂方法

        public static OperationResult Succeed(string message = null)
        {
            return new OperationResult
            {
                Success = true,
                ErrorCode = 0,
                Message = message ?? "操作成功"
            };
        }

        public static OperationResult Failure(string errorMessage, int errorCode = -1)
        {
            return new OperationResult
            {
                Success = false,
                ErrorCode = errorCode,
                ErrorMessage = errorMessage,
                Message = errorMessage
            };
        }

        public static OperationResult Fail(Exception exception, int errorCode = -1)
        {
            return new OperationResult
            {
                Success = false,
                ErrorCode = errorCode,
                ErrorMessage = exception?.Message ?? "未知错误",
                Exception = exception,
                Message = exception?.Message ?? "未知错误"
            };
        }

        public static OperationResult Fail(string errorMessage, Exception exception, int errorCode = -1)
        {
            return new OperationResult
            {
                Success = false,
                ErrorCode = errorCode,
                ErrorMessage = errorMessage,
                Exception = exception,
                Message = errorMessage
            };
        }

        #endregion

        public OperationResult WithData(string key, object value)
        {
            ExtendedData[key] = value;
            return this;
        }

        public override string ToString()
        {
            if (Success)
                return $"[成功] {Message}";
            else
                return $"[失败] 错误码:{ErrorCode}, 消息:{ErrorMessage}";
        }
    }

    /// <summary>
    /// 操作结果类（带数据返回）
    /// </summary>
    public class OperationResult<T> : OperationResult
    {
        public T Data { get; set; }

        #region 静态工厂方法

        public static OperationResult<T> Succeed(T data, string message = null)
        {
            return new OperationResult<T>
            {
                Success = true,
                ErrorCode = 0,
                Data = data,
                Message = message ?? "操作成功"
            };
        }

        public new static OperationResult<T> Failure(string errorMessage, int errorCode = -1)
        {
            return new OperationResult<T>
            {
                Success = false,
                ErrorCode = errorCode,
                ErrorMessage = errorMessage,
                Message = errorMessage,
                Data = default(T)
            };
        }

        public new static OperationResult<T> Fail(Exception exception, int errorCode = -1)
        {
            return new OperationResult<T>
            {
                Success = false,
                ErrorCode = errorCode,
                ErrorMessage = exception?.Message ?? "未知错误",
                Exception = exception,
                Message = exception?.Message ?? "未知错误",
                Data = default(T)
            };
        }

        public new static OperationResult<T> Fail(string errorMessage, Exception exception, int errorCode = -1)
        {
            return new OperationResult<T>
            {
                Success = false,
                ErrorCode = errorCode,
                ErrorMessage = errorMessage,
                Exception = exception,
                Message = errorMessage,
                Data = default(T)
            };
        }

        /// <summary>
        /// 创建部分成功的结果（用于批量操作，部分成功部分失败的情况）
        /// </summary>
        /// <param name="message">结果消息</param>
        /// <param name="data">成功的数据</param>
        /// <param name="errorDetails">错误详情（可选）</param>
        /// <param name="errorCode">错误码（可选，默认使用部分成功码）</param>
        /// <returns>部分成功的结果</returns>
        public static OperationResult<T> PartialSuccess(
            string message,
            T data,
            string errorDetails = null,
            int errorCode = 0)
        {
            return new OperationResult<T>
            {
                Success = true, // 部分成功仍然视为成功
                ErrorCode = errorCode,
                Data = data,
                Message = message,
                ErrorMessage = errorDetails // 错误详情存储在 ErrorMessage 中
            };
        }

        #endregion

        public new OperationResult<T> WithData(string key, object value)
        {
            ExtendedData[key] = value;
            return this;
        }

        public override string ToString()
        {
            if (Success)
                return $"[成功] {Message}, 数据: {Data}";
            else
                return $"[失败] 错误码:{ErrorCode}, 消息:{ErrorMessage}";
        }
    }
}

