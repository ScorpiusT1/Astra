using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Astra.UI.Styles.Windows
{
    /// <summary>
    /// 自定义窗体基类，提供统一的窗口样式和行为
    /// </summary>
    public class BaseWindow : Window
    {
        // 依赖属性：标题栏背景色
        public static readonly DependencyProperty TitleBarBackgroundProperty =
            DependencyProperty.Register("TitleBarBackground", typeof(Brush), typeof(BaseWindow),
                new PropertyMetadata(new SolidColorBrush(Color.FromRgb(245, 245, 245))));

        public Brush TitleBarBackground
        {
            get => (Brush)GetValue(TitleBarBackgroundProperty);
            set => SetValue(TitleBarBackgroundProperty, value);
        }

        // 依赖属性：标题栏前景色
        public static readonly DependencyProperty TitleBarForegroundProperty =
            DependencyProperty.Register("TitleBarForeground", typeof(Brush), typeof(BaseWindow),
                new PropertyMetadata(new SolidColorBrush(Color.FromRgb(51, 51, 51))));

        public Brush TitleBarForeground
        {
            get => (Brush)GetValue(TitleBarForegroundProperty);
            set => SetValue(TitleBarForegroundProperty, value);
        }

        // 依赖属性：边框颜色
        public static readonly DependencyProperty WindowBorderBrushProperty =
            DependencyProperty.Register("WindowBorderBrush", typeof(Brush), typeof(BaseWindow),
                new PropertyMetadata(Brushes.Black));

        public Brush WindowBorderBrush
        {
            get => (Brush)GetValue(WindowBorderBrushProperty);
            set => SetValue(WindowBorderBrushProperty, value);
        }

        // 依赖属性：边框厚度
        public static readonly DependencyProperty WindowBorderThicknessProperty =
            DependencyProperty.Register("WindowBorderThickness", typeof(Thickness), typeof(BaseWindow),
                new PropertyMetadata(new Thickness(2)));

        public Thickness WindowBorderThickness
        {
            get => (Thickness)GetValue(WindowBorderThicknessProperty);
            set => SetValue(WindowBorderThicknessProperty, value);
        }

        // 依赖属性：是否显示图标
        public static readonly DependencyProperty ShowIconProperty =
            DependencyProperty.Register("ShowIcon", typeof(bool), typeof(BaseWindow),
                new PropertyMetadata(false));

        public bool ShowIcon
        {
            get => (bool)GetValue(ShowIconProperty);
            set => SetValue(ShowIconProperty, value);
        }

        static BaseWindow()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(BaseWindow),
                new FrameworkPropertyMetadata(typeof(BaseWindow)));
        }

        public BaseWindow()
        {
            // 设置基本属性
            this.WindowStyle = WindowStyle.None;
            this.AllowsTransparency = true;
            this.Background = Brushes.White; // 重要：设置默认背景为白色
            this.ResizeMode = ResizeMode.CanResize;

            // 监听窗口状态变化
            this.StateChanged += OnWindowStateChanged;
            this.Loaded += OnWindowLoaded;
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            // 获取模板中的控件
            var titleBar = GetTemplateChild("PART_TitleBar") as UIElement;
            var minimizeButton = GetTemplateChild("PART_MinimizeButton") as Button;
            var maximizeButton = GetTemplateChild("PART_MaximizeButton") as Button;
            var closeButton = GetTemplateChild("PART_CloseButton") as Button;

            // 绑定事件
            if (titleBar != null)
            {
                titleBar.MouseLeftButtonDown += TitleBar_MouseLeftButtonDown;
            }

            if (minimizeButton != null)
            {
                minimizeButton.Click += (s, e) => this.WindowState = WindowState.Minimized;
            }

            if (maximizeButton != null)
            {
                maximizeButton.Click += (s, e) => ToggleMaximize();
            }

            if (closeButton != null)
            {
                closeButton.Click += (s, e) => this.Close();
            }
        }

        private void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            // 窗口加载完成后的初始化
            UpdateWindowState();
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                // 双击最大化/还原
                ToggleMaximize();
            }
            else if (e.LeftButton == MouseButtonState.Pressed)
            {
                // 拖动窗口
                try
                {
                    this.DragMove();
                }
                catch
                {
                    // 拖动过程中可能抛出异常，忽略
                }
            }
        }

        private void ToggleMaximize()
        {
            if (this.WindowState == WindowState.Maximized)
            {
                this.WindowState = WindowState.Normal;
            }
            else
            {
                this.WindowState = WindowState.Maximized;
            }
        }

        private void OnWindowStateChanged(object sender, EventArgs e)
        {
            UpdateWindowState();
        }

        private void UpdateWindowState()
        {
            var mainBorder = GetTemplateChild("PART_MainBorder") as Border;
            var maximizeIcon = GetTemplateChild("PART_MaximizeIcon") as Path;
            var maximizeButton = GetTemplateChild("PART_MaximizeButton") as Button;

            if (this.WindowState == WindowState.Maximized)
            {
                // 最大化状态：移除边框
                if (mainBorder != null)
                {
                    mainBorder.BorderThickness = new Thickness(0);
                }

                if (maximizeIcon != null)
                {
                    maximizeIcon.Data = Geometry.Parse("M0,3 H7 V10 H0 V3 M0,4 H7 M3,0 H10 V7 M3,0 V3");
                }

                if (maximizeButton != null)
                {
                    maximizeButton.ToolTip = "还原";
                }
            }
            else
            {
                // 正常状态：恢复边框
                if (mainBorder != null)
                {
                    mainBorder.BorderThickness = WindowBorderThickness;
                }

                if (maximizeIcon != null)
                {
                    maximizeIcon.Data = Geometry.Parse("M0,0 H10 V10 H0 V0 M0,1 H10");
                }

                if (maximizeButton != null)
                {
                    maximizeButton.ToolTip = "最大化";
                }
            }
        }
    }
}

