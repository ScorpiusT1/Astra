using Astra.Core.Nodes.Models;
using Astra.Engine.Execution.Strategies;
using Astra.Engine.Execution.Validators;

namespace Astra.Engine.Execution.WorkFlowEngine
{
    /// <summary>
    /// 工作流引擎工厂
    /// 提供创建和配置工作流引擎的便捷方法
    /// </summary>
    public static class WorkFlowEngineFactory
    {
        /// <summary>
        /// 创建默认工作流引擎
        /// </summary>
        /// <returns>默认配置的工作流引擎实例</returns>
        public static IWorkFlowEngine CreateDefault()
        {
            return new DefaultWorkFlowEngine();
        }

        /// <summary>
        /// 创建自定义工作流引擎
        /// </summary>
        /// <param name="strategyDetector">策略检测器</param>
        /// <param name="strategyFactory">策略工厂</param>
        /// <param name="validator">工作流验证器</param>
        /// <returns>自定义配置的工作流引擎实例</returns>
        public static IWorkFlowEngine CreateCustom(
            IStrategyDetector strategyDetector,
            IExecutionStrategyFactory strategyFactory,
            IWorkFlowValidator validator)
        {
            return new DefaultWorkFlowEngine(strategyDetector, strategyFactory, validator);
        }
    }
}

