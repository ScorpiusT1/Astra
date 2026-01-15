using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Astra.Core.Nodes.Models
{
    /// <summary>
    /// 单个子流程数据模型（用于导入/导出单个子流程）
    /// 符合单一职责原则：专门负责单个子流程数据的序列化结构
    /// </summary>
    public class SingleWorkflowData
    {
        public SingleWorkflowData()
        {
            Version = "1.0";
            CreatedAt = DateTime.Now;
            ModifiedAt = DateTime.Now;
            Workflow = new WorkFlowNode();
            Metadata = new Dictionary<string, object>();
        }

        /// <summary>
        /// 数据格式版本（用于兼容性检查）
        /// </summary>
        [JsonProperty(Order = 1)]
        public string Version { get; set; }

        /// <summary>
        /// 流程名称
        /// </summary>
        [JsonProperty(Order = 2)]
        public string WorkflowName { get; set; }

        /// <summary>
        /// 流程描述
        /// </summary>
        [JsonProperty(Order = 3)]
        public string WorkflowDescription { get; set; }

        /// <summary>
        /// 创建时间
        /// </summary>
        [JsonProperty(Order = 4)]
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// 修改时间
        /// </summary>
        [JsonProperty(Order = 5)]
        public DateTime ModifiedAt { get; set; }

        /// <summary>
        /// 子流程数据
        /// </summary>
        [JsonProperty(Order = 6)]
        public WorkFlowNode Workflow { get; set; }

        /// <summary>
        /// 元数据（用于扩展，存储自定义信息）
        /// </summary>
        [JsonProperty(Order = 7)]
        public Dictionary<string, object> Metadata { get; set; }
    }
}

