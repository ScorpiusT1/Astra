using Astra.Core.Access;
using Astra.UI.Helpers;
using Astra.Services.Dialogs;
using Astra.Services.Session;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Astra.Core.Access.Models;
using NavStack.Services;
using Astra.Utilities;
using System.Windows.Threading;

namespace Astra.ViewModels
{
    /// <summary>
    /// 用户菜单ViewModel - 职责分离后的轻量级ViewModel
    /// </summary>
    public partial class UserMenuViewModel : ObservableObject
    {
        private readonly IUserSessionService _sessionService;
        private readonly IDialogService _dialogService;
        private readonly IMessenger _messenger;
        private readonly INavigationManager _navigationManager;
        private bool _isProcessingSessionChange = false; // ⭐ 防止递归调用标志

        [ObservableProperty]
        private User _currentUser;

        [ObservableProperty]
        private bool _isLoggedIn;

        [ObservableProperty]
        private bool _isMenuOpen;

        public UserMenuViewModel(
            IUserSessionService sessionService,
            IDialogService dialogService,
            IMessenger messenger = null,
            INavigationManager navigationManager = null)
        {
            _sessionService = sessionService ?? throw new ArgumentNullException(nameof(sessionService));
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
            _messenger = messenger ?? WeakReferenceMessenger.Default;
            _navigationManager = navigationManager;

            // 同步会话状态
            SyncSessionState();

            // ⭐ 注册会话变更消息，监听自动登录等事件
            _messenger.Register<UserSessionChangedMessage>(this, OnSessionChanged);

            System.Diagnostics.Debug.WriteLine($"[UserMenuViewModel] 初始化完成，当前状态: 登录={IsLoggedIn}, 用户={CurrentUser?.Username}");
        }

        /// <summary>
        /// 同步会话状态
        /// </summary>
        private void SyncSessionState()
        {
            CurrentUser = _sessionService.CurrentUser;
            IsLoggedIn = _sessionService.IsLoggedIn;
            System.Diagnostics.Debug.WriteLine($"[UserMenuViewModel.SyncSessionState] 同步后: 登录={IsLoggedIn}, 用户={CurrentUser?.Username}");
        }

        /// <summary>
        /// 处理会话变更消息
        /// </summary>
        private void OnSessionChanged(object recipient, UserSessionChangedMessage message)
        {
            // ⭐ 防止递归调用
            if (_isProcessingSessionChange)
            {
                System.Diagnostics.Debug.WriteLine(
                    "[UserMenuViewModel.OnSessionChanged] 检测到递归调用，已阻止");
                return;
            }

            try
            {
                _isProcessingSessionChange = true;

                System.Diagnostics.Debug.WriteLine(
                    $"[UserMenuViewModel.OnSessionChanged] 收到会话变更消息: " +
                    $"用户={message.CurrentUser?.Username ?? "未登录"}, " +
                    $"登录={message.IsLoggedIn}, " +
                    $"原因={message.Reason}");

                // 同步状态
                SyncSessionState();
            }
            finally
            {
                _isProcessingSessionChange = false;
            }
        }

        /// <summary>
        /// 显示登录对话框
        /// </summary>
        [RelayCommand]
        private void ShowLogin()
        {
            var result = _dialogService.ShowLoginDialog();

            if (result.Success)
            {
                // ⭐ 先登录（会触发 UserSessionChangedMessage）
                _sessionService.Login(result.Data);
                // ⭐ 使用 Toast 显示轻量级提示
                ToastHelper.ShowLoginSuccess(result.Data.Username);
            }
        }

        /// <summary>
        /// 切换用户
        /// </summary>
        [RelayCommand]
        private void SwitchUser()
        {
            IsMenuOpen = false;

            var result = _dialogService.ShowLoginDialog();

            if (result.Success)
            {
                // ⭐ 先登录（会触发 UserSessionChangedMessage）
                _sessionService.Login(result.Data);
                // ⭐ 使用 Toast 显示轻量级提示
                ToastHelper.ShowUserSwitched(result.Data.Username);
            }
        }

        /// <summary>
        /// 修改密码
        /// </summary>
        [RelayCommand]
        private void ChangePassword()
        {
            IsMenuOpen = false;

            if (!IsLoggedIn)
            {
                ToastHelper.ShowWarning("请先登录");
                return;
            }

            var result = _dialogService.ShowChangePasswordDialog(CurrentUser.Username);

            if (result.Success)
            {
                ToastHelper.ShowSuccess(result.Message);
            }
        }

        /// <summary>
        /// 登出
        /// </summary>
        [RelayCommand]
        private void Logout()
        {
            System.Diagnostics.Debug.WriteLine($"[UserMenuViewModel.Logout] 当前状态: 登录={IsLoggedIn}, 用户={CurrentUser?.Username}");

            if (!IsLoggedIn)
            {
                ToastHelper.ShowInfo("当前未登录");
                return;
            }

            // ⭐ 先关闭菜单，再显示确认对话框
            IsMenuOpen = false;

            // ⭐ 使用 MessageBoxHelper 显示确认对话框（需要用户确认的场景保留 MessageBox）
            var userName = CurrentUser.Username; // 先保存用户名
            
            // ⭐ 尝试使用 Dispatcher.BeginInvoke 延迟显示确认对话框
            System.Windows.Application.Current.Dispatcher.BeginInvoke(
                System.Windows.Threading.DispatcherPriority.Background,
                new Action(() =>
                {
                    if (!MessageBoxHelper.Confirm(
                        $"确定要退出登录吗?\n当前用户: {userName}",
                        "退出确认"))
                    {
                        return;
                    }

                    // ⭐ 先登出（会触发 UserSessionChangedMessage）
                    _sessionService.Logout();
                    // ⭐ 使用 Toast 显示轻量级提示
                    ToastHelper.ShowLogoutSuccess();
                    // ⭐ 退出登录后，默认导航到首页，并在布局加载后设置选中态
                    try
                    {
                        _ = _navigationManager?.NavigateAsync(NavigationKeys.Home);
                        System.Windows.Application.Current?.Dispatcher.BeginInvoke(
                            System.Windows.Threading.DispatcherPriority.Loaded,
                            new Action(() =>
                            {
                                var mainWindow = System.Windows.Application.Current.MainWindow as Astra.Views.MainView;
                                var vm = mainWindow?.DataContext as MainViewModel;
                                vm?.SetSelectedByKey(NavigationKeys.Home);                                
                            })
                        );
                    }
                    catch { }
                }));
        }

        /// <summary>
        /// 切换菜单显示状态
        /// </summary>
        [RelayCommand]
        private void ToggleMenu()
        {
            IsMenuOpen = !IsMenuOpen;

            // 更新活动时间
            if (IsLoggedIn)
            {
                _sessionService.UpdateActivity();
            }
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            // ⭐ 取消注册消息
            _messenger?.Unregister<UserSessionChangedMessage>(this);
            System.Diagnostics.Debug.WriteLine("[UserMenuViewModel] 资源释放完成");
        }
    }
}
