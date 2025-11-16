namespace Astra.Core.Nodes.Models
{
    // ===== 2. 日志条目 =====

    /// <summary>
    /// 日志条目
    /// </summary>
    public class LogEntry
    {
        public LogLevel Level { get; set; }
        public string Message { get; set; }
        public DateTime Timestamp { get; set; }
        public Dictionary<string, object> Data { get; set; }

        public override string ToString()
        {
            return $"[{Level}] {Timestamp:HH:mm:ss.fff} - {Message}";
        }
    }

}
