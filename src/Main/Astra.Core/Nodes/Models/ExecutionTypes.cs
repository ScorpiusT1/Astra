using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Astra.Core.Nodes.Models
{
    /// <summary>
    /// 检测到的执行策略
    /// 包含策略类型、描述、原因以及相关的节点信息
    /// </summary>
    public class DetectedExecutionStrategy
    {
        /// <summary>
        /// 执行策略类型
        /// </summary>
        public ExecutionStrategyType Type { get; set; }

        /// <summary>
        /// 策略描述
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// 检测到此策略的原因
        /// </summary>
        public string Reason { get; set; }

        /// <summary>
        /// 相关节点列表（用于Sequential和ComplexGraph策略）
        /// </summary>
        public List<Node> Nodes { get; set; }

        /// <summary>
        /// 并行执行组（用于PartiallyParallel策略）
        /// </summary>
        public List<List<Node>> ParallelGroups { get; set; }

        /// <summary>
        /// 节点依赖关系（用于PartiallyParallel策略）
        /// </summary>
        public Dictionary<string, List<string>> Dependencies { get; set; }

        /// <summary>
        /// 预期并行度
        /// </summary>
        public int ExpectedParallelism { get; set; }

        /// <summary>
        /// 是否存在循环依赖
        /// </summary>
        public bool HasCycle { get; set; }
    }

    /// <summary>
    /// 执行策略类型枚举
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum ExecutionStrategyType
    {
        /// <summary>
        /// 并行执行 - 所有节点同时执行
        /// </summary>
        Parallel,

        /// <summary>
        /// 顺序执行 - 节点按顺序逐个执行
        /// </summary>
        Sequential,

        /// <summary>
        /// 部分并行 - 按层并行执行，层间顺序执行
        /// </summary>
        PartiallyParallel,

        /// <summary>
        /// 复杂图 - 使用拓扑排序确定执行顺序
        /// </summary>
        ComplexGraph
    }

    /// <summary>
    /// 工作流执行统计信息
    /// 记录工作流执行的详细统计信息
    /// </summary>
    public class WorkFlowExecutionStatistics
    {
        /// <summary>
        /// 总节点数
        /// </summary>
        public int TotalNodes { get; set; }

        /// <summary>
        /// 已完成的节点数
        /// </summary>
        public int CompletedNodes { get; set; }

        /// <summary>
        /// 失败的节点数
        /// </summary>
        public int FailedNodes { get; set; }

        /// <summary>
        /// 跳过的节点数
        /// </summary>
        public int SkippedNodes { get; set; }

        /// <summary>
        /// 总执行时长
        /// </summary>
        public TimeSpan? TotalDuration { get; set; }

        /// <summary>
        /// 最后的错误信息
        /// </summary>
        public string LastError { get; set; }

        /// <summary>
        /// 使用的执行策略
        /// </summary>
        public string ExecutionStrategy { get; set; }

        /// <summary>
        /// 成功率（百分比）
        /// </summary>
        public double SuccessRate => TotalNodes > 0 ? (CompletedNodes * 100.0 / TotalNodes) : 0;
    }

    /// <summary>
    /// 节点执行事件参数
    /// </summary>
    public class NodeExecutionEventArgs : EventArgs
    {
        /// <summary>
        /// 执行的节点
        /// </summary>
        public Node Node { get; set; }

        /// <summary>
        /// 节点执行上下文
        /// </summary>
        public NodeContext Context { get; set; }

        /// <summary>
        /// 执行结果（执行完成后才有值）
        /// </summary>
        public ExecutionResult Result { get; set; }
    }

    /// <summary>
    /// 进度变化事件参数
    /// </summary>
    public class ProgressChangedEventArgs : EventArgs
    {
        /// <summary>
        /// 进度百分比（0-100）
        /// </summary>
        public int Progress { get; set; }
    }

    /// <summary>
    /// 策略检测事件参数
    /// </summary>
    public class StrategyDetectedEventArgs : EventArgs
    {
        /// <summary>
        /// 检测到的执行策略
        /// </summary>
        public DetectedExecutionStrategy Strategy { get; set; }
    }
}

