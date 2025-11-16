using System;
using System.Collections.Generic;
using System.Text;

namespace Astra.Core.Logs.Formatting
{
    /// <summary>
    /// JSON 日志格式化器
    /// 将日志条目格式化为 JSON 格式
    /// </summary>
    public class JsonLogFormatter : ILogFormatter
    {
        public string Format(LogEntry entry)
        {
            var sb = new StringBuilder();
            sb.Append("{");

            // 基本信息
            sb.Append($"\"timestamp\":\"{entry.Timestamp:yyyy-MM-dd HH:mm:ss.fff}\",");
            sb.Append($"\"level\":\"{entry.Level}\",");
            sb.Append($"\"category\":\"{entry.Category}\",");
            sb.Append($"\"logger\":\"{EscapeJson(entry.Logger)}\",");
            sb.Append($"\"message\":\"{EscapeJson(entry.Message)}\"");

            // Node 信息
            if (entry.NodeInfo != null)
            {
                sb.Append(",\"node\":{");
                sb.Append($"\"id\":\"{EscapeJson(entry.NodeInfo.NodeId)}\",");
                sb.Append($"\"type\":\"{EscapeJson(entry.NodeInfo.NodeType)}\",");
                sb.Append($"\"name\":\"{EscapeJson(entry.NodeInfo.NodeName)}\",");
                sb.Append($"\"action\":\"{EscapeJson(entry.NodeInfo.Action)}\"");

                if (entry.NodeInfo.Duration.HasValue)
                {
                    sb.Append($",\"duration_ms\":{entry.NodeInfo.Duration.Value.TotalMilliseconds:F2}");
                }

                if (entry.NodeInfo.Parameters != null && entry.NodeInfo.Parameters.Count > 0)
                {
                    sb.Append(",\"parameters\":{");
                    var paramItems = new List<string>();
                    foreach (var kv in entry.NodeInfo.Parameters)
                    {
                        paramItems.Add($"\"{EscapeJson(kv.Key)}\":{FormatJsonValue(kv.Value)}");
                    }
                    sb.Append(string.Join(",", paramItems));
                    sb.Append("}");
                }

                if (!string.IsNullOrEmpty(entry.NodeInfo.ErrorMessage))
                {
                    sb.Append($",\"error_message\":\"{EscapeJson(entry.NodeInfo.ErrorMessage)}\"");
                }

                sb.Append("}");
            }

            // 结构化数据
            if (entry.Data != null && entry.Data.Count > 0)
            {
                sb.Append(",\"data\":{");
                var dataItems = new List<string>();
                foreach (var kv in entry.Data)
                {
                    dataItems.Add($"\"{EscapeJson(kv.Key)}\":{FormatJsonValue(kv.Value)}");
                }
                sb.Append(string.Join(",", dataItems));
                sb.Append("}");
            }

            // 异常信息
            if (entry.Exception != null)
            {
                sb.Append(",\"exception\":{");
                sb.Append($"\"type\":\"{EscapeJson(entry.Exception.GetType().Name)}\",");
                sb.Append($"\"message\":\"{EscapeJson(entry.Exception.Message)}\"");

                if (!string.IsNullOrEmpty(entry.Exception.StackTrace))
                {
                    sb.Append($",\"stack_trace\":\"{EscapeJson(entry.Exception.StackTrace)}\"");
                }

                if (entry.Exception.InnerException != null)
                {
                    sb.Append($",\"inner_exception\":\"{EscapeJson(entry.Exception.InnerException.ToString())}\"");
                }

                sb.Append("}");
            }

            sb.Append("}");
            return sb.ToString();
        }

        private string EscapeJson(string value)
        {
            if (value == null) return string.Empty;
            return value.Replace("\\", "\\\\")
                       .Replace("\"", "\\\"")
                       .Replace("\n", "\\n")
                       .Replace("\r", "\\r")
                       .Replace("\t", "\\t");
        }

        private string FormatJsonValue(object value)
        {
            if (value == null) return "null";
            if (value is string str) return $"\"{EscapeJson(str)}\"";
            if (value is bool b) return b.ToString().ToLower();
            if (value is DateTime dt) return $"\"{dt:yyyy-MM-dd HH:mm:ss.fff}\"";
            if (value is int || value is long || value is short || value is byte)
                return value.ToString();
            if (value is double || value is float || value is decimal)
                return string.Format("{0:F2}", value);
            return $"\"{EscapeJson(value.ToString())}\"";
        }
    }
}

