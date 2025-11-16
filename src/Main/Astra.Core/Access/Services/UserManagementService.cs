using Astra.Core.Access;
using Astra.Core.Access.Data;
using Astra.Core.Access.Exceptions;
using Astra.Core.Access.Models;
using Astra.Core.Foundation.Common;
using Microsoft.EntityFrameworkCore;

namespace Astra.Core.Access.Services
{
    /// <summary>
    /// 用户管理服务类（基于EF Core）
    /// </summary>
    public class UserManagementService : IUserManagementService, IDisposable
    {
        private readonly AccessGuardDbContext _context;
        private readonly bool _ownsContext;

        /// <summary>
        /// 默认构造函数（创建新的DbContext）
        /// </summary>
        public UserManagementService(string dbPath = null)
        {
            _context = new AccessGuardDbContext(dbPath);
            _ownsContext = true;
            DatabaseInitializer.Initialize(_context);
        }

        /// <summary>
        /// 依赖注入构造函数（使用外部DbContext）
        /// </summary>
        public UserManagementService(AccessGuardDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _ownsContext = false;
            DatabaseInitializer.Initialize(_context);
        }

        /// <summary>
        /// 用户登录验证
        /// </summary>
        public User Login(string username, string password)
        {
            var user = _context.Users
                .AsNoTracking()
                .FirstOrDefault(u => u.Username == username);

            if (user == null)
            {
                throw new AccessGuardException("用户名不存在");
            }

            if (!PasswordHelper.VerifyPassword(password, user.PasswordHash))
            {
                throw new AccessGuardException("密码错误");
            }

            // 更新最后登录时间
            UpdateLastLoginTime(username);

            return user;
        }

        /// <summary>
        /// 添加用户（仅管理员可操作）
        /// </summary>
        public void AddUser(User currentUser, string username, string password, UserRole role)
        {
            // 权限检查
            if (currentUser.Role != UserRole.Administrator)
            {
                throw new AccessGuardException("只有管理员才能添加用户");
            }

            // 检查用户名是否已存在
            if (_context.Users.Any(u => u.Username == username))
            {
                throw new AccessGuardException("用户名已存在");
            }

            // 验证密码强度
            if (string.IsNullOrWhiteSpace(password) || password.Length < 6)
            {
                throw new AccessGuardException("密码长度至少为6位");
            }

            // 创建新用户
            var newUser = new User
            {
                Username = username,
                PasswordHash = PasswordHelper.HashPassword(password),
                Role = role,
                CreateTime = DateTime.Now,
                LastModifyTime = DateTime.Now
            };

            // ⭐ 调试日志：记录创建的用户信息
            System.Diagnostics.Debug.WriteLine($"[UserManagementService.AddUser] 创建用户: {username}, 角色: {role}");

            _context.Users.Add(newUser);
            _context.SaveChanges();
            
            // ⭐ 调试日志：确认保存成功
            System.Diagnostics.Debug.WriteLine($"[UserManagementService.AddUser] 用户 {username} 已成功保存到数据库，角色: {role}");
        }

        /// <summary>
        /// 删除用户（仅管理员可操作）
        /// </summary>
        public void DeleteUser(User currentUser, string username)
        {
            // 权限检查
            if (currentUser.Role != UserRole.Administrator)
            {
                throw new AccessGuardException("只有管理员才能删除用户");
            }

            // 不能删除自己
            if (currentUser.Username == username)
            {
                throw new AccessGuardException("不能删除当前登录的账号");
            }

            var targetUser = _context.Users.FirstOrDefault(u => u.Username == username);
            if (targetUser == null)
            {
                throw new AccessGuardException("用户不存在");
            }

            // 检查是否为最后一个管理员
            if (targetUser.Role == UserRole.Administrator)
            {
                int adminCount = _context.Users.Count(u => u.Role == UserRole.Administrator);
                if (adminCount <= 1)
                {
                    throw new AccessGuardException("不能删除最后一个管理员账号");
                }
            }

            _context.Users.Remove(targetUser);
            _context.SaveChanges();
        }

        /// <summary>
        /// 修改用户角色（仅管理员可操作）
        /// </summary>
        public void ChangeUserRole(User currentUser, string username, UserRole newRole)
        {
            // 权限检查
            if (currentUser.Role != UserRole.Administrator)
            {
                throw new AccessGuardException("只有管理员才能修改用户角色");
            }

            var targetUser = _context.Users.FirstOrDefault(u => u.Username == username);
            if (targetUser == null)
            {
                throw new AccessGuardException("用户不存在");
            }

            var oldRole = targetUser.Role;

            // 如果要将管理员改为其他角色，检查是否为最后一个管理员
            if (oldRole == UserRole.Administrator && newRole != UserRole.Administrator)
            {
                int adminCount = _context.Users.Count(u => u.Role == UserRole.Administrator);
                if (adminCount <= 1)
                {
                    throw new AccessGuardException("不能修改最后一个管理员的权限");
                }
            }

            // ⭐ 调试日志：记录角色修改
            System.Diagnostics.Debug.WriteLine($"[UserManagementService.ChangeUserRole] 修改用户角色: {username}, 从 {oldRole} 改为 {newRole}");

            // 修改角色
            targetUser.Role = newRole;
            targetUser.LastModifyTime = DateTime.Now;
            _context.SaveChanges();
            
            // ⭐ 调试日志：确认保存成功
            System.Diagnostics.Debug.WriteLine($"[UserManagementService.ChangeUserRole] 用户 {username} 角色已成功保存到数据库，新角色: {newRole}");
        }

        /// <summary>
        /// 修改密码（用户可以修改自己的密码）
        /// </summary>
        public void ChangePassword(User currentUser, string oldPassword, string newPassword)
        {
            // 验证旧密码
            if (!PasswordHelper.VerifyPassword(oldPassword, currentUser.PasswordHash))
            {
                throw new AccessGuardException("原密码错误");
            }

            // 验证新密码强度
            if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 6)
            {
                throw new AccessGuardException("新密码长度至少为6位");
            }

            // 从数据库获取用户实体
            var user = _context.Users.Find(currentUser.Id);
            if (user == null)
            {
                throw new AccessGuardException("用户不存在");
            }

            // 更新密码
            user.PasswordHash = PasswordHelper.HashPassword(newPassword);
            user.LastModifyTime = DateTime.Now;

            _context.SaveChanges();

            // 更新当前用户对象
            currentUser.PasswordHash = user.PasswordHash;
            currentUser.LastModifyTime = user.LastModifyTime;
        }

        /// <summary>
        /// 管理员重置用户密码
        /// </summary>
        public void ResetPassword(User currentUser, string username, string newPassword)
        {
            // 权限检查
            if (currentUser.Role != UserRole.Administrator)
            {
                throw new AccessGuardException("只有管理员才能重置密码");
            }

            var targetUser = _context.Users.FirstOrDefault(u => u.Username == username);
            if (targetUser == null)
            {
                throw new AccessGuardException("用户不存在");
            }

            // 验证新密码强度
            if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 6)
            {
                throw new AccessGuardException("新密码长度至少为6位");
            }

            // 更新密码
            targetUser.PasswordHash = PasswordHelper.HashPassword(newPassword);
            targetUser.LastModifyTime = DateTime.Now;

            _context.SaveChanges();
        }

        /// <summary>
        /// 获取所有用户列表
        /// </summary>
        public IEnumerable<User> GetAllUsers()
        {
            return _context.Users
                .AsNoTracking()
                .OrderByDescending(u => u.Role)
                .ThenBy(u => u.CreateTime)
                .ToList();
        }

        /// <summary>
        /// 根据用户名获取用户
        /// </summary>
        public User GetUserByUsername(string username)
        {
            return _context.Users
                .AsNoTracking()
                .FirstOrDefault(u => u.Username == username);
        }

        /// <summary>
        /// 根据角色获取用户列表
        /// </summary>
        public IEnumerable<User> GetUsersByRole(UserRole role)
        {
            return _context.Users
                .AsNoTracking()
                .Where(u => u.Role == role)
                .OrderBy(u => u.CreateTime)
                .ToList();
        }

        /// <summary>
        /// 获取用户总数
        /// </summary>
        public int GetUserCount()
        {
            return _context.Users.Count();
        }

        /// <summary>
        /// 获取管理员数量
        /// </summary>
        public int GetAdminCount()
        {
            return _context.Users.Count(u => u.Role == UserRole.Administrator);
        }

        /// <summary>
        /// 获取最后登录的操作员用户
        /// </summary>
        /// <returns>最后登录的操作员用户，如果没有则返回null</returns>
        public User? GetLastLoginOperator()
        {
            return _context.Users
                .Where(u => u.Role == UserRole.Operator)
                .OrderByDescending(u => u.LastLoginTime ?? u.CreateTime)
                .FirstOrDefault();
        }

        /// <summary>
        /// 更新用户最后登录时间
        /// </summary>
        /// <param name="username">用户名</param>
        public void UpdateLastLoginTime(string username)
        {
            User? user = _context.Users.FirstOrDefault(u => u.Username == username);
            if (user != null)
            {
                user.LastLoginTime = DateTime.Now;
                user.LastModifyTime = DateTime.Now;
                _context.SaveChanges();
            }
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            if (_ownsContext)
            {
                _context?.Dispose();
            }
        }
    }
}
