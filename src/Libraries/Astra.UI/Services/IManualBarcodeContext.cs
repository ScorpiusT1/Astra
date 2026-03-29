namespace Astra.UI.Services;

/// <summary>
/// Home 手动扫码模式下，在联动执行前暂存条码，供 <see cref="ViewModels.MultiFlowEditorViewModel"/> 构建 <see cref="Astra.Core.Nodes.Models.NodeContext"/> 时写入全局变量 SN。
/// </summary>
public interface IManualBarcodeContext
{
    /// <summary>本次运行开始测试前由 Home 设置；自动模式应为 null。</summary>
    string? PendingBarcode { get; set; }
}
