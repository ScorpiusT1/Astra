using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using Astra.Views;
using Astra.Services.Session;
using Astra.Core.Access.Services;
using Astra.Core.Access;
using Astra.Core.Access.Models;

namespace Astra.Services.Dialogs
{
    /// <summary>
    /// 对话框类型枚举
    /// </summary>
    public enum DialogType
    {
        Login,
        ChangePassword,
        ChangeUserRole
    }

    /// <summary>
    /// 对话框结果
    /// </summary>
    public class DialogResult<T>
    {
        public bool Success { get; set; }
        public T Data { get; set; }
        public string Message { get; set; }
    }

    /// <summary>
    /// 对话框工厂接口 - 工厂模式
    /// </summary>
    public interface IDialogFactory
    {
        /// <summary>
        /// 创建对话框
        /// </summary>
        Window CreateDialog(DialogType dialogType, object parameter = null);
    }

    /// <summary>
    /// 对话框服务接口 - 门面模式
    /// </summary>
    public interface IDialogService
    {
        /// <summary>
        /// 显示登录对话框
        /// </summary>
        DialogResult<User> ShowLoginDialog();

        /// <summary>
        /// 显示修改密码对话框
        /// </summary>
        DialogResult<bool> ShowChangePasswordDialog(string username);
    }

    /// <summary>
    /// 对话框工厂实现
    /// </summary>
    public class DialogFactory : IDialogFactory
    {
        private readonly IUserManagementService _userManagementService;

        public DialogFactory(IUserManagementService userManagementService)
        {
            _userManagementService = userManagementService;
        }

        public Window CreateDialog(DialogType dialogType, object parameter = null)
        {
            return dialogType switch
            {
                DialogType.Login => CreateLoginDialog(),

                DialogType.ChangePassword => new ChangePasswordDialog(_userManagementService)
                {
                    Username = parameter as string,
                    Owner = Application.Current.MainWindow
                },


                _ => throw new ArgumentException($"未知的对话框类型: {dialogType}")
            };
        }

        /// <summary>
        /// 创建登录对话框
        /// </summary>
        private Window CreateLoginDialog()
        {
            var dialog = new LoginDialog(_userManagementService)
            {
                Owner = Application.Current.MainWindow
            };

            // ⭐ 加载用户列表
            if (_userManagementService != null)
            {
                try
                {
                    var users = _userManagementService.GetAllUsers()?.ToList();
                    if (users != null && users.Any())
                    {
                        dialog.AvailableUsers = new ObservableCollection<User>(users);
                    }
                }
                catch (Exception ex)
                {
                    // 静默处理加载用户列表失败的情况
                }
            }

            return dialog;
        }
    }

    /// <summary>
    /// 对话框服务实现
    /// </summary>
    public class DialogService : IDialogService
    {
        private readonly IDialogFactory _dialogFactory;
        private readonly IUserManagementService _userManagementService;
        private readonly IUserSessionService _sessionService;

        public DialogService(
            IDialogFactory dialogFactory,
            IUserManagementService userManagementService,
            IUserSessionService sessionService)
        {
            _dialogFactory = dialogFactory ?? throw new ArgumentNullException(nameof(dialogFactory));
            _userManagementService = userManagementService ?? throw new ArgumentNullException(nameof(userManagementService));
            _sessionService = sessionService ?? throw new ArgumentNullException(nameof(sessionService));
        }

        public DialogResult<User> ShowLoginDialog()
        {
            var dialog = _dialogFactory.CreateDialog(DialogType.Login) as LoginDialog;

            // ⭐ 设置默认选择当前用户
            if (_sessionService.IsLoggedIn && _sessionService.CurrentUser != null)
            {
                dialog.SetDefaultUser(_sessionService.CurrentUser);
            }
            else
            {
                dialog.SetDefaultUser(null);
            }

            var result = new DialogResult<User>();

            try
            {
                if (dialog.ShowDialog() == true)
                {
                    result.Success = true;
                    result.Data = dialog.LoggedInUser;
                    result.Message = $"用户 {dialog.LoggedInUser?.Username} 登录成功";
                }
                else
                {
                    result.Success = false;
                    result.Message = "用户取消登录";
                }

                return result;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = $"登录过程中发生错误: {ex.Message}";
                return result;
            }
        }

        public DialogResult<bool> ShowChangePasswordDialog(string username)
        {
            var dialog = _dialogFactory.CreateDialog(DialogType.ChangePassword, username) as ChangePasswordDialog;

            var result = new DialogResult<bool>();

            if (dialog.ShowDialog() == true)
            {
                // ⭐ 实际执行密码修改
                try
                {
                    var currentUser = _sessionService.CurrentUser;
                    if (currentUser == null)
                    {
                        result.Success = false;
                        result.Data = false;
                        result.Message = "用户未登录";
                        return result;
                    }

                    // 调用服务修改密码
                    _userManagementService.ChangePassword(currentUser, dialog.OldPassword, dialog.NewPassword);

                    result.Success = true;
                    result.Data = true;
                    result.Message = "密码修改成功";
                }
                catch (Exception ex)
                {
                    result.Success = false;
                    result.Data = false;
                    result.Message = $"密码修改失败: {ex.Message}";
                }
            }
            else
            {
                result.Success = false;
                result.Data = false;
                result.Message = "用户取消修改密码";
            }

            return result;
        }
    }
}
