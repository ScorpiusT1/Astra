using Astra.Core.Nodes.Models;
using System;
using System.Collections.Generic;

namespace Astra.Core.Orchestration
{
    public enum SubWorkflowState
    {
        Running,
        Success,
        Failed,
        Skipped,
        Cancelled
    }

    public sealed class SubWorkflowProgressEventArgs : EventArgs
    {
        public string RefId { get; init; } = string.Empty;
        public string DisplayName { get; init; } = string.Empty;
        public SubWorkflowState State { get; init; }
        public ExecutionResult? Result { get; init; }

        /// <summary>
        /// 主流程画布混合执行时：与主流程模型 Id 一致，供首页测试树等用 WorkflowKey 关联插件节点；子流程引用进度为 null。
        /// </summary>
        public string? ScopeWorkflowKey { get; init; }

        /// <summary>
        /// 若非空且含多项，表示同一并行波次内同时进入 <see cref="SubWorkflowState.Running"/>；
        /// 订阅者应在一帧内批量更新 UI。<see cref="RefId"/> / <see cref="DisplayName"/> 仅作兼容占位（通常为首项）。
        /// </summary>
        public IReadOnlyList<SubWorkflowParallelStartItem>? ParallelRunningGroup { get; init; }
    }
}
