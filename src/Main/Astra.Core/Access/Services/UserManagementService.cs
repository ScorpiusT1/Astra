using Astra.Core.Access.Exceptions;
using Astra.Core.Access.Models;
using Astra.Core.Access.Repositories;
using Astra.Core.Access.Security;

namespace Astra.Core.Access.Services
{
    /// <summary>
    /// 用户管理服务实现类。
    /// 依赖 <see cref="IUserRepository"/> 和 <see cref="IPasswordService"/>，
    /// 不直接依赖任何具体数据访问技术（EF Core、数据库等），
    /// 遵循依赖倒置原则（DIP）。
    /// </summary>
    public class UserManagementService : IUserManagementService
    {
        private readonly IUserRepository _repository;
        private readonly IPasswordService _passwordService;

        public UserManagementService(IUserRepository repository, IPasswordService passwordService)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _passwordService = passwordService ?? throw new ArgumentNullException(nameof(passwordService));
        }

        /// <inheritdoc/>
        public User Login(string username, string password)
        {
            var user = _repository.GetByUsername(username)
                ?? throw new AccessGuardException("用户名不存在");

            if (!_passwordService.VerifyPassword(password, user.PasswordHash))
                throw new AccessGuardException("密码错误");

            user.LastLoginTime = DateTime.Now;
            user.LastModifyTime = DateTime.Now;
            _repository.Update(user);

            return user;
        }

        /// <inheritdoc/>
        public void AddUser(User currentUser, string username, string password, UserRole role)
        {
            if (currentUser.Role != UserRole.Administrator && currentUser.Role != UserRole.SuperAdministrator)
                throw new AccessGuardException("只有管理员才能添加用户");

            if (role == UserRole.SuperAdministrator)
                throw new AccessGuardException("不能通过界面添加超级管理员");

            if (currentUser.Role == UserRole.Administrator && role == UserRole.Administrator)
                throw new AccessGuardException("管理员只能添加工程师和操作员，不能添加管理员");

            if (_repository.ExistsByUsername(username))
                throw new AccessGuardException("用户名已存在");

            _passwordService.ValidatePasswordStrength(password);

            _repository.Add(new User
            {
                Username = username,
                PasswordHash = _passwordService.HashPassword(password),
                Role = role,
                CreateTime = DateTime.Now,
                LastModifyTime = DateTime.Now
            });
        }

        /// <inheritdoc/>
        public void DeleteUser(User currentUser, string username)
        {
            if (currentUser.Role != UserRole.Administrator && currentUser.Role != UserRole.SuperAdministrator)
                throw new AccessGuardException("只有管理员才能删除用户");

            if (currentUser.Username == username)
                throw new AccessGuardException("不能删除当前登录的账号");

            var targetUser = _repository.GetByUsername(username)
                ?? throw new AccessGuardException("用户不存在");

            if (targetUser.Role == UserRole.SuperAdministrator)
                throw new AccessGuardException("不能删除超级管理员");

            if (currentUser.Role == UserRole.Administrator && targetUser.Role == UserRole.Administrator)
                throw new AccessGuardException("管理员只能删除工程师和操作员，不能删除管理员");

            if (targetUser.Role == UserRole.Administrator)
            {
                int totalAdmins = _repository.CountByRole(UserRole.Administrator)
                                + _repository.CountByRole(UserRole.SuperAdministrator);
                if (totalAdmins <= 1)
                    throw new AccessGuardException("不能删除最后一个管理员账号");
            }

            _repository.Delete(targetUser);
        }

        /// <inheritdoc/>
        public void ChangeUserRole(User currentUser, string username, UserRole newRole)
        {
            if (currentUser.Role != UserRole.Administrator && currentUser.Role != UserRole.SuperAdministrator)
                throw new AccessGuardException("只有管理员才能修改用户角色");

            var targetUser = _repository.GetByUsername(username)
                ?? throw new AccessGuardException("用户不存在");

            var oldRole = targetUser.Role;

            if (currentUser.Role == UserRole.SuperAdministrator)
            {
                if (oldRole == UserRole.SuperAdministrator)
                    throw new AccessGuardException("不能修改超级管理员的角色");

                if (newRole == UserRole.SuperAdministrator)
                    throw new AccessGuardException("不能通过界面将用户修改为超级管理员");

                if (oldRole == UserRole.Administrator && newRole != UserRole.Administrator)
                {
                    int totalAdmins = _repository.CountByRole(UserRole.Administrator)
                                    + _repository.CountByRole(UserRole.SuperAdministrator);
                    // 目标用户自身也是管理员，修改后剩余 = totalAdmins - 1
                    if (totalAdmins <= 1)
                        throw new AccessGuardException("不能修改最后一个管理员的权限");
                }
            }
            else if (currentUser.Role == UserRole.Administrator)
            {
                if (oldRole == UserRole.Administrator || oldRole == UserRole.SuperAdministrator)
                    throw new AccessGuardException("管理员只能修改工程师和操作员的角色");

                if (newRole == UserRole.Administrator || newRole == UserRole.SuperAdministrator)
                    throw new AccessGuardException("管理员只能将用户修改为工程师或操作员");
            }

            targetUser.Role = newRole;
            targetUser.LastModifyTime = DateTime.Now;
            _repository.Update(targetUser);
        }

        /// <inheritdoc/>
        public void ChangeUsername(User currentUser, string oldUsername, string newUsername)
        {
            if (currentUser.Role != UserRole.Administrator && currentUser.Role != UserRole.SuperAdministrator)
                throw new AccessGuardException("只有管理员才能修改用户名");

            if (string.IsNullOrWhiteSpace(newUsername))
                throw new AccessGuardException("新用户名不能为空");

            if (newUsername.Length < 3)
                throw new AccessGuardException("用户名长度不能少于3个字符");

            var targetUser = _repository.GetByUsername(oldUsername)
                ?? throw new AccessGuardException("用户不存在");

            if (currentUser.Role == UserRole.SuperAdministrator)
            {
                if (targetUser.Role == UserRole.SuperAdministrator)
                    throw new AccessGuardException("不能修改超级管理员的用户名");
            }
            else if (currentUser.Role == UserRole.Administrator)
            {
                if (targetUser.Role == UserRole.Administrator || targetUser.Role == UserRole.SuperAdministrator)
                    throw new AccessGuardException("管理员只能修改工程师和操作员的用户名");
            }

            if (_repository.GetByUsername(newUsername) is { } existing && existing.Id != targetUser.Id)
                throw new AccessGuardException("用户名已存在");

            targetUser.Username = newUsername.Trim();
            targetUser.LastModifyTime = DateTime.Now;
            _repository.Update(targetUser);
        }

        /// <inheritdoc/>
        public void ChangePassword(User currentUser, string oldPassword, string newPassword)
        {
            if (!_passwordService.VerifyPassword(oldPassword, currentUser.PasswordHash))
                throw new AccessGuardException("原密码错误");

            _passwordService.ValidatePasswordStrength(newPassword);

            var user = _repository.GetById(currentUser.Id)
                ?? throw new AccessGuardException("用户不存在");

            user.PasswordHash = _passwordService.HashPassword(newPassword);
            user.LastModifyTime = DateTime.Now;
            _repository.Update(user);

            currentUser.PasswordHash = user.PasswordHash;
            currentUser.LastModifyTime = user.LastModifyTime;
        }

        /// <inheritdoc/>
        public void ResetPassword(User currentUser, string username, string newPassword)
        {
            if (currentUser.Role != UserRole.Administrator && currentUser.Role != UserRole.SuperAdministrator)
                throw new AccessGuardException("只有管理员才能重置密码");

            var targetUser = _repository.GetByUsername(username)
                ?? throw new AccessGuardException("用户不存在");

            _passwordService.ValidatePasswordStrength(newPassword);

            targetUser.PasswordHash = _passwordService.HashPassword(newPassword);
            targetUser.LastModifyTime = DateTime.Now;
            _repository.Update(targetUser);
        }

        /// <inheritdoc/>
        public IEnumerable<User> GetAllUsers()
            => _repository.GetAll();

        /// <inheritdoc/>
        public User? GetUserByUsername(string username)
            => _repository.GetByUsername(username);

        /// <inheritdoc/>
        public IEnumerable<User> GetUsersByRole(UserRole role)
            => _repository.GetByRole(role);

        /// <inheritdoc/>
        public int GetUserCount()
            => _repository.Count() - _repository.CountByRole(UserRole.SuperAdministrator);

        /// <inheritdoc/>
        public int GetAdminCount()
            => _repository.CountByRole(UserRole.Administrator);

        /// <inheritdoc/>
        public User? GetLastLoginOperator()
            => _repository.GetByRole(UserRole.Operator)
                          .OrderByDescending(u => u.LastLoginTime ?? u.CreateTime)
                          .FirstOrDefault();

        /// <inheritdoc/>
        public void UpdateLastLoginTime(string username)
        {
            var user = _repository.GetByUsername(username);
            if (user == null) return;

            user.LastLoginTime = DateTime.Now;
            user.LastModifyTime = DateTime.Now;
            _repository.Update(user);
        }
    }
}
