using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Astra.UI.Windows;

/// <summary>
/// 扫码弹窗：适用于扫码枪（键盘楔入）或手动输入，确定后返回内容。
/// </summary>
public partial class ScanCodeDialogWindow : Window
{
    private int _minLen = 1;
    private int _maxLen = int.MaxValue;

    public ScanCodeDialogWindow()
    {
        InitializeComponent();
    }

    /// <summary>
    /// 用户确认后的扫描内容（已 Trim）。仅在 <see cref="ShowDialog"/> 返回 true 时有效。
    /// </summary>
    public string? ScannedText { get; private set; }

    /// <summary>
    /// 设置窗口标题与提示文案（可在 <see cref="ShowDialog"/> 前调用）。
    /// </summary>
    /// <param name="minLength">允许的最小长度（含），默认 1。</param>
    /// <param name="maxLength">允许的最大长度（含），默认不限制。</param>
    public void Configure(string? title = null, string? prompt = null, int minLength = 1, int maxLength = int.MaxValue)
    {
        if (!string.IsNullOrWhiteSpace(title))
            Title = title;

        if (!string.IsNullOrWhiteSpace(prompt))
            PromptTextBlock.Text = prompt;

        _minLen = minLength < 1 ? 1 : minLength;
        _maxLen = maxLength < _minLen ? _minLen : maxLength;

        LengthHintTextBlock.Text = $"长度要求：{_minLen}～{_maxLen} 个字符（当前已输入 0 个）。";
        ValidationTextBlock.Visibility = Visibility.Collapsed;
        ValidationTextBlock.Text = string.Empty;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        ScanTextBox.Focus();
        ScanTextBox.SelectAll();
    }

    private void ScanTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var raw = ScanTextBox.Text ?? string.Empty;
        var t = raw.Trim();
        var len = t.Length;
        LengthHintTextBlock.Text = $"长度要求：{_minLen}～{_maxLen} 个字符（当前已输入 {len} 个）。";

        var ok = len > 0 && len >= _minLen && len <= _maxLen;
        OkButton.IsEnabled = ok;

        if (len == 0)
        {
            ValidationTextBlock.Visibility = Visibility.Collapsed;
            return;
        }

        if (len < _minLen || len > _maxLen)
        {
            ValidationTextBlock.Text = $"条码长度须在 {_minLen}～{_maxLen} 之间，当前为 {len}。";
            ValidationTextBlock.Visibility = Visibility.Visible;
        }
        else
        {
            ValidationTextBlock.Visibility = Visibility.Collapsed;
        }
    }

    private void ScanTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            if (OkButton.IsEnabled)
                CommitOk();
        }
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        CommitOk();
    }

    private void CommitOk()
    {
        var text = ScanTextBox.Text?.Trim();
        if (string.IsNullOrEmpty(text))
            return;

        var len = text.Length;
        if (len < _minLen || len > _maxLen)
            return;

        ScannedText = text;
        DialogResult = true;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void RootBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
            DragMove();
    }
}
