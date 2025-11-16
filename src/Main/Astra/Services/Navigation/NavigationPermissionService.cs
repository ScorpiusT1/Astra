using Astra.Core.Access;
using Astra.Core.Access.Models;
using Astra.UI.Helpers;
using NavStack.Modularity;
using Astra.Utilities;
using Astra.Core.Access.Models;

namespace Astra.Services.Navigation
{
    /// <summary>
    /// 导航权限服务接口
    /// </summary>
    public interface INavigationPermissionService
    {
        /// <summary>
        /// 检查用户是否有权限访问指定菜单项
        /// </summary>
        bool HasPermission(User user, NavigationMenuItem menuItem);

        /// <summary>
        /// 根据用户权限筛选菜单项
        /// </summary>
        IEnumerable<NavigationMenuItem> FilterMenuItems(User user, IEnumerable<NavigationMenuItem> menuItems);

        /// <summary>
        /// 检查导航权限并显示提示消息
        /// </summary>
        bool CheckNavigationPermissionWithMessage(User user, NavigationMenuItem menuItem);

        /// <summary>
        /// 更新所有菜单项的可见性
        /// </summary>
        void UpdateMenuItemsVisibility(User user, IEnumerable<NavigationMenuItem> menuItems);
    }

    /// <summary>
    /// 导航权限服务实现
    /// </summary>
    public class NavigationPermissionService : INavigationPermissionService
    {
        /// <summary>
        /// 检查用户是否有权限访问指定菜单项
        /// </summary>
        public bool HasPermission(User user, NavigationMenuItem menuItem)
        {
            if (menuItem == null)
                return false;

            // 如果没有设置权限要求，默认所有用户可访问；但对特定页面做安全兜底
            int requiredLevel = menuItem.RequiredPermissionLevel;
            if (string.Equals(menuItem.NavigationKey, NavigationKeys.Permission, StringComparison.OrdinalIgnoreCase) && requiredLevel == 0)
            {
                requiredLevel = 3; // 管理员
            }

            bool isHome = string.Equals(menuItem.NavigationKey, NavigationKeys.Home, StringComparison.OrdinalIgnoreCase);

            // 为关键页面设置默认最低权限（即使未显式配置 RequiredPermissionLevel）
            if (string.Equals(menuItem.NavigationKey, NavigationKeys.Config, StringComparison.OrdinalIgnoreCase)
                || string.Equals(menuItem.NavigationKey, NavigationKeys.Debug, StringComparison.OrdinalIgnoreCase)
                || string.Equals(menuItem.NavigationKey, NavigationKeys.Sequence, StringComparison.OrdinalIgnoreCase))
            {
                if (requiredLevel < 2) // 工程师及以上
                {
                    requiredLevel = 2;
                }
            }

            // 未登录场景：只允许首页在未设权限时可见，其它默认不放开
            if (user == null)
            {
                if (requiredLevel == 0)
                    return isHome; // 仅首页可见
            }

            // 操作员场景：如果页面未显式配置权限，则默认仅首页可见（避免误放开）
            if (user != null && user.Role == UserRole.Operator && requiredLevel == 0 && !isHome)
            {
                return false;
            }

            // 未登录用户没有任何权限
            if (user == null)
                return false;

            // 检查用户角色等级是否满足要求（将枚举映射为等级：操作员=1, 工程师=2, 管理员=3）
            int userLevel = user.Role switch
            {
                UserRole.Operator => 1,
                UserRole.Engineer => 2,
                UserRole.Administrator => 3,
                _ => 0
            };
            return requiredLevel == 0 ? true : userLevel >= requiredLevel;
        }

        /// <summary>
        /// 根据用户权限筛选菜单项
        /// </summary>
        public IEnumerable<NavigationMenuItem> FilterMenuItems(User user, IEnumerable<NavigationMenuItem> menuItems)
        {
            if (menuItems == null)
                return Enumerable.Empty<NavigationMenuItem>();

            return menuItems.Where(item => HasPermission(user, item));
        }

        /// <summary>
        /// 检查导航权限并显示提示消息
        /// </summary>
        public bool CheckNavigationPermissionWithMessage(User user, NavigationMenuItem menuItem)
        {
            if (menuItem == null)
                return false;

            bool hasPermission = HasPermission(user, menuItem);

            if (!hasPermission)
            {
                string message = GetPermissionDeniedMessage(user, menuItem);
                ToastHelper.ShowWarning(message, "权限不足");
            }

            return hasPermission;
        }

        /// <summary>
        /// 更新所有菜单项的可见性
        /// </summary>
        public void UpdateMenuItemsVisibility(User user, IEnumerable<NavigationMenuItem> menuItems)
        {
            if (menuItems == null)
                return;

            foreach (var menuItem in menuItems)
            {
                menuItem.IsVisible = HasPermission(user, menuItem);

                // 递归处理子菜单
                if (menuItem.SubItems != null && menuItem.SubItems.Count > 0)
                {
                    UpdateMenuItemsVisibility(user, menuItem.SubItems);
                }
            }

            System.Diagnostics.Debug.WriteLine(
                $"[NavigationPermissionService] 已更新菜单可见性, 用户: {user?.Username ?? "未登录"}, " +
                $"权限级别: {(user != null ? (int)user.Role : 0)}");
        }

        /// <summary>
        /// 获取权限拒绝消息
        /// </summary>
        private string GetPermissionDeniedMessage(User user, NavigationMenuItem menuItem)
        {
            // 如果菜单项定义了自定义消息，使用自定义消息
            if (!string.IsNullOrEmpty(menuItem.PermissionDeniedMessage))
            {
                return $"{menuItem.PermissionDeniedMessage}\n\n" +
                       $"当前用户: {user?.Username ?? "未登录"}\n" +
                       $"当前权限: {GetRoleName(user?.Role)}\n" +
                       $"所需权限: {GetRequiredRoleName(menuItem.RequiredPermissionLevel)}";
            }

            // 默认消息
            return $"您没有权限访问 '{menuItem.Title}'\n\n" +
                   $"当前用户: {user?.Username ?? "未登录"}\n" +
                   $"当前权限: {GetRoleName(user?.Role)}\n" +
                   $"所需权限: {GetRequiredRoleName(menuItem.RequiredPermissionLevel)}";
        }

        /// <summary>
        /// 获取角色名称
        /// </summary>
        private string GetRoleName(UserRole? role)
        {
            return role switch
            {
                UserRole.Administrator => "管理员",
                UserRole.Engineer => "工程师",
                UserRole.Operator => "操作员",
                null => "未登录",
                _ => role.ToString()
            };
        }

        /// <summary>
        /// 获取所需权限级别的角色名称
        /// </summary>
        private string GetRequiredRoleName(int level)
        {
            return level switch
            {
                0 => "所有用户",
                1 => "操作员及以上",
                2 => "工程师及以上",
                3 => "管理员",
                _ => $"权限级别 {level}"
            };
        }
    }
}
