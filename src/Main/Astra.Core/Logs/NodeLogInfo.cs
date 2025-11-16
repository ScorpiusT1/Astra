namespace Astra.Core.Logs
{
    /// <summary>
    /// 节点日志信息类
    /// 包含节点执行相关的详细信息，用于记录节点执行过程
    /// </summary>
    public class NodeLogInfo
    {
        /// <summary>
        /// 节点唯一标识符
        /// </summary>
        public string NodeId { get; set; }

        /// <summary>
        /// 节点类型
        /// </summary>
        public string NodeType { get; set; }

        /// <summary>
        /// 节点名称
        /// </summary>
        public string NodeName { get; set; }

        /// <summary>
        /// 节点动作（如: Started, Completed, Failed, Info等）
        /// </summary>
        public string Action { get; set; }

        /// <summary>
        /// 节点执行耗时（可选）
        /// </summary>
        public TimeSpan? Duration { get; set; }

        /// <summary>
        /// 节点参数（键值对）
        /// </summary>
        public Dictionary<string, object> Parameters { get; set; }

        /// <summary>
        /// 错误消息（如果节点执行失败）
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// 异常堆栈跟踪信息（如果节点执行失败）
        /// </summary>
        public string StackTrace { get; set; }
    }
}
