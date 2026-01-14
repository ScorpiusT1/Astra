using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Astra.Core.Nodes.Models
{
    /// <summary>
    /// 流程间连线模型
    /// 连接主流程中的不同子流程节点
    /// 符合单一职责原则：专门负责流程间连线的数据定义
    /// </summary>
    public class WorkflowConnection
    {
        public WorkflowConnection()
        {
            Id = Guid.NewGuid().ToString();
            ParameterMappings = new Dictionary<string, string>();
            Metadata = new Dictionary<string, object>();
        }

        /// <summary>
        /// 连线唯一标识
        /// </summary>
        [JsonPropertyOrder(1)]
        public string Id { get; set; }

        /// <summary>
        /// 源流程ID（子流程ID）
        /// </summary>
        [JsonPropertyOrder(2)]
        public string SourceWorkflowId { get; set; }

        /// <summary>
        /// 目标流程ID（子流程ID）
        /// </summary>
        [JsonPropertyOrder(3)]
        public string TargetWorkflowId { get; set; }

        /// <summary>
        /// 源流程引用节点ID（WorkflowReference.Id）
        /// </summary>
        [JsonPropertyOrder(4)]
        public string SourceReferenceId { get; set; }

        /// <summary>
        /// 目标流程引用节点ID（WorkflowReference.Id）
        /// </summary>
        [JsonPropertyOrder(5)]
        public string TargetReferenceId { get; set; }

        /// <summary>
        /// 连线类型（数据流或控制流）
        /// </summary>
        [JsonPropertyOrder(6)]
        public WorkflowConnectionType Type { get; set; }

        /// <summary>
        /// 参数映射（源流程输出参数 -> 目标流程输入参数）
        /// Key: 源流程输出参数名, Value: 目标流程输入参数名
        /// </summary>
        [JsonPropertyOrder(7)]
        public Dictionary<string, string> ParameterMappings { get; set; }

        /// <summary>
        /// 连线标签（可选）
        /// </summary>
        [JsonPropertyOrder(8)]
        public string Label { get; set; }

        /// <summary>
        /// 元数据（扩展信息）
        /// </summary>
        [JsonPropertyOrder(9)]
        public Dictionary<string, object> Metadata { get; set; }

        /// <summary>
        /// 是否启用
        /// </summary>
        [JsonPropertyOrder(10)]
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// 验证连线
        /// </summary>
        public ValidationResult Validate()
        {
            if (string.IsNullOrEmpty(SourceWorkflowId))
                return ValidationResult.Failure("源流程ID为空");

            if (string.IsNullOrEmpty(TargetWorkflowId))
                return ValidationResult.Failure("目标流程ID为空");

            if (SourceWorkflowId == TargetWorkflowId)
                return ValidationResult.Failure("不能连接到自身流程");

            return ValidationResult.Success();
        }

        /// <summary>
        /// 克隆连线
        /// </summary>
        public WorkflowConnection Clone()
        {
            return new WorkflowConnection
            {
                Id = Guid.NewGuid().ToString(),
                SourceWorkflowId = this.SourceWorkflowId,
                TargetWorkflowId = this.TargetWorkflowId,
                SourceReferenceId = this.SourceReferenceId,
                TargetReferenceId = this.TargetReferenceId,
                Type = this.Type,
                Label = this.Label,
                IsEnabled = this.IsEnabled,
                ParameterMappings = new Dictionary<string, string>(this.ParameterMappings),
                Metadata = new Dictionary<string, object>(this.Metadata)
            };
        }
    }

    /// <summary>
    /// 流程间连线类型
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum WorkflowConnectionType
    {
        /// <summary>
        /// 数据流（传递数据）
        /// </summary>
        DataFlow,

        /// <summary>
        /// 控制流（控制执行顺序）
        /// </summary>
        ControlFlow,

        /// <summary>
        /// 混合（同时传递数据和控制）
        /// </summary>
        Mixed
    }
}

