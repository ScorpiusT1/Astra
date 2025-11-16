using System.Text.Json.Serialization;

namespace Astra.Core.Nodes.Models
{
    // ========================================
    // 业务层：连接（Connection）
    // ========================================

    /// <summary>
    /// 节点连接（纯业务模型）
    /// </summary>
    public class Connection
    {
        public Connection()
        {
            Id = Guid.NewGuid().ToString();
            Metadata = new Dictionary<string, object>();
        }

        [JsonPropertyOrder(1)]
        public string Id { get; set; }

        [JsonPropertyOrder(2)]
        public string SourceNodeId { get; set; }

        [JsonPropertyOrder(3)]
        public string SourcePortId { get; set; }

        [JsonPropertyOrder(4)]
        public string TargetNodeId { get; set; }

        [JsonPropertyOrder(5)]
        public string TargetPortId { get; set; }

        [JsonPropertyOrder(6)]
        public ConnectionType Type { get; set; }

        [JsonPropertyOrder(7)]
        public string Label { get; set; }  // 连接标签（可选）

        [JsonPropertyOrder(8)]
        public Dictionary<string, object> Metadata { get; set; }

        // ===== 运行时属性（不序列化） =====

        [JsonIgnore]
        public Port SourcePort { get; set; }

        [JsonIgnore]
        public Port TargetPort { get; set; }

        [JsonIgnore]
        public Node SourceNode { get; set; }

        [JsonIgnore]
        public Node TargetNode { get; set; }

        [JsonIgnore]
        public object TransferredData { get; set; }  // 运行时传输的数据

        [JsonIgnore]
        public DateTime? LastTransferTime { get; set; }  // 最后一次数据传输时间

        // ===== 计算属性 =====

        [JsonIgnore]
        public bool IsValid => SourcePort != null && TargetPort != null;

        [JsonIgnore]
        public bool IsFlowConnection => Type == ConnectionType.Flow;

        [JsonIgnore]
        public bool IsDataConnection => Type == ConnectionType.Data;

        // ===== 方法 =====

        /// <summary>
        /// 验证连接
        /// </summary>
        public ValidationResult Validate()
        {
            if (string.IsNullOrEmpty(SourceNodeId))
                return ValidationResult.Failure("源节点ID为空");

            if (string.IsNullOrEmpty(SourcePortId))
                return ValidationResult.Failure("源端口ID为空");

            if (string.IsNullOrEmpty(TargetNodeId))
                return ValidationResult.Failure("目标节点ID为空");

            if (string.IsNullOrEmpty(TargetPortId))
                return ValidationResult.Failure("目标端口ID为空");

            if (SourceNodeId == TargetNodeId)
                return ValidationResult.Failure("不能连接到自身节点");

            return ValidationResult.Success();
        }

        /// <summary>
        /// 传输数据（用于数据连接）
        /// </summary>
        public void TransferData(object data)
        {
            TransferredData = data;
            LastTransferTime = DateTime.Now;
        }

        public Connection Clone()
        {
            return new Connection
            {
                Id = Guid.NewGuid().ToString(),
                SourceNodeId = this.SourceNodeId,
                SourcePortId = this.SourcePortId,
                TargetNodeId = this.TargetNodeId,
                TargetPortId = this.TargetPortId,
                Type = this.Type,
                Label = this.Label,
                Metadata = new Dictionary<string, object>(this.Metadata)
            };
        }

        public override string ToString()
        {
            return $"{SourceNodeId}:{SourcePortId} -> {TargetNodeId}:{TargetPortId}";
        }
    }

}
