using Astra.UI.Helpers;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Navigation;
using System.Windows.Threading;

namespace Astra.Bootstrap.UI
{
    /// <summary>
    /// SplashScreenView.xaml 的交互逻辑
    /// </summary>
    public partial class SplashScreenView : Window
    {
        private readonly SplashScreenOptions _options;
        private bool _isCancelled = false;

        public event EventHandler Cancelled;

        public SplashScreenView() : this(new SplashScreenOptions())
        {
        }

        public SplashScreenView(SplashScreenOptions options)
        {
            InitializeComponent();

            _options = options ?? new SplashScreenOptions();
            DataContext = _options;
            ApplyOptions();
            Loaded += OnLoaded;

            // ⭐ 监听窗口尺寸变化，更新裁剪区域
            SizeChanged += OnSizeChanged;
        }

        private void ApplyOptions()
        {
            Width = _options.Width;
            Height = _options.Height;

            if (!_options.AllowCancel)
            {
                CloseButton.Visibility = Visibility.Collapsed;
            }

            if (!string.IsNullOrEmpty(_options.Title))
            {
                TitleText.Text = _options.Title;
            }

            if (!string.IsNullOrEmpty(_options.Subtitle))
            {
                SubtitleText.Text = _options.Subtitle;
            }

            // ⭐ 应用版权信息
            if (!string.IsNullOrEmpty(_options.Copyright))
            {
                CopyrightText.Text = _options.Copyright;
            }
        }

        /// <summary>
        /// ⭐ 处理超链接点击
        /// </summary>
        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = e.Uri.AbsoluteUri,
                    UseShellExecute = true
                });
                e.Handled = true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"打开链接失败: {ex.Message}");
            }
        }

        /// <summary>
        /// ⭐ 窗口加载完成，更新裁剪区域
        /// </summary>
        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            UpdateClipGeometry();
        }

        /// <summary>
        /// ⭐ 窗口尺寸变化时更新裁剪区域
        /// </summary>
        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateClipGeometry();
        }

        /// <summary>
        /// ⭐ 更新裁剪几何体尺寸
        /// </summary>
        private void UpdateClipGeometry()
        {
            
        }

        #region 窗口拖动

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.Source == this && _options.AllowDrag)
            {
                try
                {
                    DragMove();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"拖动异常: {ex.Message}");
                }
            }
        }

        private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_options.AllowDrag)
            {
                try
                {
                    DragMove();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"拖动异常: {ex.Message}");
                }
            }
        }

        #endregion

        #region 关闭按钮

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            if (_options.ConfirmCancel)
            {
                if (!MessageBoxHelper.Confirm(
                    _options.CancelConfirmMessage,
                    "取消确认"))
                {
                    return;
                }
            }

            _isCancelled = true;
            Cancelled?.Invoke(this, EventArgs.Empty);

            StatusText.Text = "正在取消...";
            PercentageText.Text = "已取消";
            PercentageText.SetResourceReference(ForegroundProperty, "ThirdlyTextBrush");
            CloseButton.IsEnabled = false;

            Dispatcher.InvokeAsync(async () =>
            {
                await System.Threading.Tasks.Task.Delay(500);
                CloseWithAnimation();
            });
        }

        public bool IsCancelled => _isCancelled;

        #endregion

        #region 进度更新

        public void UpdateProgress(double percentage, string message, string details)
        {
            if (_isCancelled) return;

            // ⭐ 使用 Invoke 而不是 InvokeAsync，确保更新及时执行并显示
            Dispatcher.Invoke(() =>
            {
                // ⭐ 确保窗口已加载且有实际宽度，否则使用窗口宽度或默认值
                var containerWidth = ActualWidth > 0 ? ActualWidth : Width;
                var progressContainer = ProgressIndicator.Parent as FrameworkElement;
                var containerActualWidth = progressContainer?.ActualWidth ?? containerWidth;
                
                // ⭐ 计算进度条目标宽度（留出左右边距，共约 100 像素）
                var availableWidth = Math.Max(0, containerActualWidth - 100);
                var targetWidth = (percentage / 100.0) * availableWidth;

                // ⭐ 更新进度条宽度（带动画）
                var animation = new DoubleAnimation
                {
                    To = Math.Max(0, targetWidth),
                    Duration = TimeSpan.FromMilliseconds(300),
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                };

                ProgressIndicator.BeginAnimation(WidthProperty, animation);

                // ⭐ 更新百分比文本（确保显示）
                PercentageText.Text = $"{percentage:F0}%";

                // ⭐ 更新状态消息
                if (!string.IsNullOrEmpty(message))
                {
                    StatusText.Text = message;
                }

                // ⭐ 更新详细信息
                if (!string.IsNullOrEmpty(details))
                {
                    DetailsText.Text = details;
                }
                else
                {
                    DetailsText.Text = string.Empty;
                }

                // ⭐ 强制刷新 UI（确保百分比文本立即显示）
                UpdateLayout();
            }, DispatcherPriority.Render);
        }

        public void ShowError(string errorMessage)
        {
            Dispatcher.InvokeAsync(() =>
            {
                StatusText.Text = "启动失败";
                StatusText.SetResourceReference(ForegroundProperty, "DangerBrush");

                PercentageText.Text = "失败";
                PercentageText.SetResourceReference(ForegroundProperty, "DangerBrush");

                DetailsText.Text = errorMessage;
                DetailsText.SetResourceReference(ForegroundProperty, "DangerBrush");

                CloseButton.Visibility = Visibility.Collapsed;

                var storyboard = (Storyboard)FindResource("PulseAnimation");
                storyboard?.Stop(this);
            });
        }

        #endregion

        #region 窗口关闭

        public void CloseWithAnimation()
        {
            var fadeOut = new DoubleAnimation
            {
                From = 1,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(300)
            };

            fadeOut.Completed += (s, e) =>
            {
                try
                {
                    Close();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"关闭窗口异常: {ex.Message}");
                }
            };

            MainBorder.BeginAnimation(OpacityProperty, fadeOut);
        }

        #endregion
    }

    public class SplashScreenOptions
    {
        public string Title { get; set; } = "应用程序正在启动";
        public string Subtitle { get; set; } = "请稍候...";
        public string LogoText { get; set; } = "AS";
        public double Width { get; set; } = 700;
        public double Height { get; set; } = 450;
        public bool AllowCancel { get; set; } = true;
        public bool ConfirmCancel { get; set; } = true;
        public string CancelConfirmMessage { get; set; } = "确定要取消启动吗？";
        public bool AllowDrag { get; set; } = true;

        /// <summary>
        /// ⭐ 版权信息
        /// </summary>
        public string Copyright { get; set; } = $"© {DateTime.Now.Year} Your Company. All rights reserved.";

        /// <summary>
        /// ⭐ 版本号（可选）
        /// </summary>
        public string Version { get; set; } = "v1.0.0";

        /// <summary>
        /// ⭐ 网站链接（可选）
        /// </summary>
        public string Website { get; set; } = "https://www.example.com";
    }
}
