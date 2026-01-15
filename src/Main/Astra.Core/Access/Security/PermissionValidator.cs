using Astra.Core.Access;
using Astra.Core.Access.Exceptions;
using Astra.Core.Access.Models;
using Astra.Core.Access.Repositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Astra.Core.Access.Security
{
    /// <summary>
    /// 权限验证器实现类
    /// 负责验证用户是否具有执行特定操作的权限
    /// 实现权限控制逻辑，确保只有具有相应权限的用户才能执行操作
    /// </summary>
    public class PermissionValidator : IPermissionValidator
    {
        private readonly IUserRepository _userRepository;

        /// <summary>
        /// 构造函数，通过依赖注入获取用户仓储
        /// </summary>
        /// <param name="userRepository">用户仓储实例</param>
        public PermissionValidator(IUserRepository userRepository)
        {
            _userRepository = userRepository;
        }

        /// <summary>
        /// 验证当前用户是否有权限添加用户
        /// 只有管理员或超级管理员角色可以添加用户
        /// </summary>
        public void ValidateCanAddUser(User currentUser)
        {
            if (currentUser.Role != UserRole.Administrator && currentUser.Role != UserRole.SuperAdministrator)
            {
                throw new AccessGuardException("只有管理员才能添加用户");
            }
        }

        /// <summary>
        /// 验证当前用户是否有权限删除用户
        /// 只有管理员或超级管理员角色可以删除用户
        /// </summary>
        public void ValidateCanDeleteUser(User currentUser)
        {
            if (currentUser.Role != UserRole.Administrator && currentUser.Role != UserRole.SuperAdministrator)
            {
                throw new AccessGuardException("只有管理员才能删除用户");
            }
        }

        /// <summary>
        /// 验证当前用户是否有权限重置密码
        /// 只有管理员或超级管理员角色可以重置其他用户的密码
        /// </summary>
        public void ValidateCanResetPassword(User currentUser)
        {
            if (currentUser.Role != UserRole.Administrator && currentUser.Role != UserRole.SuperAdministrator)
            {
                throw new AccessGuardException("只有管理员才能重置密码");
            }
        }

        /// <summary>
        /// 验证当前用户是否有权限删除目标用户
        /// 检查规则：
        /// 1. 不能删除自己
        /// 2. 不能删除最后一个管理员账号（确保系统至少有一个管理员）
        /// </summary>
        public void ValidateCanDeleteTarget(User currentUser, User targetUser)
        {
            // 不能删除自己
            if (currentUser.Id == targetUser.Id)
            {
                throw new AccessGuardException("不能删除当前登录的账号");
            }

            // 不能删除最后一个管理员（包括超级管理员）
            if (targetUser.Role == UserRole.Administrator || targetUser.Role == UserRole.SuperAdministrator)
            {
                int adminCount = _userRepository.CountByRole(UserRole.Administrator);
                int superAdminCount = _userRepository.CountByRole(UserRole.SuperAdministrator);
                int totalAdminCount = adminCount + superAdminCount;
                
                if (totalAdminCount <= 1)
                {
                    throw new AccessGuardException("不能删除最后一个管理员账号");
                }
            }
        }
    }
}
