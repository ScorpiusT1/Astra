using Astra.Core.Nodes.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace Astra.Core.Nodes.Models
{
    /// <summary>
    /// 主流程数据模型
    /// 包含多个子流程引用和流程间连线
    /// 符合单一职责原则：专门负责主流程的数据组织
    /// </summary>
    public class MasterWorkflow
    {
        public MasterWorkflow()
        {
            Id = Guid.NewGuid().ToString();
            Name = "主流程";
            SubWorkflowReferences = new List<WorkflowReference>();
            Edges = new List<Edge>();
            CreatedAt = DateTime.Now;
            ModifiedAt = DateTime.Now;
        }

        /// <summary>
        /// 主流程唯一标识
        /// </summary>
        [JsonPropertyOrder(1)]
        public string Id { get; set; }

        /// <summary>
        /// 主流程名称
        /// </summary>
        [JsonPropertyOrder(2)]
        public string Name { get; set; }

        /// <summary>
        /// 主流程描述
        /// </summary>
        [JsonPropertyOrder(3)]
        public string Description { get; set; }

        /// <summary>
        /// 子流程引用列表（在主流程画布上显示的流程节点）
        /// Key: WorkFlowNode.Id (子流程ID)
        /// </summary>
        [JsonPropertyOrder(4)]
        public List<WorkflowReference> SubWorkflowReferences { get; set; }

        /// <summary>
        /// 流程间连线列表（连接不同的子流程节点）
        /// 统一使用 Edge 类，与画布显示保持一致
        /// </summary>
        [JsonPropertyOrder(5)]
        public List<Edge> Edges { get; set; }

        /// <summary>
        /// 创建时间
        /// </summary>
        [JsonPropertyOrder(6)]
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// 修改时间
        /// </summary>
        [JsonPropertyOrder(7)]
        public DateTime ModifiedAt { get; set; }

        /// <summary>
        /// 文件路径（如果已保存）
        /// </summary>
        [JsonPropertyOrder(8)]
        public string FilePath { get; set; }

        /// <summary>
        /// 是否已修改（未保存）
        /// </summary>
        [JsonIgnore]
        public bool IsModified { get; set; }

        /// <summary>
        /// 添加子流程引用
        /// </summary>
        public void AddSubWorkflowReference(WorkflowReference reference)
        {
            if (reference == null)
                throw new ArgumentNullException(nameof(reference));

            if (SubWorkflowReferences.Any(r => r.SubWorkflowId == reference.SubWorkflowId))
                throw new InvalidOperationException($"子流程 {reference.SubWorkflowId} 已存在");

            SubWorkflowReferences.Add(reference);
            ModifiedAt = DateTime.Now;
            IsModified = true;
        }

        /// <summary>
        /// 移除子流程引用
        /// </summary>
        public bool RemoveSubWorkflowReference(string subWorkflowId)
        {
            var reference = SubWorkflowReferences.FirstOrDefault(r => r.SubWorkflowId == subWorkflowId);
            if (reference == null)
                return false;

            // 移除相关的连线（通过节点ID查找）
            // 需要先找到对应的节点ID，然后移除连线
            var referenceId = reference.Id;
            Edges.RemoveAll(e => 
                e.SourceNodeId == referenceId || 
                e.TargetNodeId == referenceId);

            SubWorkflowReferences.Remove(reference);
            ModifiedAt = DateTime.Now;
            IsModified = true;
            return true;
        }

        /// <summary>
        /// 添加流程间连线
        /// </summary>
        public void AddEdge(Edge edge)
        {
            if (edge == null)
                throw new ArgumentNullException(nameof(edge));

            // 验证源和目标节点是否存在（通过引用ID查找）
            if (!SubWorkflowReferences.Any(r => r.Id == edge.SourceNodeId))
                throw new InvalidOperationException($"源节点 {edge.SourceNodeId} 不存在");

            if (!SubWorkflowReferences.Any(r => r.Id == edge.TargetNodeId))
                throw new InvalidOperationException($"目标节点 {edge.TargetNodeId} 不存在");

            Edges.Add(edge);
            ModifiedAt = DateTime.Now;
            IsModified = true;
        }

        /// <summary>
        /// 移除流程间连线
        /// </summary>
        public bool RemoveEdge(string edgeId)
        {
            var edge = Edges.FirstOrDefault(e => e.Id == edgeId);
            if (edge == null)
                return false;

            Edges.Remove(edge);
            ModifiedAt = DateTime.Now;
            IsModified = true;
            return true;
        }

        /// <summary>
        /// 获取子流程引用
        /// </summary>
        public WorkflowReference GetSubWorkflowReference(string subWorkflowId)
        {
            return SubWorkflowReferences.FirstOrDefault(r => r.SubWorkflowId == subWorkflowId);
        }
    }

    /// <summary>
    /// 子流程引用（在主流程画布上显示的流程节点信息）
    /// </summary>
    public class WorkflowReference
    {
        public WorkflowReference()
        {
            Id = Guid.NewGuid().ToString();
            Position = new Point2D(0, 0);
            Size = new Size2D(200, 150);
        }

        /// <summary>
        /// 引用节点唯一标识
        /// </summary>
        [JsonPropertyOrder(1)]
        public string Id { get; set; }

        /// <summary>
        /// 引用的子流程ID
        /// </summary>
        [JsonPropertyOrder(2)]
        public string SubWorkflowId { get; set; }

        /// <summary>
        /// 在主流程画布上的位置
        /// </summary>
        [JsonPropertyOrder(3)]
        public Point2D Position { get; set; }

        /// <summary>
        /// 在主流程画布上的大小
        /// </summary>
        [JsonPropertyOrder(4)]
        public Size2D Size { get; set; }

        /// <summary>
        /// 显示名称（可以覆盖子流程的名称）
        /// </summary>
        [JsonPropertyOrder(5)]
        public string DisplayName { get; set; }

        /// <summary>
        /// 参数映射（子流程输入参数 -> 主流程变量或常量）
        /// </summary>
        [JsonPropertyOrder(6)]
        public Dictionary<string, string> InputParameterMapping { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// 输出参数映射（子流程输出参数 -> 主流程变量）
        /// </summary>
        [JsonPropertyOrder(7)]
        public Dictionary<string, string> OutputParameterMapping { get; set; } = new Dictionary<string, string>();
    }
}

