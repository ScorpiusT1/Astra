using Astra.Core.Nodes.Models;
using Astra.Engine.Execution.WorkFlowEngine;
using System.Threading;
using System.Threading.Tasks;

namespace Astra.Engine.Execution.NodeExecutor
{
    /// <summary>
    /// 节点执行扩展方法
    /// 为 Node 类提供便捷的执行方法，避免在 Core 中直接依赖 Engine 的实现
    /// </summary>
    public static class NodeExecutionExtensions
    {
        /// <summary>
        /// 执行节点 - 使用默认执行器
        /// 自动识别节点类型，如果是 WorkFlowNode 则使用 WorkFlowNodeExecutor
        /// </summary>
        /// <param name="node">要执行的节点</param>
        /// <param name="context">节点执行上下文</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>执行结果</returns>
        public static async Task<ExecutionResult> ExecuteAsync(
            this Node node,
            NodeContext context,
            CancellationToken cancellationToken)
        {
            // 自动识别节点类型，选择合适的执行器
            INodeExecutor executor;
            if (node is WorkFlowNode)
            {
                // WorkFlowNode 使用专门的执行器
                executor = NodeExecutorFactory.CreateWorkFlowNodeExecutor();
            }
            else
            {
                // 普通节点使用默认执行器
                executor = NodeExecutorFactory.GetDefaultExecutor();
            }
            
            return await executor.ExecuteAsync(node, context, cancellationToken);
        }

        /// <summary>
        /// 使用指定执行器执行节点
        /// </summary>
        /// <param name="node">要执行的节点</param>
        /// <param name="executor">节点执行器实例</param>
        /// <param name="context">节点执行上下文</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>执行结果</returns>
        public static async Task<ExecutionResult> ExecuteAsync(
            this Node node,
            INodeExecutor executor,
            NodeContext context,
            CancellationToken cancellationToken)
        {
            if (executor == null) throw new System.ArgumentNullException(nameof(executor));
            return await executor.ExecuteAsync(node, context, cancellationToken);
        }
    }
}

