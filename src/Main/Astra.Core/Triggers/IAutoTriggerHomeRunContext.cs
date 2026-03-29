namespace Astra.Core.Triggers
{
    /// <summary>
    /// Home 侧与 UI 线程、测试树、会话状态相关的操作，供 Engine 中自动触发执行逻辑调用。
    /// 通常由宿主 Home 视图模型实现。
    /// </summary>
    public interface IAutoTriggerHomeRunContext
    {
        bool IsSequenceLinkageEnabled { get; }

        bool IsExecutionBusy { get; }

        /// <summary>
        /// 在 UI 线程上准备一次与手动「独立执行」一致的会话；若当前不可执行则返回 <see cref="AutoTriggerPrepareResult.Started"/> 为 false。
        /// </summary>
        Task<AutoTriggerPrepareResult> TryPrepareAutoTriggerRunAsync(CancellationToken externalCancellation);

        /// <summary>
        /// 在 UI 线程上结束会话并恢复按钮/计时器状态。
        /// </summary>
        Task CompleteAutoTriggerRunAsync();
    }

    /// <summary>
    /// <see cref="IAutoTriggerHomeRunContext.TryPrepareAutoTriggerRunAsync"/> 的结果。
    /// </summary>
    public readonly struct AutoTriggerPrepareResult
    {
        public bool Started { get; init; }

        /// <summary>
        /// 与外部取消令牌链接后的 CTS；由调用方在流程结束后 <see cref="CancellationTokenSource.Dispose"/>。
        /// </summary>
        public CancellationTokenSource? LinkedCancellation { get; init; }
    }
}
