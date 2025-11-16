using Astra.ViewModels;
using Astra.Core.Access;
using CommunityToolkit.Mvvm.Messaging;
using System.Windows;
using System.Windows.Controls;
using Astra.Messages;
using System.Threading.Tasks;
using Astra.Core.Access.Models;

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

                // 获取新选择的角色
                var newRole = (UserRole)comboBox.SelectedValue;

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

                _ = viewModel.ChangePermission(user.Username, newRole);

                // 更新状态消息
                viewModel.StatusMessage = $"用户 {user.Username} 的权限已更新为 {GetRoleDisplayName(newRole)}";
               
                // 发送用户列表更新消息
                WeakReferenceMessenger.Default.Send(new UsersUpdatedMessage { Message = $"用户 {user.Username} 角色已更新" });
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

        private string GetRoleDisplayName(UserRole role)
        {
            return role switch
            {
                UserRole.Operator => "操作员",
                UserRole.Engineer => "工程师",
                UserRole.Administrator => "管理员",
                _ => "未知"
            };
        }
    }
}
