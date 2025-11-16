using Astra.Core.Plugins.Manifest.Serializers;
using Astra.Core.Plugins.Validation;
using System;
using System.Collections.Generic;

namespace Astra.Core.Plugins.Host.Configuration
{
    /// <summary>
    /// 服务配置构建器 - 专门负责服务相关配置
    /// </summary>
    public class ServiceConfigurationBuilder
    {
        private readonly ServiceConfiguration _config;
        private readonly HostBuilder _hostBuilder;

        public ServiceConfigurationBuilder(ServiceConfiguration config, HostBuilder hostBuilder)
        {
            _config = config;
            _hostBuilder = hostBuilder;
        }

        /// <summary>
        /// 启用默认序列化器
        /// </summary>
        public ServiceConfigurationBuilder EnableDefaultSerializers(bool enable = true)
        {
            _config.EnableDefaultSerializers = enable;
            return this;
        }

        /// <summary>
        /// 启用默认验证规则
        /// </summary>
        public ServiceConfigurationBuilder EnableDefaultValidationRules(bool enable = true)
        {
            _config.EnableDefaultValidationRules = enable;
            return this;
        }

        /// <summary>
        /// 添加清单序列化器
        /// </summary>
        public ServiceConfigurationBuilder AddManifestSerializer<T>() where T : IManifestSerializer, new()
        {
            _config.ManifestSerializers.Add(typeof(T));
            return this;
        }

        /// <summary>
        /// 添加验证规则
        /// </summary>
        public ServiceConfigurationBuilder AddValidationRule<T>() where T : IValidationRule, new()
        {
            _config.ValidationRules.Add(typeof(T));
            return this;
        }

        /// <summary>
        /// 添加多个清单序列化器
        /// </summary>
        public ServiceConfigurationBuilder AddManifestSerializers(params Type[] serializers)
        {
            foreach (var serializer in serializers)
            {
                if (typeof(IManifestSerializer).IsAssignableFrom(serializer))
                {
                    _config.ManifestSerializers.Add(serializer);
                }
            }
            return this;
        }

        /// <summary>
        /// 添加多个验证规则
        /// </summary>
        public ServiceConfigurationBuilder AddValidationRules(params Type[] rules)
        {
            foreach (var rule in rules)
            {
                if (typeof(IValidationRule).IsAssignableFrom(rule))
                {
                    _config.ValidationRules.Add(rule);
                }
            }
            return this;
        }

        /// <summary>
        /// 配置服务
        /// </summary>
        public ServiceConfigurationBuilder Configure(Action<ServiceConfiguration> configure)
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
