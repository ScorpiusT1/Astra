using Astra.Core.Nodes.Models;
using Astra.Engine.Execution.WorkFlowEngine;
using System.Threading;
using System.Threading.Tasks;

namespace Astra.Engine.Execution.WorkFlowEngine
{
    /// <summary>
    /// 工作流执行扩展方法
    /// 为 WorkFlowNode 类提供便捷的执行方法，避免在 Core 中直接依赖 Engine 的实现
    /// </summary>
    public static class WorkFlowExecutionExtensions
    {
        /// <summary>
        /// 执行工作流 - 使用默认引擎
        /// </summary>
        /// <param name="workflow">要执行的工作流</param>
        /// <param name="context">节点执行上下文</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>执行结果</returns>
        public static async Task<ExecutionResult> ExecuteAsync(
            this WorkFlowNode workflow,
            NodeContext context,
            CancellationToken cancellationToken)
        {
            var engine = WorkFlowEngineFactory.CreateDefault();
            return await engine.ExecuteAsync(workflow, context, cancellationToken);
        }

        /// <summary>
        /// 使用指定引擎执行工作流
        /// </summary>
        /// <param name="workflow">要执行的工作流</param>
        /// <param name="engine">工作流引擎实例</param>
        /// <param name="context">节点执行上下文</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>执行结果</returns>
        public static async Task<ExecutionResult> ExecuteAsync(
            this WorkFlowNode workflow,
            IWorkFlowEngine engine,
            NodeContext context,
            CancellationToken cancellationToken)
        {
            if (engine == null) throw new System.ArgumentNullException(nameof(engine));
            return await engine.ExecuteAsync(workflow, context, cancellationToken);
        }
    }
}

