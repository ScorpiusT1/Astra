using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Astra.Core.Nodes.Models
{
    /// <summary>
    /// 节点端口（纯业务模型）
    /// </summary>
    public class Port
    {
        public Port()
        {
            Id = Guid.NewGuid().ToString();
            Metadata = new Dictionary<string, object>();
        }

        [JsonPropertyOrder(1)]
        public string Id { get; set; }

        [JsonPropertyOrder(2)]
        public string NodeId { get; set; }  // 所属节点ID

        [JsonPropertyOrder(3)]
        public string Name { get; set; }

        [JsonPropertyOrder(4)]
        public string DisplayName { get; set; }  // 显示名称

        [JsonPropertyOrder(5)]
        public PortType Type { get; set; }

        [JsonPropertyOrder(6)]
        public PortDirection Direction { get; set; }

        [JsonPropertyOrder(7)]
        public string DataTypeName { get; set; }  // 数据类型名称（如 "string", "int", "CustomType"）

        [JsonPropertyOrder(8)]
        public bool IsRequired { get; set; }  // 是否必填

        [JsonPropertyOrder(9)]
        public bool AllowMultipleConnections { get; set; }  // 是否允许多个连接

        [JsonPropertyOrder(10)]
        public object DefaultValue { get; set; }  // 默认值

        [JsonPropertyOrder(11)]
        public string Description { get; set; }

        [JsonPropertyOrder(12)]
        public int Order { get; set; }  // 显示顺序

        [JsonPropertyOrder(13)]
        public Dictionary<string, object> Metadata { get; set; }

        // ===== 运行时属性（不序列化） =====

        [JsonIgnore]
        public Node ParentNode { get; set; }  // 父节点引用

        [JsonIgnore]
        public List<Connection> Connections { get; set; } = new List<Connection>();

        // ===== 计算属性 =====

        [JsonIgnore]
        public bool IsConnected => Connections.Any();

        [JsonIgnore]
        public int ConnectionCount => Connections.Count;

        [JsonIgnore]
        public bool CanAcceptMoreConnections
        {
            get
            {
                if (AllowMultipleConnections) return true;
                return ConnectionCount == 0;
            }
        }

        // ===== 方法 =====

        /// <summary>
        /// 验证是否可以连接到目标端口
        /// </summary>
        public ValidationResult CanConnectTo(Port targetPort)
        {
            if (targetPort == null)
                return ValidationResult.Failure("目标端口为空");

            if (this.Id == targetPort.Id)
                return ValidationResult.Failure("不能连接到自身");

            if (this.NodeId == targetPort.NodeId)
                return ValidationResult.Failure("不能连接到同一个节点的端口");

            if (this.Direction == targetPort.Direction)
                return ValidationResult.Failure($"不能连接两个{(Direction == PortDirection.Input ? "输入" : "输出")}端口");

            if (this.Type != targetPort.Type)
                return ValidationResult.Failure($"端口类型不匹配：{this.Type} vs {targetPort.Type}");

            // 数据端口需要检查数据类型兼容性
            if (this.Type == PortType.Data && !string.IsNullOrEmpty(this.DataTypeName) && !string.IsNullOrEmpty(targetPort.DataTypeName))
            {
                if (!IsDataTypeCompatible(this.DataTypeName, targetPort.DataTypeName))
                {
                    return ValidationResult.Failure($"数据类型不兼容：{this.DataTypeName} vs {targetPort.DataTypeName}");
                }
            }

            // 检查是否已存在连接
            var sourcePort = this.Direction == PortDirection.Output ? this : targetPort;
            var destPort = this.Direction == PortDirection.Input ? this : targetPort;

            if (sourcePort.Connections.Any(c => c.TargetPortId == destPort.Id))
                return ValidationResult.Failure("已存在相同的连接");

            // 检查目标端口是否还能接受连接
            if (!destPort.CanAcceptMoreConnections)
                return ValidationResult.Failure("目标端口已达到最大连接数");

            return ValidationResult.Success();
        }

        /// <summary>
        /// 检查数据类型兼容性
        /// </summary>
        private bool IsDataTypeCompatible(string type1, string type2)
        {
            if (type1 == type2) return true;
            if (type1 == "object" || type2 == "object") return true;  // object 类型兼容所有类型

            // 可以扩展更复杂的类型兼容规则
            // 例如：int 可以转换为 double，string 可以转换为 object 等

            return false;
        }

        public Port Clone()
        {
            return new Port
            {
                Id = Guid.NewGuid().ToString(),
                NodeId = this.NodeId,
                Name = this.Name,
                DisplayName = this.DisplayName,
                Type = this.Type,
                Direction = this.Direction,
                DataTypeName = this.DataTypeName,
                IsRequired = this.IsRequired,
                AllowMultipleConnections = this.AllowMultipleConnections,
                DefaultValue = this.DefaultValue,
                Description = this.Description,
                Order = this.Order,
                Metadata = new Dictionary<string, object>(this.Metadata)
            };
        }

        public override string ToString()
        {
            return $"{Name} ({Direction}, {Type})";
        }
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum PortType
    {
        Flow,   // 流程控制端口
        Data    // 数据端口
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum PortDirection
    {
        Input,   // 输入端口
        Output   // 输出端口
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum ConnectionType
    {
        Flow,   // 流程连接
        Data    // 数据连接
    }

}
