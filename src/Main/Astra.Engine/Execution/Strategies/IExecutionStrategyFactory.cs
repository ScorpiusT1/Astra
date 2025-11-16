using Astra.Core.Nodes.Models;
using Astra.Engine.Execution.WorkFlowEngine;

namespace Astra.Engine.Execution.Strategies
{
    /// <summary>
    /// 执行策略工厂接口
    /// 负责创建不同策略类型的实例
    /// </summary>
    public interface IExecutionStrategyFactory
    {
        /// <summary>
        /// 创建指定类型的执行策略
        /// </summary>
        /// <param name="type">执行策略类型</param>
        /// <returns>执行策略实例</returns>
        IExecutionStrategy CreateStrategy(ExecutionStrategyType type);
    }
}

