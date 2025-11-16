using Astra.Core.Access;
using Astra.Core.Access.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Astra.Core.Access.Security
{
    /// <summary>
    /// 权限验证器接口
    /// 定义用户权限验证的抽象接口，遵循接口隔离原则 (ISP)
    /// 用于验证用户是否具有执行特定操作的权限
    /// </summary>
    public interface IPermissionValidator
    {
        /// <summary>
        /// 验证当前用户是否有权限添加用户
        /// </summary>
        /// <param name="currentUser">当前操作用户</param>
        /// <exception cref="AccessGuardException">当用户没有权限时抛出异常</exception>
        void ValidateCanAddUser(User currentUser);

        /// <summary>
        /// 验证当前用户是否有权限删除用户
        /// </summary>
        /// <param name="currentUser">当前操作用户</param>
        /// <exception cref="AccessGuardException">当用户没有权限时抛出异常</exception>
        void ValidateCanDeleteUser(User currentUser);

        /// <summary>
        /// 验证当前用户是否有权限重置密码
        /// </summary>
        /// <param name="currentUser">当前操作用户</param>
        /// <exception cref="AccessGuardException">当用户没有权限时抛出异常</exception>
        void ValidateCanResetPassword(User currentUser);

        /// <summary>
        /// 验证当前用户是否有权限删除目标用户
        /// 包括检查：不能删除自己、不能删除最后一个管理员
        /// </summary>
        /// <param name="currentUser">当前操作用户</param>
        /// <param name="targetUser">目标用户</param>
        /// <exception cref="AccessGuardException">当用户没有权限或违反删除规则时抛出异常</exception>
        void ValidateCanDeleteTarget(User currentUser, User targetUser);
    }
}
