using System;

namespace Astra.Core.Logs
{
    /// <summary>
    /// 日志条目事件参数
    /// 用于日志事件通知
    /// </summary>
    public class LogEntryEventArgs : EventArgs
    {
        /// <summary>
        /// 日志条目
        /// </summary>
        public LogEntry Entry { get; }

        /// <summary>
        /// 创建日志条目事件参数
        /// </summary>
        /// <param name="entry">日志条目</param>
        public LogEntryEventArgs(LogEntry entry)
        {
            Entry = entry ?? throw new ArgumentNullException(nameof(entry));
        }
    }
}

