using Addins.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Text;
using System.Threading.Tasks;

namespace Addins.Security
{
    /// <summary>
    /// 权限管理器 - 安全沙箱机制
    /// </summary>
    public interface IPermissionManager
    {
        void GrantPermission(string pluginId, PluginPermissions permission);
        void RevokePermission(string pluginId, PluginPermissions permission);
        bool HasPermission(string pluginId, PluginPermissions permission);
        void CheckPermission(string pluginId, PluginPermissions permission);
    }

    public class PermissionManager : IPermissionManager
    {
        private readonly Dictionary<string, PluginPermissions> _permissions = new();

        public void GrantPermission(string pluginId, PluginPermissions permission)
        {
            if (!_permissions.ContainsKey(pluginId))
                _permissions[pluginId] = PluginPermissions.None;

            _permissions[pluginId] |= permission;
        }

        public void RevokePermission(string pluginId, PluginPermissions permission)
        {
            if (_permissions.ContainsKey(pluginId))
            {
                _permissions[pluginId] &= ~permission;
            }
        }

        public bool HasPermission(string pluginId, PluginPermissions permission)
        {
            if (!_permissions.TryGetValue(pluginId, out var granted))
                return false;

            return (granted & permission) == permission;
        }

        public void CheckPermission(string pluginId, PluginPermissions permission)
        {
            if (!HasPermission(pluginId, permission))
            {
                throw new SecurityException(
                    $"Plugin '{pluginId}' does not have permission: {permission}");
            }
        }
    }
}
