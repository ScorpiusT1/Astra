namespace Astra.Core.Logs.Filters
{
    /// <summary>
    /// 日志级别过滤器
    /// 根据日志级别过滤日志
    /// </summary>
    public class LevelFilter : ILogFilter
    {
        private readonly LogLevel _minLevel;

        /// <summary>
        /// 创建级别过滤器
        /// </summary>
        /// <param name="minLevel">最低日志级别，低于此级别的日志将被过滤</param>
        public LevelFilter(LogLevel minLevel)
        {
            _minLevel = minLevel;
        }

        public bool ShouldLog(LogEntry entry)
        {
            if (entry == null)
                return false;

            // 将字符串级别转换为 LogLevel 枚举
            if (System.Enum.TryParse<LogLevel>(entry.Level, true, out var entryLevel))
            {
                return entryLevel >= _minLevel;
            }

            // 如果无法解析，默认允许记录
            return true;
        }
    }
}

