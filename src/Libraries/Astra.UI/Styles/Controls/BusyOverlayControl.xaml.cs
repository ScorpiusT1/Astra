using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Astra.UI.Styles.Controls;

public partial class BusyOverlayControl : UserControl
{
    public BusyOverlayControl()
    {
        InitializeComponent();
    }

    public static readonly DependencyProperty IsBusyProperty =
        DependencyProperty.Register(nameof(IsBusy), typeof(bool), typeof(BusyOverlayControl), new PropertyMetadata(false));

    public bool IsBusy
    {
        get => (bool)GetValue(IsBusyProperty);
        set => SetValue(IsBusyProperty, value);
    }

    public static readonly DependencyProperty BusyMessageProperty =
        DependencyProperty.Register(nameof(BusyMessage), typeof(string), typeof(BusyOverlayControl), new PropertyMetadata(string.Empty));

    public string BusyMessage
    {
        get => (string)GetValue(BusyMessageProperty);
        set => SetValue(BusyMessageProperty, value);
    }

    public static readonly DependencyProperty ShowCancelButtonProperty =
        DependencyProperty.Register(nameof(ShowCancelButton), typeof(bool), typeof(BusyOverlayControl), new PropertyMetadata(false));

    public bool ShowCancelButton
    {
        get => (bool)GetValue(ShowCancelButtonProperty);
        set => SetValue(ShowCancelButtonProperty, value);
    }

    public static readonly DependencyProperty CancelButtonTextProperty =
        DependencyProperty.Register(nameof(CancelButtonText), typeof(string), typeof(BusyOverlayControl), new PropertyMetadata("取消"));

    public string CancelButtonText
    {
        get => (string)GetValue(CancelButtonTextProperty);
        set => SetValue(CancelButtonTextProperty, value);
    }

    public static readonly DependencyProperty CancelCommandProperty =
        DependencyProperty.Register(nameof(CancelCommand), typeof(ICommand), typeof(BusyOverlayControl), new PropertyMetadata(null));

    public ICommand? CancelCommand
    {
        get => (ICommand?)GetValue(CancelCommandProperty);
        set => SetValue(CancelCommandProperty, value);
    }
}
