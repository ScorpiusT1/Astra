using Astra.Core.Access;
using Astra.Core.Access.Exceptions;
using Astra.Core.Access.Models;
using Astra.Core.Access.Repositories;
using Astra.Core.Access.Security;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Astra.Core.Access.Services
{
    /// <summary>
    /// 认证服务实现类
    /// 负责用户身份认证相关的业务逻辑，遵循单一职责原则 (SRP)：只负责认证相关业务
    /// 包括用户登录验证和密码修改功能
    /// </summary>
    public class AuthenticationService : IAuthenticationService
    {
        private readonly IUserRepository _userRepository;
        private readonly IPasswordService _passwordService;

        /// <summary>
        /// 构造函数，通过依赖注入获取用户仓储和密码服务
        /// </summary>
        /// <param name="userRepository">用户仓储实例</param>
        /// <param name="passwordService">密码服务实例</param>
        /// <exception cref="ArgumentNullException">当参数为null时抛出异常</exception>
        public AuthenticationService(
            IUserRepository userRepository,
            IPasswordService passwordService)
        {
            _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
            _passwordService = passwordService ?? throw new ArgumentNullException(nameof(passwordService));
        }

        /// <summary>
        /// 用户登录验证
        /// 验证用户名和密码，如果验证成功返回用户对象
        /// </summary>
        /// <exception cref="AccessGuardException">当用户名不存在或密码错误时抛出异常</exception>
        public User Login(string username, string password)
        {
            var user = _userRepository.GetByUsername(username);
            if (user == null)
            {
                throw new AccessGuardException("用户名不存在");
            }

            if (!_passwordService.VerifyPassword(password, user.PasswordHash))
            {
                throw new AccessGuardException("密码错误");
            }

            return user;
        }

        /// <summary>
        /// 修改用户密码（用户自己修改）
        /// 验证旧密码正确性，检查新密码强度，然后更新密码
        /// </summary>
        /// <exception cref="AccessGuardException">当旧密码错误、新密码不符合强度要求或用户不存在时抛出异常</exception>
        public void ChangePassword(User currentUser, string oldPassword, string newPassword)
        {
            // 验证旧密码
            if (!_passwordService.VerifyPassword(oldPassword, currentUser.PasswordHash))
            {
                throw new AccessGuardException("原密码错误");
            }

            // 验证新密码强度
            _passwordService.ValidatePasswordStrength(newPassword);

            // 获取数据库中的用户实体
            var user = _userRepository.GetById(currentUser.Id);
            if (user == null)
            {
                throw new AccessGuardException("用户不存在");
            }

            // 更新密码
            user.PasswordHash = _passwordService.HashPassword(newPassword);
            user.LastModifyTime = DateTime.Now;
            _userRepository.Update(user);

            // 同步当前用户对象，确保内存中的对象与数据库保持一致
            currentUser.PasswordHash = user.PasswordHash;
            currentUser.LastModifyTime = user.LastModifyTime;
        }
    }
}
