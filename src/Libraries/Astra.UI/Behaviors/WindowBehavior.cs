using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Astra.UI.Behaviors
{
    public static class WindowBehavior
    {
        #region EnableWindowControls 附加属性

        public static readonly DependencyProperty EnableWindowControlsProperty =
            DependencyProperty.RegisterAttached(
                "EnableWindowControls",
                typeof(bool),
                typeof(WindowBehavior),
                new PropertyMetadata(false, OnEnableWindowControlsChanged));

        public static bool GetEnableWindowControls(DependencyObject obj)
        {
            return (bool)obj.GetValue(EnableWindowControlsProperty);
        }

        public static void SetEnableWindowControls(DependencyObject obj, bool value)
        {
            obj.SetValue(EnableWindowControlsProperty, value);
        }

        private static void OnEnableWindowControlsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is Window window && (bool)e.NewValue)
            {
                window.Loaded += Window_Loaded;
            }
        }

        #endregion

        #region 窗体加载事件

        private static void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is Window window)
            {
                // 查找标题栏
                if (window.Template?.FindName("TitleBar", window) is Border titleBar)
                {
                    titleBar.MouseLeftButtonDown += (s, args) => TitleBar_MouseLeftButtonDown(window, args);
                }

                // 查找最小化按钮
                if (window.Template?.FindName("MinimizeButton", window) is Button minimizeButton)
                {
                    minimizeButton.Click += (s, args) => window.WindowState = WindowState.Minimized;
                }

                // 查找最大化按钮
                if (window.Template?.FindName("MaximizeButton", window) is Button maximizeButton)
                {
                    maximizeButton.Click += (s, args) => ToggleMaximize(window);
                    window.StateChanged += (s, args) => UpdateMaximizeButtonIcon(window, maximizeButton);
                    UpdateMaximizeButtonIcon(window, maximizeButton);
                }

                // 查找关闭按钮
                if (window.Template?.FindName("CloseButton", window) is Button closeButton)
                {
                    closeButton.Click += (s, args) => window.Close();
                }
            }
        }

        #endregion

        #region 窗体操作方法

        private static void TitleBar_MouseLeftButtonDown(Window window, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                ToggleMaximize(window);
            }
            else if (e.LeftButton == MouseButtonState.Pressed)
            {
                window.DragMove();
            }
        }

        private static void ToggleMaximize(Window window)
        {
            window.WindowState = window.WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
        }

        private static void UpdateMaximizeButtonIcon(Window window, Button button)
        {
            // Segoe MDL2 Assets 字体图标
            // E922: 最大化图标
            // E923: 还原图标
            button.Content = window.WindowState == WindowState.Maximized
                ? "\uE923"  // 还原图标
                : "\uE922"; // 最大化图标
        }

        #endregion
    }
}
