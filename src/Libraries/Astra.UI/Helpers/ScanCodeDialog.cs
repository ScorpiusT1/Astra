using System.Windows;
using Astra.UI.Windows;

namespace Astra.UI.Helpers;

/// <summary>
/// 扫码弹窗快捷调用（基于 <see cref="ScanCodeDialogWindow"/>，HandyControl 窗口）。
/// </summary>
public static class ScanCodeDialog
{
    /// <summary>
    /// 模态显示扫码窗口。
    /// </summary>
    /// <param name="owner">父窗口，可为 null（则居中屏幕）</param>
    /// <param name="scannedText">确认后的扫描内容</param>
    /// <param name="title">窗口标题</param>
    /// <param name="prompt">提示说明</param>
    /// <param name="minLength">条码最小长度（字符数，含）</param>
    /// <param name="maxLength">条码最大长度（字符数，含）</param>
    /// <returns>用户点击确定为 true，取消或关闭为 false</returns>
    public static bool Show(
        Window? owner,
        out string? scannedText,
        string? title = null,
        string? prompt = null,
        int minLength = 1,
        int maxLength = int.MaxValue)
    {
        var window = new ScanCodeDialogWindow();
        window.Configure(title, prompt, minLength, maxLength);
        if (owner != null)
            window.Owner = owner;
        else
            window.WindowStartupLocation = WindowStartupLocation.CenterScreen;

        var ok = window.ShowDialog() == true;
        scannedText = ok ? window.ScannedText : null;
        return ok;
    }
}
