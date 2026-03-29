namespace Astra.Core.Triggers
{
    /// <summary>
    /// 根据触发器配置 Id 在软件配置中解析要执行的主流程路径（宿主实现，可依赖具体 <c>SoftwareConfig</c> 类型）。
    /// </summary>
    public interface ISoftwareConfigAutoTriggerWorkflowResolver
    {
        Task<AutoTriggerWorkflowResolveResult> ResolveAsync(string triggerConfigId, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// <see cref="ISoftwareConfigAutoTriggerWorkflowResolver"/> 的解析结果。
    /// </summary>
    public sealed class AutoTriggerWorkflowResolveResult
    {
        public bool ShouldExecute { get; init; }

        public string? MasterWorkflowFilePath { get; init; }

        /// <summary>当 <see cref="ShouldExecute"/> 为 false 时写入日志的说明（可选）。</summary>
        public string? SkipMessage { get; init; }

        public bool LogSkipAsError { get; init; }
    }
}
