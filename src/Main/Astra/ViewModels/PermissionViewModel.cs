using Astra.Core.Access;
using Astra.Core.Access.Models;
using Astra.Core.Access.Services;
using Astra.Messages;
using Astra.Services.Authorization;
using Astra.Services.Dialogs;
using Astra.Services.Session;
using Astra.UI.Helpers;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using System.Collections.ObjectModel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Linq;
using Astra.Core.Access.Exceptions;

namespace Astra.ViewModels
{
    /// <summary>
    /// 权限管理ViewModel - 重构后的版本
    /// 职责更加清晰,使用策略模式进行权限验证
    /// </summary>
    public partial class PermissionViewModel : ObservableObject
    {
        private readonly IUserManagementService _userManagementService;
        private readonly IUserSessionService _sessionService;
        private readonly IPermissionService _permissionService;
        private readonly IDialogService _dialogService;
        private readonly IMessenger _messenger;
        private readonly ILogger<PermissionViewModel> _logger;

        // 权限策略
        private readonly IPermissionStrategy _adminPermissionStrategy;

        #region 属性

        [ObservableProperty]
        private string _title = "权限管理";

        [ObservableProperty]
        private ObservableCollection<User> _users = new();

        [ObservableProperty]
        private User _selectedUser;

        [ObservableProperty]
        private string _newUsername = string.Empty;

        [ObservableProperty]
        private string _newPassword = string.Empty;

        [ObservableProperty]
        private UserRole _selectedRole = UserRole.Operator;

        [ObservableProperty]
        private bool _isLoading = false;

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        [ObservableProperty]
        private string _validationMessage = string.Empty;

        [ObservableProperty]
        private bool _canAddUser;

        [ObservableProperty]
        private int _totalUsers = 0;

        [ObservableProperty]
        private int _adminCount = 0;

        /// <summary>
        /// 是否为管理员 - 从会话服务获取
        /// </summary>
        public bool IsAdministrator => _sessionService?.IsAdministrator ?? false;

        /// <summary>
        /// 获取当前登录用户（用于权限检查）
        /// </summary>
        public User GetCurrentUser() => _sessionService?.CurrentUser;

        #endregion

        public PermissionViewModel(
            IUserManagementService userManagementService,
            IUserSessionService sessionService,
            IPermissionService permissionService,
            IDialogService dialogService,
            IMessenger messenger = null,
            ILogger<PermissionViewModel> logger = null)
        {
            _userManagementService = userManagementService ?? throw new ArgumentNullException(nameof(userManagementService));
            _sessionService = sessionService ?? throw new ArgumentNullException(nameof(sessionService));
            _permissionService = permissionService ?? throw new ArgumentNullException(nameof(permissionService));
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
            _messenger = messenger ?? WeakReferenceMessenger.Default;
            _logger = logger ?? NullLogger<PermissionViewModel>.Instance;

            // 初始化权限策略
            _adminPermissionStrategy = new AdministratorPermissionStrategy();

            // 注册会话变更消息
            _messenger.Register<UserSessionChangedMessage>(this, OnSessionChanged);
            _messenger.Register<UsersUpdatedMessage>(this, (_, __) => RefreshStatistics());

            // 加载用户列表
            LoadUsers();

            UpdateCanAddUser();
        }

        partial void OnNewUsernameChanged(string value) => DebouncedValidateAndUpdate();
        partial void OnNewPasswordChanged(string value) => DebouncedValidateAndUpdate();
        partial void OnSelectedRoleChanged(UserRole value) => DebouncedValidateAndUpdate();

        private System.Timers.Timer _debounceTimer;
        private void DebouncedValidateAndUpdate()
        {
            _debounceTimer ??= new System.Timers.Timer { AutoReset = false, Interval = 200 };
            _debounceTimer.Elapsed -= DebounceElapsed;
            _debounceTimer.Elapsed += DebounceElapsed;
            _debounceTimer.Stop();
            _debounceTimer.Start();
        }

        private void DebounceElapsed(object? sender, System.Timers.ElapsedEventArgs e)
        {
            App.Current?.Dispatcher.Invoke(() =>
            {
                ValidateForm();
                UpdateCanAddUser();
            });
        }

        private void UpdateCanAddUser()
        {
            CanAddUser = IsAdministrator && string.IsNullOrEmpty(ValidationMessage) && !IsLoading;
        }

        private void ValidateForm()
        {
            ValidationMessage = string.Empty;
            var username = (NewUsername ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(username))
            {
                ValidationMessage = "用户名不能为空";
                return;
            }
            if (username.Length < 3)
            {
                ValidationMessage = "用户名长度不能少于 3 个字符";
                return;
            }
            if (string.IsNullOrWhiteSpace(NewPassword))
            {
                ValidationMessage = "密码不能为空";
                return;
            }
            if (NewPassword.Length < 6)
            {
                ValidationMessage = "密码长度不能少于 6 位";
                return;
            }
            // 重名检测（快速检查，最终以后端服务添加时的异常为准）
            try
            {
                var exists = _userManagementService
                    .GetAllUsers()
                    .Any(u => string.Equals(u.Username, username, StringComparison.OrdinalIgnoreCase));
                if (exists)
                {
                    ValidationMessage = "用户名已存在";
                }
            }
            catch { }
        }

        /// <summary>
        /// 修改用户权限 - 同步到数据库（不重新加载列表）
        /// </summary>
        public async Task ChangePermission(string userName, UserRole newRole)
        {
            try
            {
                // 检查参数
                if (_sessionService.CurrentUser == null)
                {
                    throw new ArgumentNullException(nameof(_sessionService.CurrentUser), "当前用户不能为空");
                }

                if (string.IsNullOrEmpty(userName))
                {
                    throw new ArgumentException("用户名不能为空", nameof(userName));
                }

                // 不能修改为超级管理员
                if (newRole == UserRole.SuperAdministrator)
                {
                    throw new InvalidOperationException("不能通过界面将用户修改为超级管理员");
                }

                // 查找要修改的用户（从数据库查询，因为界面上不显示超级管理员）
                var userToModify = _userManagementService.GetUserByUsername(userName);
                if (userToModify == null)
                {
                    throw new InvalidOperationException("用户不存在");
                }

                // 超级管理员可以修改任何用户角色（除了超级管理员本身）
                if (_sessionService.CurrentUser.Role == UserRole.SuperAdministrator)
                {
                    // 不能修改超级管理员
                    if (userToModify.Role == UserRole.SuperAdministrator)
                    {
                        throw new InvalidOperationException("不能修改超级管理员的角色");
                    }
                }
                // 管理员只能修改工程师和操作员的角色
                else if (_sessionService.CurrentUser.Role == UserRole.Administrator)
                {
                    // 不能修改管理员和超级管理员
                    if (userToModify.Role == UserRole.Administrator || userToModify.Role == UserRole.SuperAdministrator)
                    {
                        throw new InvalidOperationException("管理员只能修改工程师和操作员的角色");
                    }

                    // 不能修改为管理员
                    if (newRole == UserRole.Administrator)
                    {
                        throw new InvalidOperationException("管理员只能将用户修改为工程师或操作员");
                    }
                }

                // 在后台线程执行数据库操作
                await Task.Run(() =>
                {
                    _userManagementService.ChangeUserRole(_sessionService.CurrentUser, userName, newRole);
                });

                // 更新UI状态（不重新加载列表，避免中断ComboBox编辑）
                App.Current.Dispatcher.Invoke(() =>
                {
                    // 更新错误状态
                    StatusMessage = $"用户 {userName} 的权限已修改为 {newRole}";
                });

                //ToastHelper.ShowSuccess($"用户 {userName} 的权限已修改为 {newRole}");
                // 发送更新消息
                _messenger.Send(new UsersUpdatedMessage { Message = $"用户 {userName} 的权限已修改" });
            }
            catch (Exception ex)
            {
                App.Current.Dispatcher.Invoke(() =>
                {
                    // 更新错误状态
                    StatusMessage = $"修改用户 {userName} 权限失败: {ex.Message}";
                });

                ToastHelper.ShowError($"修改用户 {userName} 权限失败: {ex.Message}");

                // 重新抛出异常，让调用者知道操作失败
                throw;
            }
        }

        /// <summary>
        /// 修改用户权限 - 同步到数据库（重新加载列表版本）
        /// </summary>
        public async Task ChangePermissionWithReload(string userName, UserRole newRole)
        {
            await ChangePermission(userName, newRole);

            // 延迟重新加载用户列表
            await Task.Delay(100); // 给ComboBox足够时间完成编辑
            await LoadUsers();
        }

        /// <summary>
        /// 修改用户名 - 同步到数据库
        /// </summary>
        public async Task ChangeUsername(string oldUsername, string newUsername)
        {
            try
            {
                // 检查参数
                if (_sessionService.CurrentUser == null)
                {
                    throw new ArgumentNullException(nameof(_sessionService.CurrentUser), "当前用户不能为空");
                }

                if (string.IsNullOrEmpty(oldUsername))
                {
                    throw new ArgumentException("原用户名不能为空", nameof(oldUsername));
                }

                if (string.IsNullOrEmpty(newUsername))
                {
                    throw new ArgumentException("新用户名不能为空", nameof(newUsername));
                }

                // 验证权限
                var currentUser = _sessionService.CurrentUser;
                if (currentUser.Role != UserRole.Administrator 
                    && currentUser.Role != UserRole.SuperAdministrator)
                {
                    throw new InvalidOperationException("只有管理员才能修改用户名");
                }

                // 获取要修改的用户
                var userToModify = _userManagementService.GetUserByUsername(oldUsername);
                if (userToModify == null)
                {
                    throw new InvalidOperationException("用户不存在");
                }

                // 超级管理员可以修改任何用户名（除了超级管理员本身）
                if (currentUser.Role == UserRole.SuperAdministrator)
                {
                    // 不能修改超级管理员的用户名
                    if (userToModify.Role == UserRole.SuperAdministrator)
                    {
                        throw new InvalidOperationException("不能修改超级管理员的用户名");
                    }
                }
                // 管理员只能修改工程师和操作员的用户名
                else if (currentUser.Role == UserRole.Administrator)
                {
                    // 不能修改管理员和超级管理员的用户名
                    if (userToModify.Role == UserRole.Administrator || userToModify.Role == UserRole.SuperAdministrator)
                    {
                        throw new InvalidOperationException("管理员只能修改工程师和操作员的用户名");
                    }
                }

                // 在后台线程执行数据库操作
                await Task.Run(() =>
                {
                    _userManagementService.ChangeUsername(_sessionService.CurrentUser, oldUsername, newUsername.Trim());
                });

                // 更新UI状态
                App.Current.Dispatcher.Invoke(() =>
                {
                    StatusMessage = $"用户 {oldUsername} 的用户名已修改为 {newUsername}";
                });

                // 发送更新消息
                _messenger.Send(new UsersUpdatedMessage { Message = $"用户 {oldUsername} 的用户名已修改为 {newUsername}" });
            }
            catch (Exception ex)
            {
                App.Current.Dispatcher.Invoke(() =>
                {
                    StatusMessage = $"修改用户名失败: {ex.Message}";
                });

                ToastHelper.ShowError($"修改用户名失败: {ex.Message}");

                // 重新抛出异常，让调用者知道操作失败
                throw;
            }
        }


        #region 命令

        /// <summary>
        /// 获取所有用户（用于权限检查，包括超级管理员）
        /// </summary>
        public List<User> GetAllUsersForCheck()
        {
            return _userManagementService.GetAllUsers().ToList();
        }

        /// <summary>
        /// 加载用户列表
        /// 按照角色优先级排序：管理员 > 工程师 > 操作员，同一角色内按创建时间排序
        /// </summary>
        [RelayCommand]
        private async Task LoadUsers()
        {
            try
            {
                IsLoading = true;
                StatusMessage = "正在加载用户列表...";

                var users = _userManagementService.GetAllUsers().ToList();

                // 过滤掉超级管理员，不在界面上显示
                var filteredUsers = users.Where(u => u.Role != UserRole.SuperAdministrator).ToList();

                // 按照角色优先级排序：管理员 > 工程师 > 操作员
                // 同一角色内按创建时间升序排序
                var sortedUsers = filteredUsers.OrderBy(u => GetRolePriority(u.Role))
                                               .ThenBy(u => u.CreateTime)
                                               .ToList();

                Users.Clear();

                foreach (var user in sortedUsers)
                {
                    Users.Add(user);
                }

                RefreshStatistics();
                StatusMessage = $"已加载 {TotalUsers} 个用户";
            }
            catch (Exception ex)
            {
                StatusMessage = $"加载用户失败: {ex.Message}";
                ToastHelper.ShowError($"加载用户失败: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
                UpdateCanAddUser();
            }
        }

        private void RefreshStatistics()
        {
            try
            {
                TotalUsers = _userManagementService.GetUserCount();
                AdminCount = _userManagementService.GetAdminCount();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[PermissionViewModel] 刷新统计失败");
            }
        }

        /// <summary>
        /// 刷新用户列表
        /// </summary>
        [RelayCommand]
        private async Task RefreshUsers()
        {
            await LoadUsers();
        }

        /// <summary>
        /// 获取角色优先级（用于排序）
        /// 超级管理员=0, 管理员=1, 工程师=2, 操作员=3
        /// </summary>
        /// <param name="role">用户角色</param>
        /// <returns>角色优先级，数值越小优先级越高</returns>
        private int GetRolePriority(UserRole role)
        {
            return role switch
            {
                UserRole.SuperAdministrator => 0,
                UserRole.Administrator => 1,
                UserRole.Engineer => 2,
                UserRole.Operator => 3,
                _ => 99 // 未知角色排在最后
            };
        }

        /// <summary>
        /// 添加用户
        /// </summary>
        [RelayCommand]
        private async Task AddUser()
        {
            // ⭐ 调试日志：检查当前用户状态
            var currentUser = _sessionService.CurrentUser;
            var isAdmin = _sessionService.IsAdministrator;
            System.Diagnostics.Debug.WriteLine($"[PermissionViewModel.AddUser] 当前用户: {currentUser?.Username ?? "未登录"}, 角色: {currentUser?.Role}, 是否管理员: {isAdmin}");

            // 使用策略模式验证权限
            if (!_permissionService.CheckPermissionWithMessage(
                currentUser, "添加用户", _adminPermissionStrategy))
            {
                System.Diagnostics.Debug.WriteLine($"[PermissionViewModel.AddUser] 权限验证失败");
                return;
            }

            System.Diagnostics.Debug.WriteLine($"[PermissionViewModel.AddUser] 权限验证通过");

            // 验证不能添加超级管理员
            if (SelectedRole == UserRole.SuperAdministrator)
            {
                StatusMessage = "不能通过界面添加超级管理员";
                ToastHelper.ShowError("不能通过界面添加超级管理员");
                return;
            }

            // 管理员只能添加工程师和操作员，不能添加管理员
            if (currentUser.Role == UserRole.Administrator && SelectedRole == UserRole.Administrator)
            {
                StatusMessage = "管理员只能添加工程师和操作员，不能添加管理员";
                ToastHelper.ShowError("管理员只能添加工程师和操作员，不能添加管理员");
                return;
            }

            // 验证输入
            ValidateForm();
            if (!string.IsNullOrEmpty(ValidationMessage))
            {
                StatusMessage = ValidationMessage;
                ToastHelper.ShowWarning(ValidationMessage);
                return;
            }

            try
            {
                IsLoading = true;
                UpdateCanAddUser();
                var username = NewUsername.Trim();
                // 后台线程执行数据库写入，避免阻塞UI线程
                await Task.Run(() =>
                {
                    _userManagementService.AddUser(
                        _sessionService.CurrentUser,
                        username,
                        NewPassword,
                        SelectedRole);
                });

                var addedUsername = username;

                // 清空表单
                ClearForm();

                // 重新加载（刷新列表与统计）
                await LoadUsers();

                StatusMessage = $"用户 {addedUsername} 添加成功";
                ToastHelper.ShowSuccess($"用户 {addedUsername} 添加成功");

                // 发送更新消息
                _messenger.Send(new UsersUpdatedMessage { Message = $"用户 {addedUsername} 已添加" });
            }
            catch (AccessGuardException agx)
            {
                StatusMessage = agx.Message;
                ToastHelper.ShowError(agx.Message);
            }
            catch (Exception ex)
            {
                StatusMessage = $"添加用户失败: {ex.Message}";
                ToastHelper.ShowError($"添加用户失败: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
                UpdateCanAddUser();
            }
        }

        /// <summary>
        /// 删除用户
        /// </summary>
        [RelayCommand]
        private async Task DeleteUser(User user)
        {
            // ⭐ 调试日志
            var currentUser = _sessionService.CurrentUser;
            var isAdmin = _sessionService.IsAdministrator;
            System.Diagnostics.Debug.WriteLine($"[PermissionViewModel.DeleteUser] 当前用户: {currentUser?.Username ?? "未登录"}, 角色: {currentUser?.Role}, 是否管理员: {isAdmin}");

            // 验证权限
            if (!_permissionService.CheckPermissionWithMessage(
                currentUser, "删除用户", _adminPermissionStrategy))
            {
                System.Diagnostics.Debug.WriteLine($"[PermissionViewModel.DeleteUser] 权限验证失败");
                return;
            }

            System.Diagnostics.Debug.WriteLine($"[PermissionViewModel.DeleteUser] 权限验证通过");

            if (user == null)
            {
                StatusMessage = "请选择要删除的用户";
                return;
            }

            // 不能删除超级管理员
            if (user.Role == UserRole.SuperAdministrator)
            {
                StatusMessage = "不能删除超级管理员";
                ToastHelper.ShowError("不能删除超级管理员");
                return;
            }

            // 管理员只能删除工程师和操作员，不能删除管理员
            if (currentUser.Role == UserRole.Administrator && user.Role == UserRole.Administrator)
            {
                StatusMessage = "管理员只能删除工程师和操作员，不能删除管理员";
                ToastHelper.ShowError("管理员只能删除工程师和操作员，不能删除管理员");
                return;
            }

            // 检查是否为最后一个管理员
            if (!CanDeleteUser(user))
            {
                return;
            }

            if (!MessageBoxHelper.ConfirmDelete($"用户 '{user.Username}'"))
                return;

            try
            {
                IsLoading = true;
                UpdateCanAddUser();
                // 后台线程执行数据库写入
                await Task.Run(() =>
                {
                    _userManagementService.DeleteUser(_sessionService.CurrentUser, user.Username);
                });

                // 清除当前选中，防止指向已删除项
                SelectedUser = null;
                // 重新加载（刷新列表与统计）
                await LoadUsers();

                StatusMessage = $"用户 {user.Username} 已删除";
                ToastHelper.ShowSuccess($"用户 {user.Username} 删除成功");

                // 发送更新消息
                _messenger.Send(new UsersUpdatedMessage { Message = $"用户 {user.Username} 已删除" });
            }
            catch (Exception ex)
            {
                StatusMessage = $"删除用户失败: {ex.Message}";
                ToastHelper.ShowError($"删除用户失败: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
                UpdateCanAddUser();
            }
        }

        /// <summary>
        /// 保存当前行用户角色变更
        /// </summary>
        [RelayCommand]
        private async Task ChangeUserRole(User user)
        {
            var currentUser = _sessionService.CurrentUser;
            // 超级管理员和管理员都可以修改用户角色
            if (currentUser == null || (currentUser.Role != UserRole.Administrator && currentUser.Role != UserRole.SuperAdministrator))
            {
                if (!_permissionService.CheckPermissionWithMessage(currentUser, "修改用户角色", _adminPermissionStrategy))
                {
                    return;
                }
            }
            if (user == null)
            {
                StatusMessage = "未选择用户";
                return;
            }
            // 保护最后一个管理员
            if (!CanChangeUserRole(user, user.Role))
            {
                return;
            }
            try
            {
                IsLoading = true;
                await ChangePermissionWithReload(user.Username, user.Role);
                StatusMessage = $"已保存 {user.Username} 的角色为 {user.Role}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"保存用户角色失败: {ex.Message}";
                ToastHelper.ShowError(StatusMessage);
            }
            finally
            {
                IsLoading = false;
            }
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 清空表单
        /// </summary>
        private void ClearForm()
        {
            NewUsername = string.Empty;
            NewPassword = string.Empty;
            SelectedRole = UserRole.Operator;
        }

        /// <summary>
        /// 检查是否可以删除用户
        /// </summary>
        private bool CanDeleteUser(User user)
        {
            // 不能删除超级管理员
            if (user.Role == UserRole.SuperAdministrator)
            {
                ToastHelper.ShowWarning("不能删除超级管理员");
                StatusMessage = "不能删除超级管理员";
                return false;
            }

            if (user.Role == UserRole.Administrator)
            {
                // 从数据库查询所有管理员（包括超级管理员），因为界面上不显示超级管理员
                var allUsers = _userManagementService.GetAllUsers().ToList();
                var otherAdmins = allUsers.Count(u => 
                    (u.Role == UserRole.Administrator || u.Role == UserRole.SuperAdministrator) && u.Id != user.Id);
                if (otherAdmins == 0)
                {
                    ToastHelper.ShowWarning("不能删除最后一个管理员账户");
                    StatusMessage = "不能删除最后一个管理员账户";
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// 检查是否可以修改用户角色
        /// </summary>
        public bool CanChangeUserRole(User user, UserRole newRole, UserRole? originalRole = null)
        {
            var currentUser = _sessionService.CurrentUser;
            var currentRole = originalRole ?? user.Role;

            // 超级管理员可以修改任何用户角色（除了超级管理员本身）
            if (currentUser?.Role == UserRole.SuperAdministrator)
            {
                // 不能修改超级管理员
                if (currentRole == UserRole.SuperAdministrator)
                {
                    ToastHelper.ShowWarning("不能修改超级管理员的角色");
                    return false;
                }

                // 不能修改为超级管理员
                if (newRole == UserRole.SuperAdministrator)
                {
                    ToastHelper.ShowWarning("不能通过界面将用户修改为超级管理员");
                    return false;
                }

                // 超级管理员可以修改任何其他角色
                return true;
            }

            // 管理员只能修改工程师和操作员的角色
            if (currentUser?.Role == UserRole.Administrator)
            {
                // 不能修改管理员和超级管理员
                if (currentRole == UserRole.Administrator || currentRole == UserRole.SuperAdministrator)
                {
                    ToastHelper.ShowWarning("管理员只能修改工程师和操作员的角色");
                    return false;
                }

                // 不能修改为管理员或超级管理员
                if (newRole == UserRole.Administrator || newRole == UserRole.SuperAdministrator)
                {
                    ToastHelper.ShowWarning("管理员只能将用户修改为工程师或操作员");
                    return false;
                }

                // 管理员可以修改工程师和操作员的角色
                return true;
            }

            // 其他角色不能修改用户角色
            ToastHelper.ShowWarning("只有管理员才能修改用户角色");
            return false;
        }

        /// <summary>
        /// 处理会话变更消息
        /// </summary>
        private void OnSessionChanged(object recipient, UserSessionChangedMessage message)
        {
            // 通知权限相关属性变化
            OnPropertyChanged(nameof(IsAdministrator));
            UpdateCanAddUser();

            System.Diagnostics.Debug.WriteLine(
                $"[PermissionViewModel] 会话变更: {message.CurrentUser?.Username ?? "未登录"}, " +
                $"是否管理员: {IsAdministrator}");
        }

        #endregion
    }
}
