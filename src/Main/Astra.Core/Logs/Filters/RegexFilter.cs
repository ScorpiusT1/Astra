using System.Text.RegularExpressions;

namespace Astra.Core.Logs.Filters
{
    /// <summary>
    /// 正则表达式过滤器
    /// 根据日志消息内容的正则表达式匹配结果过滤日志
    /// </summary>
    public class RegexFilter : ILogFilter
    {
        private readonly Regex _pattern;
        private readonly bool _allowMatch;

        /// <summary>
        /// 创建正则表达式过滤器
        /// </summary>
        /// <param name="pattern">正则表达式模式</param>
        /// <param name="allowMatch">true 表示匹配的日志允许记录，false 表示匹配的日志被过滤</param>
        /// <param name="options">正则表达式选项</param>
        public RegexFilter(string pattern, bool allowMatch = true, RegexOptions options = RegexOptions.None)
        {
            if (string.IsNullOrWhiteSpace(pattern))
                throw new System.ArgumentException("正则表达式模式不能为空", nameof(pattern));

            _pattern = new Regex(pattern, options);
            _allowMatch = allowMatch;
        }

        public bool ShouldLog(LogEntry entry)
        {
            if (entry == null || string.IsNullOrEmpty(entry.Message))
                return !_allowMatch; // 如果消息为空，根据 allowMatch 决定

            var isMatch = _pattern.IsMatch(entry.Message);
            return _allowMatch ? isMatch : !isMatch;
        }
    }
}

