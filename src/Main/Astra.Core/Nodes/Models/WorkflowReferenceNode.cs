using Astra.Core.Nodes.Geometry;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Astra.Core.Nodes.Models
{
    /// <summary>
    /// 流程引用节点
    /// 在主流程画布上代表子流程的节点，可以连接和配置
    /// 符合单一职责原则：专门负责流程引用的可视化表示
    /// 符合里氏替换原则：继承自 Node，可以替换基类使用
    /// </summary>
    public class WorkflowReferenceNode : Node
    {
        public WorkflowReferenceNode()
        {
            NodeType = "WorkflowReferenceNode";
            Name = "流程引用";
            Icon = "📋";
            Size = new Size2D(200, 150);
            
            // 初始化输入输出端口
            InitializePorts();
        }

        /// <summary>
        /// 引用的子流程ID
        /// </summary>
        public string SubWorkflowId { get; set; }

        /// <summary>
        /// 引用的子流程名称（缓存，避免频繁查找）
        /// </summary>
        public string SubWorkflowName { get; set; }

        /// <summary>
        /// 输入参数映射（子流程输入参数 -> 主流程变量或常量）
        /// </summary>
        public Dictionary<string, string> InputParameterMapping { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// 输出参数映射（子流程输出参数 -> 主流程变量）
        /// </summary>
        public Dictionary<string, string> OutputParameterMapping { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// 初始化端口（为流程引用节点创建输入和输出端口）
        /// </summary>
        private void InitializePorts()
        {
            // 输入端口（用于接收数据）
            InputPorts.Add(new Port
            {
                Name = "Input",
                DisplayName = "输入",
                Type = PortType.Data,
                Direction = PortDirection.Input,
                AllowMultipleConnections = true,
                Description = "接收来自其他流程的数据"
            });

            // 输出端口（用于发送数据）
            OutputPorts.Add(new Port
            {
                Name = "Output",
                DisplayName = "输出",
                Type = PortType.Data,
                Direction = PortDirection.Output,
                AllowMultipleConnections = true,
                Description = "向其他流程发送数据"
            });

            // 流程控制端口
            InputPorts.Add(new Port
            {
                Name = "FlowIn",
                DisplayName = "流程输入",
                Type = PortType.Flow,
                Direction = PortDirection.Input,
                AllowMultipleConnections = false,
                Description = "流程执行入口"
            });

            OutputPorts.Add(new Port
            {
                Name = "FlowOut",
                DisplayName = "流程输出",
                Type = PortType.Flow,
                Direction = PortDirection.Output,
                AllowMultipleConnections = true,
                Description = "流程执行完成"
            });
        }

        /// <summary>
        /// 执行流程引用节点（调用子流程）
        /// 符合依赖倒置原则：通过反射或依赖注入调用子流程执行器
        /// </summary>
        protected override async Task<ExecutionResult> ExecuteCoreAsync(NodeContext context, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(SubWorkflowId))
            {
                return ExecutionResult.Failed("子流程ID为空");
            }

            // 这里应该通过 WorkFlowManager 查找并执行子流程
            // 为了保持架构清晰，使用反射或依赖注入获取 WorkFlowManager
            // 实际实现应该在 Engine 层完成

            // 临时实现：返回成功（实际执行逻辑在 Engine 层）
            return ExecutionResult.Successful("流程引用节点执行成功（实际执行在 Engine 层）");
        }

        /// <summary>
        /// 克隆流程引用节点
        /// </summary>
        public override Node Clone()
        {
            var cloned = new WorkflowReferenceNode
            {
                SubWorkflowId = this.SubWorkflowId,
                SubWorkflowName = this.SubWorkflowName,
                InputParameterMapping = new Dictionary<string, string>(this.InputParameterMapping),
                OutputParameterMapping = new Dictionary<string, string>(this.OutputParameterMapping)
            };

            // 调用基类克隆方法复制基本属性
            cloned.Id = Guid.NewGuid().ToString();
            cloned.Name = this.Name;
            cloned.Description = this.Description;
            cloned.Icon = this.Icon;
            cloned.Color = this.Color;
            cloned.Position = this.Position;
            cloned.Size = this.Size;
            cloned.IsEnabled = this.IsEnabled;

            return cloned;
        }
    }
}




















