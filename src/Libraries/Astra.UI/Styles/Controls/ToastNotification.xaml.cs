using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace Astra.UI.Styles.Controls
{
    /// <summary>
    /// Toast 通知类型
    /// </summary>
    public enum ToastType
    {
        Info,
        Success,
        Warning,
        Error
    }

    /// <summary>
    /// Toast 管理器 - 管理多个 Toast 的显示位置
    /// </summary>
    internal static class ToastManager
    {
        private static readonly List<ToastNotification> _activeToasts = new List<ToastNotification>();
        private const double ToastSpacing = 8; // Toast 之间的上下间距
        private const double DefaultToastHeight = 80; // 默认 Toast 高度估算
        private static Window _ownerWindow;
        private static bool _isListeningToOwner = false;

        /// <summary>
        /// 注册新的 Toast
        /// </summary>
        public static void Register(ToastNotification toast)
        {
            lock (_activeToasts)
            {
                _activeToasts.Add(toast);

                // 确保监听主窗口事件
                EnsureOwnerListener();

                // 重新计算所有Toast的位置（使用动画）
                UpdatePositions(useAnimation: true);
            }
        }

        /// <summary>
        /// 取消注册 Toast
        /// </summary>
        public static void Unregister(ToastNotification toast)
        {
            lock (_activeToasts)
            {
                _activeToasts.Remove(toast);
                // Toast移除时，其他Toast位置调整（使用动画）
                UpdatePositions(useAnimation: true);
            }
        }

        /// <summary>
        /// 确保监听Owner窗口的位置变化
        /// </summary>
        private static void EnsureOwnerListener()
        {
            if (_isListeningToOwner || Application.Current?.MainWindow == null)
                return;

            _ownerWindow = Application.Current.MainWindow;
            _ownerWindow.LocationChanged += OwnerWindow_LocationChanged;
            _ownerWindow.SizeChanged += OwnerWindow_SizeChanged;
            _ownerWindow.StateChanged += OwnerWindow_StateChanged;
            _ownerWindow.Activated += OwnerWindow_Activated;
            _isListeningToOwner = true;

            System.Diagnostics.Debug.WriteLine("[ToastManager] 开始监听主窗口位置变化");
        }

        /// <summary>
        /// Owner窗口位置变化时，更新Toast位置
        /// </summary>
        private static void OwnerWindow_LocationChanged(object sender, EventArgs e)
        {
            var window = sender as Window;
            System.Diagnostics.Debug.WriteLine(
                $"[ToastManager] 主窗口位置变化: Left={window?.Left:F2}, Top={window?.Top:F2}, " +
                $"活动Toast数量={_activeToasts.Count}");

            lock (_activeToasts)
            {
                // 主窗口移动时，不使用动画，直接更新位置
                UpdatePositions(useAnimation: false);
            }
        }

        /// <summary>
        /// Owner窗口尺寸变化时，更新Toast位置
        /// </summary>
        private static void OwnerWindow_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            lock (_activeToasts)
            {
                // 主窗口缩放时，不使用动画，直接更新位置
                UpdatePositions(useAnimation: false);
            }
        }

        /// <summary>
        /// Owner窗口状态变化时，更新Toast位置
        /// </summary>
        private static void OwnerWindow_StateChanged(object sender, EventArgs e)
        {
            var window = sender as Window;
            System.Diagnostics.Debug.WriteLine(
                $"[ToastManager] 主窗口状态变化: State={window?.WindowState}, " +
                $"活动Toast数量={_activeToasts.Count}");

            lock (_activeToasts)
            {
                // 窗口状态变化时，延迟更新位置，确保窗口完全恢复
                if (window?.WindowState == WindowState.Normal || window?.WindowState == WindowState.Maximized)
                {
                    // 窗口从最小化恢复到Normal或Maximized时，多次延迟更新位置
                    Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        lock (_activeToasts)
                        {
                            // 第一次更新：立即重置位置
                            ResetAllToastPositions();
                            UpdatePositions(useAnimation: false);
                        }
                    }), System.Windows.Threading.DispatcherPriority.Loaded);

                    // 第二次更新：确保位置正确
                    Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        lock (_activeToasts)
                        {
                            UpdatePositions(useAnimation: false);
                        }
                    }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                }
                else if (window?.WindowState == WindowState.Minimized)
                {
                    // 窗口最小化时，立即隐藏Toast
                    UpdatePositions(useAnimation: false);
                }
                else
                {
                    // 其他状态变化时，立即更新位置
                    UpdatePositions(useAnimation: false);
                }
            }
        }

        /// <summary>
        /// Owner窗口激活时，更新Toast位置
        /// </summary>
        private static void OwnerWindow_Activated(object sender, EventArgs e)
        {
            lock (_activeToasts)
            {
                // 窗口激活时，确保Toast位置正确
                UpdatePositions(useAnimation: false);
            }
        }

        /// <summary>
        /// 重置所有Toast的位置到屏幕顶部
        /// </summary>
        private static void ResetAllToastPositions()
        {
            var screenBounds = SystemParameters.WorkArea;
            double resetTop = screenBounds.Top + 20;

            System.Diagnostics.Debug.WriteLine($"[ToastManager] 重置所有Toast位置到: {resetTop:F2}");

            foreach (var toast in _activeToasts)
            {
                if (toast.IsVisible)
                {
                    toast.Top = resetTop;
                    toast.UpdateHorizontalPosition();
                    System.Diagnostics.Debug.WriteLine($"[ToastManager] 重置Toast位置: Left={toast.Left:F2}, Top={toast.Top:F2}");
                }
            }
        }

        /// <summary>
        /// 更新所有 Toast 的位置（使用屏幕绝对坐标）
        /// </summary>
        /// <param name="useAnimation">是否使用动画</param>
        private static void UpdatePositions(bool useAnimation = false)
        {
            var mainWindow = Application.Current?.MainWindow;
            if (mainWindow == null || !mainWindow.IsLoaded)
            {
                System.Diagnostics.Debug.WriteLine("[ToastManager.UpdatePositions] 主窗口为null或未加载");
                return;
            }

            System.Diagnostics.Debug.WriteLine($"[ToastManager.UpdatePositions] 开始更新位置: " +
                $"WindowState={mainWindow.WindowState}, " +
                $"Left={mainWindow.Left:F2}, Top={mainWindow.Top:F2}, " +
                $"Width={mainWindow.ActualWidth:F2}, Height={mainWindow.ActualHeight:F2}, " +
                $"活动Toast数量={_activeToasts.Count}");

            // ⭐ 检查窗口状态，只有在最小化时才隐藏Toast
            if (mainWindow.WindowState == WindowState.Minimized)
            {
                System.Diagnostics.Debug.WriteLine("[ToastManager.UpdatePositions] 窗口最小化，隐藏所有Toast");
                // 窗口最小化时，隐藏所有Toast
                foreach (var toast in _activeToasts)
                {
                    if (toast.IsVisible)
                    {
                        toast.Hide();
                    }
                }

                return;
            }

            // ⭐ 简化位置计算：使用固定的默认位置
            var screenBounds = SystemParameters.WorkArea;
            double currentTop;
            double maxTop = screenBounds.Bottom - DefaultToastHeight - 20; // 屏幕底部边界

            // ⭐ 根据窗口状态设置固定的起始位置
            if (mainWindow.WindowState == WindowState.Maximized)
            {
                // 最大化时使用固定的默认位置
                currentTop = screenBounds.Top + 80; // 距离屏幕顶部80像素
                System.Diagnostics.Debug.WriteLine($"[ToastManager.UpdatePositions] 最大化窗口，使用固定起始位置: {currentTop:F2}");
            }
            else
            {
                // 非最大化时使用主窗口位置
                currentTop = mainWindow.Top + 60;
                // 确保在屏幕范围内
                double minTop = screenBounds.Top + 20;
                currentTop = Math.Max(minTop, Math.Min(currentTop, maxTop));
                System.Diagnostics.Debug.WriteLine($"[ToastManager.UpdatePositions] 非最大化窗口，使用主窗口位置: {currentTop:F2}");
            }

            System.Diagnostics.Debug.WriteLine($"[ToastManager.UpdatePositions] 计算起始Top位置: {currentTop:F2}, " +
                $"ScreenBounds=({screenBounds.Left:F2}, {screenBounds.Top:F2}, {screenBounds.Right:F2}, {screenBounds.Bottom:F2})");

            foreach (var toast in _activeToasts)
            {
                System.Diagnostics.Debug.WriteLine($"[ToastManager.UpdatePositions] 处理Toast: " +
                    $"IsVisible={toast.IsVisible}, IsLoaded={toast.IsLoaded}, " +
                    $"ActualHeight={toast.ActualHeight:F2}, CurrentTop={toast.Top:F2}");

                // ⭐ 确保Toast在窗口恢复时重新显示
                if (!toast.IsVisible)
                {
                    System.Diagnostics.Debug.WriteLine("[ToastManager.UpdatePositions] Toast不可见，重新显示");
                    toast.Show();
                    // 强制刷新窗口状态
                    toast.UpdateLayout();
                }

                // 使用实际高度或默认高度
                double toastHeight = toast.ActualHeight > 0 ? toast.ActualHeight : DefaultToastHeight;
                System.Diagnostics.Debug.WriteLine($"[ToastManager.UpdatePositions] Toast高度: {toastHeight:F2}");

                // ⭐ 同时更新水平和垂直位置（屏幕绝对坐标）
                toast.UpdateHorizontalPosition();

                // ⭐ 检查垂直位置是否超出屏幕边界
                double finalTop = Math.Min(currentTop, maxTop);
                if (finalTop != currentTop)
                {
                    System.Diagnostics.Debug.WriteLine($"[ToastManager.UpdatePositions] 垂直位置超出屏幕，调整从 {currentTop:F2} 到 {finalTop:F2}");
                }

                // ⭐ 更新垂直位置（屏幕绝对坐标）
                if (toast.IsLoaded && toast.ActualHeight > 0)
                {
                    System.Diagnostics.Debug.WriteLine($"[ToastManager.UpdatePositions] 使用动画更新位置到: {finalTop:F2}");
                    toast.AnimateToPosition(finalTop, useAnimation);
                }
                else
                {
                    // 窗口未加载完成，直接设置位置
                    System.Diagnostics.Debug.WriteLine($"[ToastManager.UpdatePositions] 直接设置位置到: {finalTop:F2}");
                    toast.Top = finalTop;
                }

                currentTop += toastHeight + ToastSpacing;
                System.Diagnostics.Debug.WriteLine($"[ToastManager.UpdatePositions] 下一个Toast的Top位置: {currentTop:F2}");
            }
        }
    }

    /// <summary>
    /// 轻量级浮动提示窗口
    /// </summary>
    public partial class ToastNotification : Window
    {
        private DispatcherTimer _autoCloseTimer;
        private DispatcherTimer _progressTimer;
        private int _durationSeconds;
        private int _elapsedMilliseconds;

        private ToastNotification()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // ⭐⭐ 不设置Owner关系，使用屏幕绝对坐标
                // 通过监听主窗口位置变化来手动更新Toast位置
                System.Diagnostics.Debug.WriteLine($"[Toast] Window_Loaded，不使用Owner关系");

                // ⭐ 注册到管理器（会计算屏幕绝对位置）
                ToastManager.Register(this);

                // ⭐⭐ 延迟定位，等待布局完成后再设置位置
                // 此时ActualWidth/ActualHeight已经有值
                Dispatcher.InvokeAsync(() =>
                {
                    PositionWindow();
                    System.Diagnostics.Debug.WriteLine(
                        $"[Toast] 延迟定位完成: " +
                        $"Left={Left:F2}, Top={Top:F2}, " +
                        $"ActualWidth={ActualWidth:F2}, ActualHeight={ActualHeight:F2}");
                }, DispatcherPriority.Loaded);

                // 播放入场动画
                var slideIn = Resources["SlideInAnimation"] as Storyboard;
                if (slideIn != null)
                {
                    slideIn.Begin();
                }

                // 启动自动关闭计时器
                StartAutoCloseTimer();

                // 启动进度条动画
                StartProgressAnimation();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Toast] Window_Loaded 错误: {ex.Message}\n{ex.StackTrace}");
                // 即使加载失败，也要显示 Toast
                ToastManager.Register(this);
            }
        }

        /// <summary>
        /// MainBorder 加载完成，设置窗口裁剪区域
        /// </summary>
        private void MainBorder_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // ⭐ 设置窗口裁剪区域为圆角矩形，去除四个角的透明区域
                var border = sender as Border;
                if (border != null)
                {
                    var rect = new RectangleGeometry
                    {
                        Rect = new Rect(0, 0, border.ActualWidth, border.ActualHeight),
                        RadiusX = 12,
                        RadiusY = 12
                    };
                    this.Clip = rect;
                }

                // ⭐⭐ 关键：布局完成后重新定位（此时 ActualWidth 已经有值）
                // 使用多次延迟调用，确保布局完全完成
                Dispatcher.InvokeAsync(() =>
                {
                    UpdateHorizontalPosition();
                    System.Diagnostics.Debug.WriteLine(
                        $"[Toast] MainBorder_Loaded 后重新定位: " +
                        $"Left={Left:F2}, ActualWidth={ActualWidth:F2}");
                }, DispatcherPriority.Loaded);

                // ⭐⭐ 额外再次延迟调用，确保处理窗口尺寸变化后的情况
                Dispatcher.InvokeAsync(() =>
                {
                    UpdateHorizontalPosition();
                }, DispatcherPriority.ApplicationIdle);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Toast] MainBorder_Loaded 错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 定位窗口到屏幕右上角
        /// </summary>
        private void PositionWindow()
        {
            UpdateHorizontalPosition();
            // Top 位置由 ToastManager 管理
        }

        /// <summary>
        /// 更新水平位置（使用屏幕绝对坐标）
        /// </summary>
        internal void UpdateHorizontalPosition()
        {
            try
            {
                var mainWindow = Application.Current?.MainWindow;
                if (mainWindow != null && mainWindow.IsLoaded)
                {
                    var toastWidth = ActualWidth;

                    // 如果Toast尚未布局完成，使用估算宽度
                    if (toastWidth < 10)
                    {
                        if (MainBorder != null)
                        {
                            toastWidth = MainBorder.ActualWidth;
                        }
                        if (toastWidth < 10)
                        {
                            toastWidth = 300; // 默认估算宽度
                        }
                    }

                    // ⭐⭐ 简化水平位置计算：使用固定的默认位置
                    var screenBounds = SystemParameters.WorkArea;
                    double calculatedLeft;

                    // ⭐ 根据窗口状态设置固定的水平位置
                    if (mainWindow.WindowState == WindowState.Maximized)
                    {
                        // 最大化时使用固定的屏幕右上角位置
                        calculatedLeft = screenBounds.Right - toastWidth - 20;
                        System.Diagnostics.Debug.WriteLine($"[Toast] 最大化窗口，使用固定水平位置: {calculatedLeft:F2}");
                    }
                    else
                    {
                        // 非最大化时使用主窗口右上角位置
                        calculatedLeft = mainWindow.Left + mainWindow.ActualWidth - toastWidth - 20;
                        // 确保在屏幕范围内
                        double maxLeft = screenBounds.Right - toastWidth - 20;
                        double minLeft = screenBounds.Left + 20;
                        calculatedLeft = Math.Max(minLeft, Math.Min(calculatedLeft, maxLeft));
                        System.Diagnostics.Debug.WriteLine($"[Toast] 非最大化窗口，使用主窗口水平位置: {calculatedLeft:F2}");
                    }

                    // 设置最终位置
                    Left = calculatedLeft;

                    System.Diagnostics.Debug.WriteLine(
                        $"[Toast] UpdateHorizontalPosition: " +
                        $"MainWindow.Left={mainWindow.Left:F2}, " +
                        $"MainWindow.ActualWidth={mainWindow.ActualWidth:F2}, " +
                        $"Toast.ActualWidth={toastWidth:F2}, " +
                        $"Calculated Left={calculatedLeft:F2}, " +
                        $"ScreenBounds=({screenBounds.Left:F2}, {screenBounds.Top:F2}, {screenBounds.Right:F2}, {screenBounds.Bottom:F2}), " +
                        $"Final Left={Left:F2} (屏幕绝对坐标)");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("[Toast] 警告: MainWindow未找到，无法更新水平位置");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Toast] UpdateHorizontalPosition 错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 动画移动到新位置
        /// </summary>
        /// <param name="newTop">新的Top位置</param>
        /// <param name="useAnimation">是否使用动画（主窗口移动时不使用动画）</param>
        internal void AnimateToPosition(double newTop, bool useAnimation = true)
        {
            System.Diagnostics.Debug.WriteLine($"[Toast] AnimateToPosition: " +
                $"CurrentTop={Top:F2}, NewTop={newTop:F2}, UseAnimation={useAnimation}");

            // 如果位置没有变化，不需要动画
            if (Math.Abs(Top - newTop) < 1)
            {
                System.Diagnostics.Debug.WriteLine("[Toast] AnimateToPosition: 位置无变化，跳过");
                return;
            }

            try
            {
                // ⭐ 停止之前的动画
                BeginAnimation(TopProperty, null);

                if (useAnimation)
                {
                    System.Diagnostics.Debug.WriteLine("[Toast] AnimateToPosition: 使用动画");
                    // 使用动画
                    var animation = new DoubleAnimation
                    {
                        From = Top,
                        To = newTop,
                        Duration = TimeSpan.FromMilliseconds(300),
                        EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                    };

                    animation.Completed += (s, e) =>
                    {
                        System.Diagnostics.Debug.WriteLine($"[Toast] AnimateToPosition: 动画完成，最终Top={Top:F2}");
                    };

                    BeginAnimation(TopProperty, animation);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("[Toast] AnimateToPosition: 直接设置位置");
                    // 直接设置位置（跟随窗口移动时不使用动画）
                    Top = newTop;
                }
            }
            catch (Exception ex)
            {
                // 动画失败时直接设置位置
                System.Diagnostics.Debug.WriteLine($"[Toast] AnimateToPosition: 动画失败: {ex.Message}");
                Top = newTop;
            }
        }

        /// <summary>
        /// 启动自动关闭计时器
        /// </summary>
        private void StartAutoCloseTimer()
        {
            _autoCloseTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(_durationSeconds)
            };
            _autoCloseTimer.Tick += (s, e) =>
            {
                _autoCloseTimer.Stop();
                _progressTimer?.Stop();
                CloseWithAnimation();
            };
            _autoCloseTimer.Start();
        }

        /// <summary>
        /// 启动进度条动画
        /// </summary>
        private void StartProgressAnimation()
        {
            _progressTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(50)
            };

            _progressTimer.Tick += (s, e) =>
            {
                _elapsedMilliseconds += 50;
                var progress = (double)_elapsedMilliseconds / (_durationSeconds * 1000);
                ProgressBar.Width = ActualWidth * progress;
            };

            _progressTimer.Start();
        }

        /// <summary>
        /// 关闭按钮点击
        /// </summary>
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            _autoCloseTimer?.Stop();
            _progressTimer?.Stop();
            CloseWithAnimation();
        }

        /// <summary>
        /// 带动画关闭
        /// </summary>
        private void CloseWithAnimation()
        {
            try
            {
                var slideOut = Resources["SlideOutAnimation"] as Storyboard;
                if (slideOut != null)
                {
                    slideOut.Begin();
                }
                else
                {
                    // 没有动画时直接关闭
                    ToastManager.Unregister(this);
                    Close();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Toast] CloseWithAnimation 错误: {ex.Message}");
                ToastManager.Unregister(this);
                Close();
            }
        }

        /// <summary>
        /// 退场动画完成
        /// </summary>
        private void SlideOutAnimation_Completed(object sender, EventArgs e)
        {
            // 从管理器取消注册
            ToastManager.Unregister(this);
            Close();
        }

        #region 静态显示方法

        /// <summary>
        /// 显示信息提示
        /// </summary>
        public static void ShowInfo(string message, string title = "提示", int duration = 3)
        {
            Show(message, title, ToastType.Info, duration);
        }

        /// <summary>
        /// 显示成功提示
        /// </summary>
        public static void ShowSuccess(string message, string title = "成功", int duration = 3)
        {
            Show(message, title, ToastType.Success, duration);
        }

        /// <summary>
        /// 显示警告提示
        /// </summary>
        public static void ShowWarning(string message, string title = "警告", int duration = 3)
        {
            Show(message, title, ToastType.Warning, duration);
        }

        /// <summary>
        /// 显示错误提示
        /// </summary>
        public static void ShowError(string message, string title = "错误", int duration = 4)
        {
            Show(message, title, ToastType.Error, duration);
        }

        /// <summary>
        /// 显示 Toast 通知
        /// </summary>
        private static void Show(string message, string title, ToastType type, int duration)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                // ⭐ 根据消息长度自动调整显示时长
                int calculatedDuration = CalculateDuration(message, title, duration);

                var toast = new ToastNotification
                {
                    _durationSeconds = calculatedDuration
                };

                toast.TitleText.Text = title;
                toast.MessageText.Text = message;

                // 根据类型设置样式
                toast.SetupStyle(type);

                toast.Show();

                System.Diagnostics.Debug.WriteLine($"[Toast] Show完成，使用屏幕绝对坐标");
            });
        }

        /// <summary>
        /// 根据文本长度计算显示时长
        /// </summary>
        private static int CalculateDuration(string message, string title, int baseDuration)
        {
            // 计算总字符数
            int totalLength = (title?.Length ?? 0) + (message?.Length ?? 0);

            // 每 30 个字增加 1 秒，最少 baseDuration 秒，最多 10 秒
            int calculatedDuration = baseDuration + (totalLength / 30);

            // 限制在合理范围内
            return Math.Max(baseDuration, Math.Min(calculatedDuration, 10));
        }

        #endregion

        #region 样式设置

        /// <summary>
        /// 设置 Toast 样式
        /// </summary>
        private void SetupStyle(ToastType type)
        {
            string iconGeometry;
            Color iconColor;
            Color backgroundColor;

            switch (type)
            {
                case ToastType.Success:
                    // 成功图标 ✓
                    iconGeometry = "M9 16.17L4.83 12l-1.42 1.41L9 19 21 7l-1.41-1.41z";
                    iconColor = (Color)FindResource("SuccessColor");
                    backgroundColor = iconColor;
                    break;

                case ToastType.Warning:
                    // 警告图标 ⚠
                    iconGeometry = "M1 21h22L12 2 1 21zm12-3h-2v-2h2v2zm0-4h-2v-4h2v4z";
                    iconColor = (Color)FindResource("WarningColor");
                    backgroundColor = iconColor;
                    break;

                case ToastType.Error:
                    // 错误图标 ✗
                    iconGeometry = "M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm1 15h-2v-2h2v2zm0-4h-2V7h2v6z";
                    iconColor = (Color)FindResource("DangerColor");
                    backgroundColor = iconColor;
                    break;

                case ToastType.Info:
                default:
                    // 信息图标 ℹ
                    iconGeometry = "M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm1 15h-2v-6h2v6zm0-8h-2V7h2v2z";
                    iconColor = (Color)FindResource("InfoColor");
                    backgroundColor = iconColor;
                    break;
            }

            // 设置图标
            IconPath.Data = Geometry.Parse(iconGeometry);
            IconPath.Fill = new SolidColorBrush(iconColor);

            // 设置图标背景
            IconBackgroundBrush.Color = backgroundColor;

            // 设置左侧彩色边条
            AccentBarBrush.Color = iconColor;

            // 设置进度条颜色
            ProgressBar.Background = new SolidColorBrush(iconColor);
        }

        #endregion
    }
}
