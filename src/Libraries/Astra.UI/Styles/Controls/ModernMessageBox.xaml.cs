using System.Media;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Astra.UI.Styles.Controls
{
    /// <summary>
    /// 现代化消息框 - 完美替代 MessageBox
    /// </summary>
    public partial class ModernMessageBox : Window
    {
        private MessageBoxResult _result = MessageBoxResult.None;

        private ModernMessageBox()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // 播放入场动画
                var storyboard = Resources["ShowAnimation"] as Storyboard;

                if (storyboard != null)
                {
                    storyboard.Begin(this);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ModernMessageBox] 动画播放失败: {ex.Message}");
                // 动画失败也不影响显示
            }
        }

        /// <summary>
        /// 关闭按钮点击事件
        /// </summary>
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            _result = MessageBoxResult.Cancel;
            CloseWithAnimation();
        }

        #region 静态显示方法

        /// <summary>
        /// 显示消息框
        /// </summary>
        public static MessageBoxResult Show(string message)
        {
            return Show(message, "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        /// <summary>
        /// 显示消息框（带标题）
        /// </summary>
        public static MessageBoxResult Show(string message, string caption)
        {
            return Show(message, caption, MessageBoxButton.OK, MessageBoxImage.Information);
        }

        /// <summary>
        /// 显示消息框（带标题和按钮）
        /// </summary>
        public static MessageBoxResult Show(string message, string caption, MessageBoxButton button)
        {
            return Show(message, caption, button, MessageBoxImage.Information);
        }

        /// <summary>
        /// 显示消息框（完整参数）
        /// </summary>
        public static MessageBoxResult Show(string message, string caption, MessageBoxButton button, MessageBoxImage icon)
        {
            MessageBoxResult result = MessageBoxResult.None;

            // ⭐ 检查是否在 UI 线程
            if (Application.Current.Dispatcher.CheckAccess())
            {
                // 已经在 UI 线程，直接执行
                var messageBox = new ModernMessageBox();
                messageBox.SetupContent(message, caption, button, icon);

                // 设置父窗口
                try
                {
                    if (Application.Current?.MainWindow != null && Application.Current.MainWindow.IsLoaded)
                    {
                        messageBox.Owner = Application.Current.MainWindow;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[ModernMessageBox] 设置 Owner 失败: {ex.Message}");
                }

                messageBox.ShowDialog();
                result = messageBox._result;
            }
            else
            {
                // 不在 UI 线程，使用 Invoke 切换
                Application.Current.Dispatcher.Invoke(() =>
                {
                    result = Show(message, caption, button, icon);
                });
            }

            return result;
        }

        #endregion

        #region 内容设置

        private void SetupContent(string message, string caption, MessageBoxButton button, MessageBoxImage icon)
        {
            // 设置标题和消息
            TitleText.Text = caption;
            MessageText.Text = message;

            // 设置图标
            SetupIcon(icon);

            // 设置按钮
            SetupButtons(button);

            // 播放系统声音
            PlaySystemSound(icon);
        }

        private void SetupIcon(MessageBoxImage icon)
        {
            string iconGeometry;
            Brush iconBackground;
            Brush topBarBrush;

            switch (icon)
            {
                case MessageBoxImage.Information:
                    // 信息图标 (i)
                    iconGeometry = "M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm1 15h-2v-6h2v6zm0-8h-2V7h2v2z";
                    iconBackground = (Brush)FindResource("InfoBrush");
                    topBarBrush = (Brush)FindResource("InfoBrush");
                    break;

                case MessageBoxImage.Warning:
                    // 警告图标
                    iconGeometry = "M1 21h22L12 2 1 21zm12-3h-2v-2h2v2zm0-4h-2v-4h2v4z";
                    iconBackground = (Brush)FindResource("WarningBrush");
                    topBarBrush = (Brush)FindResource("WarningBrush");
                    break;

                case MessageBoxImage.Error:
                    // 错误图标
                    iconGeometry = "M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm1 15h-2v-2h2v2zm0-4h-2V7h2v6z";
                    iconBackground = (Brush)FindResource("DangerBrush");
                    topBarBrush = (Brush)FindResource("DangerBrush");
                    break;

                case MessageBoxImage.Question:
                    // 问号图标
                    iconGeometry = "M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm1 17h-2v-2h2v2zm2.07-7.75l-.9.92C13.45 12.9 13 13.5 13 15h-2v-.5c0-1.1.45-2.1 1.17-2.83l1.24-1.26c.37-.36.59-.86.59-1.41 0-1.1-.9-2-2-2s-2 .9-2 2H8c0-2.21 1.79-4 4-4s4 1.79 4 4c0 .88-.36 1.68-.93 2.25z";
                    iconBackground = (Brush)FindResource("PrimaryBrush");
                    topBarBrush = (Brush)FindResource("PrimaryBrush");
                    break;

                default:
                    // 默认信息图标
                    iconGeometry = "M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm1 15h-2v-6h2v6zm0-8h-2V7h2v2z";
                    iconBackground = (Brush)FindResource("InfoBrush");
                    topBarBrush = (Brush)FindResource("InfoBrush");
                    break;
            }

            IconPath.Data = Geometry.Parse(iconGeometry);
            IconContainer.Background = iconBackground;
            TopBar.Background = topBarBrush;

            // 为警告和错误添加脉动动画
            if (icon == MessageBoxImage.Warning || icon == MessageBoxImage.Error)
            {
                try
                {
                    var pulseAnimation = Resources["IconPulseAnimation"] as Storyboard;
                    pulseAnimation?.Begin(IconContainer);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[ModernMessageBox] 脉动动画失败: {ex.Message}");
                }
            }
        }

        private void SetupButtons(MessageBoxButton button)
        {
            ButtonPanel.Children.Clear();

            switch (button)
            {
                case MessageBoxButton.OK:
                    AddButton("取消", MessageBoxResult.Cancel, "SecondaryButtonStyle", false);
                    AddButton("确定", MessageBoxResult.OK, "PrimaryButtonStyle", true);
                    break;

                case MessageBoxButton.OKCancel:
                    AddButton("取消", MessageBoxResult.Cancel, "SecondaryButtonStyle", false);
                    AddButton("确定", MessageBoxResult.OK, "PrimaryButtonStyle", true);
                    break;

                case MessageBoxButton.YesNo:
                    AddButton("否", MessageBoxResult.No, "SecondaryButtonStyle", false);
                    AddButton("是", MessageBoxResult.Yes, "PrimaryButtonStyle", true);
                    break;

                case MessageBoxButton.YesNoCancel:
                    AddButton("取消", MessageBoxResult.Cancel, "SecondaryButtonStyle", false);
                    AddButton("否", MessageBoxResult.No, "SecondaryButtonStyle", false);
                    AddButton("是", MessageBoxResult.Yes, "PrimaryButtonStyle", true);
                    break;
            }
        }

        private void AddButton(string content, MessageBoxResult result, string styleName, bool isDefault)
        {
            var button = new Button
            {
                Content = content,
                Style = (Style)FindResource(styleName),
                MinWidth = 100,
                Margin = new Thickness(6, 0, 6, 0)
            };

            button.Click += (s, e) =>
            {
                _result = result;
                CloseWithAnimation();
            };

            if (isDefault)
            {
                button.IsDefault = true;
                button.Focus();
            }

            ButtonPanel.Children.Add(button);
        }

        private void PlaySystemSound(MessageBoxImage icon)
        {
            switch (icon)
            {
                case MessageBoxImage.Information:
                    SystemSounds.Asterisk.Play();
                    break;
                case MessageBoxImage.Warning:
                    SystemSounds.Exclamation.Play();
                    break;
                case MessageBoxImage.Error:
                    SystemSounds.Hand.Play();
                    break;
                case MessageBoxImage.Question:
                    SystemSounds.Question.Play();
                    break;
            }
        }

        #endregion

        #region 动画控制

        private void CloseWithAnimation()
        {
            try
            {
                var fadeOut = new DoubleAnimation
                {
                    From = 1,
                    To = 0,
                    Duration = TimeSpan.FromSeconds(0.15),
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
                };

                var scaleDown = new DoubleAnimation
                {
                    From = 1,
                    To = 0.9,
                    Duration = TimeSpan.FromSeconds(0.15),
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
                };

                fadeOut.Completed += (s, e) => Close();

                BeginAnimation(OpacityProperty, fadeOut);

                if (RenderTransform is ScaleTransform scaleTransform)
                {
                    scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, scaleDown);
                    scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, scaleDown);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ModernMessageBox] 关闭动画失败: {ex.Message}");
                Close();
            }
        }

        #endregion

        #region 键盘处理

        protected override void OnKeyDown(System.Windows.Input.KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (e.Key == System.Windows.Input.Key.Escape)
            {
                // ESC 键关闭对话框
                var cancelButton = FindCancelButton();
                if (cancelButton != null)
                {
                    _result = MessageBoxResult.Cancel;
                    CloseWithAnimation();
                }
                else
                {
                    _result = MessageBoxResult.None;
                    CloseWithAnimation();
                }
            }
        }

        private Button FindCancelButton()
        {
            foreach (var child in ButtonPanel.Children)
            {
                if (child is Button button && button.Content.ToString() == "取消")
                {
                    return button;
                }
            }
            return null;
        }

        #endregion

        #region 窗口拖动

        /// <summary>
        /// 窗口拖动处理
        /// </summary>
        private void DragArea_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ButtonState == System.Windows.Input.MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        #endregion
    }
}
