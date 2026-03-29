namespace Astra.Core.Triggers
{
    /// <summary>
    /// 自动触发命中后执行主流程脚本（由 Engine 实现；宿主通过 <see cref="IAutoTriggerHomeRunContext"/> 提供 UI/会话边界）。
    /// </summary>
    public interface IAutoTriggerWorkflowHandler
    {
        Task ExecuteAutoTriggerWorkflowAsync(string masterWorkflowFilePath, string? sn, CancellationToken cancellationToken);
    }
}
