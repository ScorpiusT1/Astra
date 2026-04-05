using Astra.Core.Nodes.Models;
using Astra.Engine.Execution.WorkFlowEngine.Management;
using System;
using System.Collections.Generic;
using System.Threading;

namespace Astra.Engine.Execution.WorkFlowEngine
{
    /// <summary>
    /// 工作流执行上下文
    /// 包含工作流执行所需的所有上下文信息
    /// </summary>
    public class WorkFlowExecutionContext
    {
        /// <summary>
        /// 要执行的工作流节点
        /// </summary>
        public WorkFlowNode Workflow { get; set; }

        /// <summary>
        /// 节点执行上下文
        /// </summary>
        public NodeContext NodeContext { get; set; }

        /// <summary>
        /// 取消令牌
        /// </summary>
        public CancellationToken CancellationToken { get; set; }

        /// <summary>
        /// 执行控制器（支持暂停、恢复、取消）
        /// </summary>
        public WorkFlowExecutionController ExecutionController { get; set; }

        /// <summary>
        /// 本次执行唯一ID
        /// </summary>
        public string ExecutionId { get; set; }

        /// <summary>
        /// 工作流键
        /// </summary>
        public string WorkFlowKey { get; set; }

        /// <summary>
        /// 执行开始时间
        /// </summary>
        public DateTime StartTime { get; set; }

        /// <summary>
        /// 执行统计信息
        /// </summary>
        public WorkFlowExecutionStatistics Statistics { get; set; }

        /// <summary>
        /// 检测到的执行策略
        /// </summary>
        public DetectedExecutionStrategy DetectedStrategy { get; set; }

        /// <summary>
        /// 节点执行开始事件处理器
        /// </summary>
        public Action<Node, NodeContext> OnNodeExecutionStarted { get; set; }

        /// <summary>
        /// 并行层内多节点同时开始：一次回调（项为 <see cref="NodeExecutionEventArgs"/>，仅 Node/Context 有效）。
        /// </summary>
        public Action<IReadOnlyList<NodeExecutionEventArgs>> OnParallelWaveNodesStarting { get; set; }

        /// <summary>
        /// 节点执行完成事件处理器
        /// </summary>
        public Action<Node, NodeContext, ExecutionResult> OnNodeExecutionCompleted { get; set; }

        /// <summary>
        /// 进度变化事件处理器
        /// </summary>
        public Action<int> OnProgressChanged { get; set; }
    }
}

