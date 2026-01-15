using Astra.ViewModels;
using Astra.Core.Access;
using CommunityToolkit.Mvvm.Messaging;
using System.Windows;
using System.Windows.Controls;
using Astra.Messages;
using System.Threading.Tasks;
using Astra.Core.Access.Models;
using Astra.UI.Helpers;

namespace Astra.Views
{
    /// <summary>
    /// PermissionView.xaml 的交互逻辑
    /// </summary>
    public partial class PermissionView : UserControl
    {
        private readonly PermissionViewModel _viewModel;

        public PermissionView(PermissionViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            DataContext = _viewModel;
        }

        private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (DataContext is PermissionViewModel viewModel)
            {
                var passwordBox = sender as PasswordBox;
                viewModel.NewPassword = passwordBox?.Password;

                // 如果ViewModel的密码被清空了，也清空PasswordBox
                if (string.IsNullOrEmpty(viewModel.NewPassword) && !string.IsNullOrEmpty(passwordBox?.Password))
                {
                    passwordBox.Clear();
                }
            }
        }

        private void RoleComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DataContext is PermissionViewModel viewModel && sender is ComboBox comboBox)
            {
                // 获取当前编辑的用户
                var user = comboBox.DataContext as User;

                if (user == null)
                {
                    return;
                }

                // 获取当前登录用户
                var currentUser = viewModel.GetCurrentUser();

                // 不能修改超级管理员
                if (user.Role == UserRole.SuperAdministrator)
                {
                    // 恢复到超级管理员角色
                    comboBox.SelectedValue = UserRole.SuperAdministrator;
                    ToastHelper.ShowWarning("不能修改超级管理员的角色");
                    return;
                }

                // 管理员只能修改工程师和操作员的角色
                if (currentUser?.Role == UserRole.Administrator && user.Role == UserRole.Administrator)
                {
                    var previousRole = comboBox.Tag != null && comboBox.Tag is UserRole 
                        ? (UserRole)comboBox.Tag 
                        : user.Role;
                    comboBox.SelectedValue = previousRole;
                    ToastHelper.ShowWarning("管理员只能修改工程师和操作员的角色");
                    return;
                }

                // 获取新选择的角色
                var newRole = (UserRole)comboBox.SelectedValue;

                // 不能修改为超级管理员
                if (newRole == UserRole.SuperAdministrator)
                {
                    // 恢复到原始角色
                    var previousRole = comboBox.Tag != null && comboBox.Tag is UserRole 
                        ? (UserRole)comboBox.Tag 
                        : user.Role;
                    comboBox.SelectedValue = previousRole;
                    ToastHelper.ShowWarning("不能通过界面将用户修改为超级管理员");
                    return;
                }

                // 管理员不能将用户修改为管理员
                if (currentUser?.Role == UserRole.Administrator && newRole == UserRole.Administrator)
                {
                    var previousRole = comboBox.Tag != null && comboBox.Tag is UserRole 
                        ? (UserRole)comboBox.Tag 
                        : user.Role;
                    comboBox.SelectedValue = previousRole;
                    ToastHelper.ShowWarning("管理员只能将用户修改为工程师或操作员");
                    return;
                }

                // 从Tag中获取原始角色，如果Tag为空则使用当前用户角色
                UserRole originalRole;
                if (comboBox.Tag != null && comboBox.Tag is UserRole)
                {
                    originalRole = (UserRole)comboBox.Tag;
                }
                else
                {
                    // 如果Tag为空，说明GotFocus事件没有触发，使用当前用户角色作为原始角色
                    originalRole = user.Role;
                    comboBox.Tag = originalRole; // 设置Tag以备后续使用            
                }

                // 验证角色变更是否安全（使用原始角色进行比较）
                if (!viewModel.CanChangeUserRole(user, newRole, originalRole))
                {
                    // 如果不安全，恢复到原来的角色                 
                    comboBox.SelectedValue = originalRole;
                    return;
                }

                // 如果安全，更新用户角色
                user.Role = newRole;

                // 异步调用并正确处理异常，避免未观察到的异常
                // ChangePermission 方法内部已经处理了异常并显示错误消息，这里只需要捕获异常并恢复UI状态
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await viewModel.ChangePermission(user.Username, newRole);
                        
                        // 成功时更新状态消息（在UI线程）
                        App.Current?.Dispatcher.Invoke(() =>
                        {
                            viewModel.StatusMessage = $"用户 {user.Username} 的权限已更新为 {GetRoleDisplayName(newRole)}";
                            // 发送用户列表更新消息
                            WeakReferenceMessenger.Default.Send(new UsersUpdatedMessage { Message = $"用户 {user.Username} 角色已更新" });
                        });
                    }
                    catch (Exception)
                    {
                        // ChangePermission 方法内部已经处理了异常并显示错误消息
                        // 这里只需要恢复UI状态（在UI线程）
                        App.Current?.Dispatcher.Invoke(() =>
                        {
                            // 如果修改失败，恢复到原始角色
                            var originalRole = comboBox.Tag != null && comboBox.Tag is UserRole 
                                ? (UserRole)comboBox.Tag 
                                : user.Role;
                            comboBox.SelectedValue = originalRole;
                            user.Role = originalRole;
                        });
                    }
                });
            }
        }

        private void RoleComboBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is ComboBox comboBox)
            {
                // 获取当前编辑的用户
                var user = comboBox.DataContext as User;
                if (user != null)
                {
                    // 如果是超级管理员，禁用编辑
                    if (user.Role == UserRole.SuperAdministrator)
                    {
                        comboBox.IsEnabled = false;
                        return;
                    }

                    // 将原始角色保存到Tag中（如果还没有设置的话）
                    if (comboBox.Tag == null)
                    {
                        comboBox.Tag = user.Role;
                        System.Diagnostics.Debug.WriteLine($"RoleComboBox_GotFocus: Set Tag to original role: {user.Role} for user: {user.Username}");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"RoleComboBox_GotFocus: Tag already set to: {comboBox.Tag} for user: {user.Username}");
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("RoleComboBox_GotFocus: User is null");
                }
            }
        }

        private void RoleComboBox_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is ComboBox comboBox)
            {
                // 获取当前编辑的用户
                var user = comboBox.DataContext as User;
                if (user != null && user.Role == UserRole.SuperAdministrator)
                {
                    // 超级管理员禁用编辑
                    comboBox.IsEnabled = false;
                }
                else if (DataContext is PermissionViewModel viewModel)
                {
                    // 只有管理员可以编辑
                    bool canEdit = viewModel.IsAdministrator;
                    
                    // 管理员只能编辑工程师和操作员的角色
                    if (canEdit && user != null)
                    {
                        var currentUser = viewModel.GetCurrentUser();
                        // 管理员不能编辑管理员用户的角色
                        if (currentUser?.Role == UserRole.Administrator && user.Role == UserRole.Administrator)
                        {
                            canEdit = false;
                        }
                    }
                    
                    comboBox.IsEnabled = canEdit;
                }
            }
        }

        private string GetRoleDisplayName(UserRole role)
        {
            return role switch
            {
                UserRole.SuperAdministrator => "超级管理员",
                UserRole.Operator => "操作员",
                UserRole.Engineer => "工程师",
                UserRole.Administrator => "管理员",
                _ => "未知"
            };
        }

        private void DataGrid_BeginningEdit(object sender, DataGridBeginningEditEventArgs e)
        {
            if (DataContext is PermissionViewModel viewModel && e.Column.Header?.ToString() == "用户名")
            {
                var user = e.Row.Item as User;
                if (user == null)
                {
                    return;
                }

                // 获取当前登录用户
                var currentUser = viewModel.GetCurrentUser();

                // 不能修改超级管理员的用户名
                if (user.Role == UserRole.SuperAdministrator)
                {
                    e.Cancel = true;
                    ToastHelper.ShowWarning("不能修改超级管理员的用户名");
                    return;
                }

                // 管理员不能编辑管理员用户的用户名
                if (currentUser?.Role == UserRole.Administrator && user.Role == UserRole.Administrator)
                {
                    e.Cancel = true;
                    ToastHelper.ShowWarning("管理员只能修改工程师和操作员的用户名");
                    return;
                }
            }
        }

        private void DataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (DataContext is PermissionViewModel viewModel && e.Column.Header?.ToString() == "用户名")
            {
                var user = e.Row.Item as User;
                if (user == null)
                {
                    return;
                }

                // 获取当前登录用户
                var currentUser = viewModel.GetCurrentUser();
                
                // 不能修改超级管理员的用户名
                if (user.Role == UserRole.SuperAdministrator)
                {
                    e.Cancel = true;
                    ToastHelper.ShowWarning("不能修改超级管理员的用户名");
                    return;
                }

                // 管理员只能修改工程师和操作员的用户名
                if (currentUser?.Role == UserRole.Administrator && user.Role == UserRole.Administrator)
                {
                    e.Cancel = true;
                    ToastHelper.ShowWarning("管理员只能修改工程师和操作员的用户名");
                    return;
                }

                // 获取新输入的用户名
                if (e.EditingElement is TextBox textBox)
                {
                    var newUsername = textBox.Text?.Trim();
                    var oldUsername = user.Username;

                    // 如果用户名没有变化，不需要保存
                    if (string.Equals(newUsername, oldUsername, StringComparison.Ordinal))
                    {
                        return;
                    }

                    // 验证新用户名
                    if (string.IsNullOrWhiteSpace(newUsername))
                    {
                        e.Cancel = true;
                        ToastHelper.ShowError("用户名不能为空");
                        return;
                    }

                    if (newUsername.Length < 3)
                    {
                        e.Cancel = true;
                        ToastHelper.ShowError("用户名长度不能少于3个字符");
                        return;
                    }

                    // 异步调用并正确处理异常，避免未观察到的异常
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await viewModel.ChangeUsername(oldUsername, newUsername);
                            
                            // 成功时重新加载用户列表（在UI线程）
                            App.Current?.Dispatcher.Invoke(async () =>
                            {
                                await viewModel.LoadUsersCommand.ExecuteAsync(null);
                            });
                        }
                        catch (Exception)
                        {
                            // ChangeUsername 方法内部已经处理了异常并显示错误消息
                            // 这里只需要恢复UI状态（在UI线程）
                            App.Current?.Dispatcher.Invoke(() =>
                            {
                                // 如果修改失败，恢复到原始用户名
                                user.Username = oldUsername;
                                e.Row.Item = user; // 刷新行数据
                            });
                        }
                    });
                }
            }
        }
    }
}
