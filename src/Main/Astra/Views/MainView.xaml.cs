﻿﻿using Astra.UI.Styles.Windows;
using NavStack.Core;
using NavStack.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using HandyControl;
using HandyControl.Controls;
using Astra.ViewModels;

namespace Astra.Views
{
    /// <summary>
    /// MainView.xaml 的交互逻辑
    /// </summary>
    public partial class MainView : Window
    {
        private readonly IFrameNavigationService _navigationService;
        private DateTime _lastActivityResetTime = DateTime.Now;
        private readonly TimeSpan _activityResetInterval = TimeSpan.FromSeconds(1); // 1秒内最多重置一次

        public MainView(MainViewViewModel viewModel, IFrameNavigationService navigationService)
        {
            InitializeComponent();

            _navigationService = navigationService;
            DataContext = viewModel;

            // 在Loaded事件中设置Frame，确保控件已完全初始化
            Loaded += OnLoaded;
        }

        private async void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
        {
            // 确保MainFrame不为null
            if (MainFrame == null)
            {
                System.Diagnostics.Debug.WriteLine("[MainWindow] 错误：MainFrame为null");
                return;
            }

            // 将 Frame 控件连接到导航服务
            _navigationService.Frame = MainFrame;

            System.Diagnostics.Debug.WriteLine($"[MainWindow] Frame 已连接到导航服务: {MainFrame != null}");

            // 初始化导航
            if (DataContext is MainViewViewModel viewModel)
            {
                await viewModel.Navigation.InitializeNavigationAsync();
            }
        }

        protected override void OnClosed(System.EventArgs e)
        {
            try
            {
                // 注意：DataContext 可能已经在 App.xaml.cs 中被设置为 null
                // 所以这里需要检查是否为 null
                if (DataContext is MainViewViewModel vm)
                {
                    vm.Navigation?.Dispose();
                    vm.UserMenu?.Dispose();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MainView.OnClosed] 清理资源时出错: {ex.Message}");
            }
            finally
            {
                base.OnClosed(e);
            }
        }

        #region 全局活动监听 - 重置自动退出计时器

        /// <summary>
        /// 鼠标移动事件 - 重置自动退出计时器
        /// </summary>
        private void MainWindow_MouseMove(object sender, MouseEventArgs e)
        {
            ResetAutoLogoutTimer();
        }

        /// <summary>
        /// 键盘按下事件 - 重置自动退出计时器
        /// </summary>
        private void MainWindow_KeyDown(object sender, KeyEventArgs e)
        {
            ResetAutoLogoutTimer();
        }

        /// <summary>
        /// 鼠标按下事件 - 重置自动退出计时器
        /// </summary>
        private void MainWindow_MouseDown(object sender, MouseButtonEventArgs e)
        {
            ResetAutoLogoutTimer();
        }

        /// <summary>
        /// 鼠标释放事件 - 重置自动退出计时器
        /// </summary>
        private void MainWindow_MouseUp(object sender, MouseButtonEventArgs e)
        {
            ResetAutoLogoutTimer();
        }

        /// <summary>
        /// 鼠标滚轮事件 - 重置自动退出计时器
        /// </summary>
        private void MainWindow_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            ResetAutoLogoutTimer();
        }

        /// <summary>
        /// 重置自动退出计时器（带防抖机制）
        /// </summary>
        private void ResetAutoLogoutTimer()
        {
            var now = DateTime.Now;
            if (now - _lastActivityResetTime >= _activityResetInterval)
            {
                _lastActivityResetTime = now;
                if (DataContext is MainViewViewModel viewModel)
                {
                    // ⭐ 重构后：直接调用 MainViewViewModel 的方法，内部委托给 UserSessionService
                    viewModel.UpdateActivity();
                }
            }
        }

        #endregion
    }
}
