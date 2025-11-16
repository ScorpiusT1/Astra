using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Astra.Bootstrap.Core
{
    /// <summary>
    /// 启动异常
    /// </summary>
    public class BootstrapException : Exception
    {
        public BootstrapException(string message, Exception innerException = null, IBootstrapTask failedTask = null)
            : base(message, innerException)
        {
            FailedTask = failedTask;
            Timestamp = DateTime.Now;
        }

        /// <summary>
        /// 失败的任务
        /// </summary>
        public IBootstrapTask FailedTask { get; }

        /// <summary>
        /// 异常时间
        /// </summary>
        public DateTime Timestamp { get; }

        /// <summary>
        /// 是否为关键任务失败
        /// </summary>
        public bool IsCriticalFailure => FailedTask?.IsCritical ?? false;
    }
}
