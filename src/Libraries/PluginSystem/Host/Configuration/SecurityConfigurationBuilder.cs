using Addins.Core.Models;
using System;
using System.Collections.Generic;

namespace Addins.Host.Configuration
{
    /// <summary>
    /// 安全配置构建器 - 专门负责安全相关配置
    /// </summary>
    public class SecurityConfigurationBuilder
    {
        private readonly SecurityConfiguration _config;
        private readonly HostBuilder _hostBuilder;

        public SecurityConfigurationBuilder(SecurityConfiguration config, HostBuilder hostBuilder)
        {
            _config = config;
            _hostBuilder = hostBuilder;
        }

        /// <summary>
        /// 要求签名验证
        /// </summary>
        public SecurityConfigurationBuilder RequireSignature(bool require = true)
        {
            _config.RequireSignature = require;
            return this;
        }

        /// <summary>
        /// 启用沙箱
        /// </summary>
        public SecurityConfigurationBuilder EnableSandbox(bool enable = true)
        {
            _config.EnableSandbox = enable;
            return this;
        }

        /// <summary>
        /// 设置沙箱类型
        /// </summary>
        public SecurityConfigurationBuilder WithSandboxType(SandboxType sandboxType)
        {
            _config.SandboxType = sandboxType;
            return this;
        }

        /// <summary>
        /// 设置默认权限
        /// </summary>
        public SecurityConfigurationBuilder WithDefaultPermissions(params PluginPermissions[] permissions)
        {
            _config.DefaultPermissions.Clear();
            _config.DefaultPermissions.AddRange(permissions);
            return this;
        }

        /// <summary>
        /// 添加默认权限
        /// </summary>
        public SecurityConfigurationBuilder AddDefaultPermission(PluginPermissions permission)
        {
            if (!_config.DefaultPermissions.Contains(permission))
            {
                _config.DefaultPermissions.Add(permission);
            }
            return this;
        }

        /// <summary>
        /// 移除默认权限
        /// </summary>
        public SecurityConfigurationBuilder RemoveDefaultPermission(PluginPermissions permission)
        {
            _config.DefaultPermissions.Remove(permission);
            return this;
        }

        /// <summary>
        /// 配置安全
        /// </summary>
        public SecurityConfigurationBuilder Configure(Action<SecurityConfiguration> configure)
        {
            configure(_config);
            return this;
        }

        /// <summary>
        /// 返回HostBuilder以继续配置
        /// </summary>
        public HostBuilder And()
        {
            return _hostBuilder;
        }
    }
}
