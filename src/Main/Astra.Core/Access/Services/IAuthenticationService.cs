using Astra.Core.Access;
using Astra.Core.Access.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Astra.Core.Access.Services
{
    /// <summary>
    /// 认证服务接口
    /// 定义用户身份认证相关的功能，遵循接口隔离原则 (ISP)
    /// 包括用户登录和密码修改功能
    /// </summary>
    public interface IAuthenticationService
    {
        /// <summary>
        /// 用户登录验证
        /// </summary>
        /// <param name="username">用户名</param>
        /// <param name="password">密码</param>
        /// <returns>登录成功返回用户对象</returns>
        /// <exception cref="AccessGuardException">当用户名不存在或密码错误时抛出异常</exception>
        User Login(string username, string password);

        /// <summary>
        /// 修改用户密码（用户自己修改）
        /// </summary>
        /// <param name="currentUser">当前用户</param>
        /// <param name="oldPassword">旧密码</param>
        /// <param name="newPassword">新密码</param>
        /// <exception cref="AccessGuardException">当旧密码错误或新密码不符合强度要求时抛出异常</exception>
        void ChangePassword(User currentUser, string oldPassword, string newPassword);
    }
}
