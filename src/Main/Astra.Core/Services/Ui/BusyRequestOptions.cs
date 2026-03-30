namespace Astra.Core.Services.Ui;

/// <summary>
/// 全局忙碌遮罩行为配置（每次 <see cref="IBusyService.BeginBusy"/> 可单独指定）。
/// </summary>
public sealed class BusyRequestOptions
{
    /// <summary>是否显示取消按钮并发出 <see cref="System.Threading.CancellationToken"/>。</summary>
    public bool AllowCancel { get; init; } = true;

    /// <summary>取消按钮文案。</summary>
    public string CancelButtonText { get; init; } = "取消";

    /// <summary>点击取消前是否弹出确认框。</summary>
    public bool ConfirmBeforeCancel { get; init; }

    /// <summary>确认框内容；为空时使用默认文案。</summary>
    public string? ConfirmCancelMessage { get; init; }
}
