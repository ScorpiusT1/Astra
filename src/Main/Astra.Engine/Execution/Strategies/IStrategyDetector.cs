using Astra.Core.Nodes.Models;
using Astra.Engine.Execution.WorkFlowEngine;

namespace Astra.Engine.Execution.Strategies
{
    /// <summary>
    /// 策略检测器接口
    /// 负责分析工作流结构，检测最适合的执行策略
    /// </summary>
    public interface IStrategyDetector
    {
        /// <summary>
        /// 检测工作流的最佳执行策略
        /// </summary>
        /// <param name="workflow">工作流节点</param>
        /// <returns>检测到的执行策略</returns>
        DetectedExecutionStrategy Detect(WorkFlowNode workflow);
    }
}

