namespace Astra.Core.Orchestration
{
    /// <summary>
    /// 同一拓扑层/并行波次内同时进入 Running 的项，供 UI 单次批量刷新。
    /// </summary>
    public sealed class SubWorkflowParallelStartItem
    {
        public string RefId { get; init; } = string.Empty;

        public string DisplayName { get; init; } = string.Empty;

        /// <summary>
        /// 为 true 表示主流程混合画布上的插件节点（此时进度事件应带 <see cref="SubWorkflowProgressEventArgs.ScopeWorkflowKey"/>）。
        /// </summary>
        public bool IsHybridMasterPlugin { get; init; }
    }
}
