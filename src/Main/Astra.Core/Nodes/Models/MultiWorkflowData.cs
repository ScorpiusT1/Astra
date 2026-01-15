using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Astra.Core.Nodes.Models
{
    /// <summary>
    /// 多流程数据模型（用于保存/加载整个多流程项目）
    /// 包含主流程、所有子流程和全局变量
    /// 符合单一职责原则：专门负责多流程数据的序列化结构
    /// </summary>
    public class MultiWorkflowData
    {
        public MultiWorkflowData()
        {
            Version = "1.0";
            CreatedAt = DateTime.Now;
            ModifiedAt = DateTime.Now;
            MasterWorkflow = new MasterWorkflow();
            SubWorkflows = new Dictionary<string, WorkFlowNode>();
            GlobalVariables = new GlobalVariablePool();
            Metadata = new Dictionary<string, object>();
        }

        /// <summary>
        /// 数据格式版本（用于兼容性检查）
        /// </summary>
        [JsonProperty(Order = 1)]
        public string Version { get; set; }

        /// <summary>
        /// 项目名称
        /// </summary>
        [JsonProperty(Order = 2)]
        public string ProjectName { get; set; }

        /// <summary>
        /// 项目描述
        /// </summary>
        [JsonProperty(Order = 3)]
        public string ProjectDescription { get; set; }

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
        /// 主流程数据
        /// </summary>
        [JsonProperty(Order = 6)]
        public MasterWorkflow MasterWorkflow { get; set; }

        /// <summary>
        /// 子流程字典（Key: 子流程ID，Value: 子流程数据）
        /// </summary>
        [JsonProperty(Order = 7)]
        public Dictionary<string, WorkFlowNode> SubWorkflows { get; set; }

        /// <summary>
        /// 全局变量池
        /// </summary>
        [JsonProperty(Order = 8)]
        public GlobalVariablePool GlobalVariables { get; set; }

        /// <summary>
        /// 元数据（用于扩展，存储自定义信息）
        /// </summary>
        [JsonProperty(Order = 9)]
        public Dictionary<string, object> Metadata { get; set; }
    }
}

