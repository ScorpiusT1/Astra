using Astra.Core.Nodes.Models;
using System;
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
        /// 节点执行完成事件处理器
        /// </summary>
        public Action<Node, NodeContext, ExecutionResult> OnNodeExecutionCompleted { get; set; }

        /// <summary>
        /// 进度变化事件处理器
        /// </summary>
        public Action<int> OnProgressChanged { get; set; }
    }
}

