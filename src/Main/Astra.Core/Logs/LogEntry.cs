namespace Astra.Core.Logs
{
    /// <summary>
    /// 日志条目类
    /// 表示一条完整的日志记录，包含时间戳、级别、分类、消息等信息
    /// </summary>
    public class LogEntry
    {
        /// <summary>
        /// 日志时间戳
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// 日志级别（字符串形式，如"DEBUG"、"INFO"等）
        /// </summary>
        public string Level { get; set; }

        /// <summary>
        /// 日志分类
        /// </summary>
        public LogCategory Category { get; set; }

        /// <summary>
        /// 日志器名称（标识日志来源）
        /// </summary>
        public string Logger { get; set; }

        /// <summary>
        /// 日志消息内容
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// 附加的结构化数据（键值对）
        /// </summary>
        public Dictionary<string, object> Data { get; set; }

        /// <summary>
        /// 关联的异常对象（如果有）
        /// </summary>
        public Exception Exception { get; set; }

        /// <summary>
        /// 节点日志信息（如果这是节点相关的日志）
        /// </summary>
        public NodeLogInfo NodeInfo { get; set; }

        /// <summary>
        /// 是否触发UI更新事件
        /// 用于控制是否将日志推送到UI界面显示
        /// </summary>
        public bool TriggerUIEvent { get; set; }
    }
}
