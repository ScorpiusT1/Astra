using Astra.Core.Access;
using Astra.Core.Access.Models;
using Astra.UI.Helpers;
using System;

namespace Astra.Services.Authorization
{
    /// <summary>
    /// 权限验证策略接口 - 策略模式
    /// </summary>
    public interface IPermissionStrategy
    {
        /// <summary>
        /// 验证用户是否有权限执行操作
        /// </summary>
        bool HasPermission(User user, string operation);

        /// <summary>
        /// 获取权限拒绝消息
        /// </summary>
        string GetDeniedMessage(User user, string operation);
    }

    /// <summary>
    /// 管理员权限策略（包括超级管理员和管理员）
    /// </summary>
    public class AdministratorPermissionStrategy : IPermissionStrategy
    {
        public bool HasPermission(User user, string operation)
        {
            return user?.Role == UserRole.Administrator || user?.Role == UserRole.SuperAdministrator;
        }

        public string GetDeniedMessage(User user, string operation)
        {
            return $"权限不足：只有管理员才能执行 '{operation}' 操作\n" +
                   $"当前用户: {user?.Username ?? "未登录"}\n" +
                   $"当前权限: {user?.Role.ToString() ?? "无"}";
        }
    }

    /// <summary>
    /// 工程师及以上权限策略（包括超级管理员、管理员和工程师）
    /// </summary>
    public class EngineerOrAbovePermissionStrategy : IPermissionStrategy
    {
        public bool HasPermission(User user, string operation)
        {
            if (user == null) return false;
            return user.Role == UserRole.SuperAdministrator 
                || user.Role == UserRole.Administrator 
                || user.Role == UserRole.Engineer;
        }

        public string GetDeniedMessage(User user, string operation)
        {
            return $"权限不足：需要工程师或管理员权限才能执行 '{operation}' 操作\n" +
                   $"当前用户: {user?.Username ?? "未登录"}\n" +
                   $"当前权限: {user?.Role.ToString() ?? "无"}";
        }
    }

    /// <summary>
    /// 权限服务 - 统一管理权限验证
    /// </summary>
    public interface IPermissionService
    {
        /// <summary>
        /// 验证权限
        /// </summary>
        bool CheckPermission(User user, string operation, IPermissionStrategy strategy);

        /// <summary>
        /// 验证权限并显示拒绝消息
        /// </summary>
        bool CheckPermissionWithMessage(User user, string operation, IPermissionStrategy strategy);
    }

    /// <summary>
    /// 权限服务实现
    /// </summary>
    public class PermissionService : IPermissionService
    {
        public bool CheckPermission(User user, string operation, IPermissionStrategy strategy)
        {
            if (strategy == null)
                throw new ArgumentNullException(nameof(strategy));

            return strategy.HasPermission(user, operation);
        }

        public bool CheckPermissionWithMessage(User user, string operation, IPermissionStrategy strategy)
        {
            if (strategy == null)
                throw new ArgumentNullException(nameof(strategy));

            bool hasPermission = strategy.HasPermission(user, operation);
            
            if (!hasPermission)
            {
                string message = strategy.GetDeniedMessage(user, operation);
                ToastHelper.ShowWarning(message, "权限不足");
            }

            return hasPermission;
        }
    }
}
