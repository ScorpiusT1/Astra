using System;

namespace Astra.Core.Logs.ErrorHandling
{
    /// <summary>
    /// 日志器错误处理接口
    /// 处理日志系统自身产生的错误
    /// </summary>
    public interface ILoggerErrorHandler
    {
        /// <summary>
        /// 处理错误
        /// </summary>
        /// <param name="message">错误消息</param>
        /// <param name="exception">异常对象</param>
        void OnError(string message, Exception exception = null);
    }

    /// <summary>
    /// 默认错误处理器
    /// 将错误输出到控制台错误流
    /// </summary>
    public class DefaultLoggerErrorHandler : ILoggerErrorHandler
    {
        public void OnError(string message, Exception exception = null)
        {
            Console.Error.WriteLine($"[LoggerError] {message}");
            if (exception != null)
            {
                Console.Error.WriteLine($"Exception: {exception.Message}");
                if (!string.IsNullOrEmpty(exception.StackTrace))
                {
                    Console.Error.WriteLine($"StackTrace: {exception.StackTrace}");
                }
            }
        }
    }

    /// <summary>
    /// 空错误处理器
    /// 忽略所有错误（用于测试或特殊场景）
    /// </summary>
    public class NullLoggerErrorHandler : ILoggerErrorHandler
    {
        public void OnError(string message, Exception exception = null)
        {
            // 忽略所有错误
        }
    }
}

