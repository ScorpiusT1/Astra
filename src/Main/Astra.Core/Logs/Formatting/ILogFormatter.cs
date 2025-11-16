namespace Astra.Core.Logs.Formatting
{
    /// <summary>
    /// 日志格式化接口
    /// 定义日志条目的格式化方式，支持多种格式（文本、JSON、XML等）
    /// </summary>
    public interface ILogFormatter
    {
        /// <summary>
        /// 格式化日志条目为字符串
        /// </summary>
        /// <param name="entry">日志条目</param>
        /// <returns>格式化后的字符串</returns>
        string Format(LogEntry entry);
    }
}

