using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Astra.Core.Logs.Formatting
{
    /// <summary>
    /// 文本日志格式化器
    /// 将日志条目格式化为可读的文本格式
    /// </summary>
    public class TextLogFormatter : ILogFormatter
    {
        private readonly string _dateTimeFormat;

        public TextLogFormatter(string dateTimeFormat = "yyyy-MM-dd HH:mm:ss.fff")
        {
            _dateTimeFormat = dateTimeFormat;
        }

        public string Format(LogEntry entry)
        {
            var sb = new StringBuilder();

            // 基本信息：时间戳 | 级别 | 分类 | 日志器名称 | 消息
            sb.Append($"[{entry.Timestamp.ToString(_dateTimeFormat)}] ");
            sb.Append($"[{entry.Level,-8}] ");
            sb.Append($"[{entry.Category,-11}] ");
            sb.Append($"[{entry.Logger}] ");
            sb.Append(entry.Message);

            // Node 信息
            if (entry.NodeInfo != null)
            {
                sb.AppendLine();
                sb.Append($"  Node: Id={entry.NodeInfo.NodeId}, Type={entry.NodeInfo.NodeType}, Action={entry.NodeInfo.Action}");

                if (entry.NodeInfo.Duration.HasValue)
                {
                    sb.Append($", Duration={entry.NodeInfo.Duration.Value.TotalMilliseconds:F2}ms");
                }

                // Node 参数
                if (entry.NodeInfo.Parameters != null && entry.NodeInfo.Parameters.Count > 0)
                {
                    sb.AppendLine();
                    sb.Append("  Parameters: ");
                    var paramItems = entry.NodeInfo.Parameters.Select(kv => $"{kv.Key}={FormatValue(kv.Value)}");
                    sb.Append(string.Join(", ", paramItems));
                }
            }

            // 结构化数据（键值对）
            if (entry.Data != null && entry.Data.Count > 0)
            {
                if (entry.NodeInfo == null)
                {
                    sb.Append(" | ");
                }
                else
                {
                    sb.AppendLine();
                    sb.Append("  Data: ");
                }
                var dataItems = entry.Data.Select(kv => $"{kv.Key}={FormatValue(kv.Value)}");
                sb.Append(string.Join(", ", dataItems));
            }

            // 异常信息
            if (entry.Exception != null)
            {
                sb.AppendLine();
                sb.Append("  Exception: ");
                sb.Append(entry.Exception.GetType().Name);
                sb.Append(": ");
                sb.AppendLine(entry.Exception.Message);

                if (!string.IsNullOrEmpty(entry.Exception.StackTrace))
                {
                    sb.Append("  StackTrace: ");
                    sb.AppendLine(entry.Exception.StackTrace);
                }

                // 内部异常
                if (entry.Exception.InnerException != null)
                {
                    sb.Append("  InnerException: ");
                    sb.AppendLine(entry.Exception.InnerException.ToString());
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// 格式化值
        /// </summary>
        private string FormatValue(object value)
        {
            if (value == null) return "null";
            if (value is string str) return $"\"{str}\"";
            if (value is DateTime dt) return dt.ToString("yyyy-MM-dd HH:mm:ss");
            if (value is bool b) return b.ToString().ToLower();
            if (value is double || value is float)
                return string.Format("{0:F2}", value);
            return value.ToString();
        }
    }
}

