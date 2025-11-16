namespace Astra.Core.Logs.Filters
{
    /// <summary>
    /// 日志过滤器接口
    /// 用于决定是否应该记录某条日志
    /// </summary>
    public interface ILogFilter
    {
        /// <summary>
        /// 判断是否应该记录日志
        /// </summary>
        /// <param name="entry">日志条目</param>
        /// <returns>true 表示应该记录，false 表示应该过滤掉</returns>
        bool ShouldLog(LogEntry entry);
    }
}

