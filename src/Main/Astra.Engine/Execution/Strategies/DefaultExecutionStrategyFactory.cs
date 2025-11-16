using Astra.Core.Nodes.Models;
using Astra.Engine.Execution.WorkFlowEngine;
using System;

namespace Astra.Engine.Execution.Strategies
{
    /// <summary>
    /// 默认策略工厂
    /// 根据策略类型创建对应的执行策略实例
    /// </summary>
    public class DefaultExecutionStrategyFactory : IExecutionStrategyFactory
    {
        /// <summary>
        /// 创建指定类型的执行策略
        /// </summary>
        public IExecutionStrategy CreateStrategy(ExecutionStrategyType type)
        {
            return type switch
            {
                ExecutionStrategyType.Parallel => new ParallelExecutionStrategy(),
                ExecutionStrategyType.Sequential => new SequentialExecutionStrategy(),
                ExecutionStrategyType.PartiallyParallel => new PartiallyParallelExecutionStrategy(),
                ExecutionStrategyType.ComplexGraph => new ComplexGraphExecutionStrategy(),
                _ => throw new NotSupportedException($"不支持的执行策略: {type}")
            };
        }
    }
}

