using Astra.Core.Nodes.Models;
using Astra.Engine.Execution.WorkFlowEngine;
using System.Threading.Tasks;

namespace Astra.Engine.Execution.Strategies
{
    /// <summary>
    /// 执行策略接口
    /// 定义工作流执行策略的抽象接口，不同策略实现不同的执行逻辑
    /// </summary>
    public interface IExecutionStrategy
    {
        /// <summary>
        /// 执行工作流
        /// </summary>
        /// <param name="context">工作流执行上下文</param>
        /// <returns>执行结果</returns>
        Task<ExecutionResult> ExecuteAsync(WorkFlowExecutionContext context);
    }
}

