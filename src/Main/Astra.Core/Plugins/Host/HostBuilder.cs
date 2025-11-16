using Astra.Core.Plugins.Abstractions;
using Astra.Core.Plugins.Host.Configuration;
using System;

namespace Astra.Core.Plugins.Host
{
    /// <summary>
    /// 插件宿主构建器 - 重构后的流畅 API，职责分离
    /// </summary>
    public class HostBuilder
    {
        private readonly HostConfiguration _config = new();

        public HostBuilder()
        {
            // 设置默认配置
            _config.Services.EnableDefaultSerializers = true;
            _config.Services.EnableDefaultValidationRules = true;
            _config.Performance.EnablePerformanceMonitoring = true;
            _config.Performance.EnableMemoryManagement = true;
            _config.Performance.EnableConcurrencyControl = true;
            _config.Performance.EnableCaching = true;
        }

        /// <summary>
        /// 设置插件目录
        /// </summary>
        public HostBuilder WithPluginDirectory(string directory)
        {
            _config.PluginDirectory = directory;
            return this;
        }

        /// <summary>
        /// 启用热重载
        /// </summary>
        public HostBuilder EnableHotReload(bool enable = true)
        {
            _config.EnableHotReload = enable;
            return this;
        }

        /// <summary>
        /// 配置服务
        /// </summary>
        public ServiceConfigurationBuilder ConfigureServices()
        {
            return new ServiceConfigurationBuilder(_config.Services, this);
        }

        /// <summary>
        /// 配置性能
        /// </summary>
        public PerformanceConfigurationBuilder ConfigurePerformance()
        {
            return new PerformanceConfigurationBuilder(_config.Performance, this);
        }

        /// <summary>
        /// 配置安全
        /// </summary>
        public SecurityConfigurationBuilder ConfigureSecurity()
        {
            return new SecurityConfigurationBuilder(_config.Security, this);
        }

        /// <summary>
        /// 构建默认主机
        /// </summary>
        public IPluginHost Build()
        {
            return PluginHostFactory.CreateDefaultHost(_config);
        }

        /// <summary>
        /// 构建高性能主机
        /// </summary>
        public IPluginHost BuildHighPerformance()
        {
            return PluginHostFactory.CreateHighPerformanceHost(_config);
        }

        /// <summary>
        /// 构建轻量级主机
        /// </summary>
        public IPluginHost BuildLightweight()
        {
            return PluginHostFactory.CreateLightweightHost(_config);
        }

        /// <summary>
        /// 构建开发环境主机
        /// </summary>
        public IPluginHost BuildDevelopment()
        {
            return PluginHostFactory.CreateDevelopmentHost(_config);
        }

        /// <summary>
        /// 构建生产环境主机
        /// </summary>
        public IPluginHost BuildProduction()
        {
            return PluginHostFactory.CreateProductionHost(_config);
        }

        /// <summary>
        /// 获取当前配置（用于调试）
        /// </summary>
        public HostConfiguration GetConfiguration()
        {
            return _config;
        }
    }
}