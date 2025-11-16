using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Astra.Core.Plugins.Exceptions
{
    /// <summary>
    /// 插件系统异常基类
    /// </summary>
    public abstract class PluginSystemException : Exception
    {
        public string PluginId { get; }
        public string Operation { get; }
        public DateTime Timestamp { get; }
        public Dictionary<string, object> Context { get; }

        protected PluginSystemException(string message, string pluginId = null, string operation = null, Exception innerException = null)
            : base(message, innerException)
        {
            PluginId = pluginId;
            Operation = operation;
            Timestamp = DateTime.UtcNow;
            Context = new Dictionary<string, object>();
        }

        protected PluginSystemException(string message, Exception innerException)
            : base(message, innerException)
        {
            Timestamp = DateTime.UtcNow;
            Context = new Dictionary<string, object>();
        }
    }

    /// <summary>
    /// 插件加载异常
    /// </summary>
    public class PluginLoadException : PluginSystemException
    {
        public string AssemblyPath { get; }
        public string TypeName { get; }

        public PluginLoadException(string message, string pluginId, string assemblyPath, string typeName = null, Exception innerException = null)
            : base(message, pluginId, "Load", innerException)
        {
            AssemblyPath = assemblyPath;
            TypeName = typeName;
        }
    }

    /// <summary>
    /// 插件初始化异常
    /// </summary>
    public class PluginInitializationException : PluginSystemException
    {
        public PluginInitializationException(string message, string pluginId, Exception innerException = null)
            : base(message, pluginId, "Initialize", innerException)
        {
        }
    }

    /// <summary>
    /// 插件启动异常
    /// </summary>
    public class PluginStartException : PluginSystemException
    {
        public PluginStartException(string message, string pluginId, Exception innerException = null)
            : base(message, pluginId, "Start", innerException)
        {
        }
    }

    /// <summary>
    /// 插件停止异常
    /// </summary>
    public class PluginStopException : PluginSystemException
    {
        public PluginStopException(string message, string pluginId, Exception innerException = null)
            : base(message, pluginId, "Stop", innerException)
        {
        }
    }

    /// <summary>
    /// 插件卸载异常
    /// </summary>
    public class PluginUnloadException : PluginSystemException
    {
        public PluginUnloadException(string message, string pluginId, Exception innerException = null)
            : base(message, pluginId, "Unload", innerException)
        {
        }
    }

    /// <summary>
    /// 插件验证异常
    /// </summary>
    public class PluginValidationException : PluginSystemException
    {
        public List<string> ValidationErrors { get; }

        public PluginValidationException(string message, string pluginId, List<string> validationErrors, Exception innerException = null)
            : base(message, pluginId, "Validate", innerException)
        {
            ValidationErrors = validationErrors ?? new List<string>();
        }
    }

    /// <summary>
    /// 插件依赖异常
    /// </summary>
    public class PluginDependencyException : PluginSystemException
    {
        public string MissingDependency { get; }
        public string RequiredVersion { get; }

        public PluginDependencyException(string message, string pluginId, string missingDependency, string requiredVersion = null, Exception innerException = null)
            : base(message, pluginId, "Dependency", innerException)
        {
            MissingDependency = missingDependency;
            RequiredVersion = requiredVersion;
        }
    }

    /// <summary>
    /// 插件权限异常
    /// </summary>
    public class PluginPermissionException : PluginSystemException
    {
        public string RequiredPermission { get; }

        public PluginPermissionException(string message, string pluginId, string requiredPermission, Exception innerException = null)
            : base(message, pluginId, "Permission", innerException)
        {
            RequiredPermission = requiredPermission;
        }
    }

    /// <summary>
    /// 插件通信异常
    /// </summary>
    public class PluginCommunicationException : PluginSystemException
    {
        public string Topic { get; }
        public string MessageType { get; }

        public PluginCommunicationException(string message, string pluginId, string topic = null, string messageType = null, Exception innerException = null)
            : base(message, pluginId, "Communication", innerException)
        {
            Topic = topic;
            MessageType = messageType;
        }
    }

    /// <summary>
    /// 插件配置异常
    /// </summary>
    public class PluginConfigurationException : PluginSystemException
    {
        public string ConfigurationKey { get; }

        public PluginConfigurationException(string message, string pluginId, string configurationKey = null, Exception innerException = null)
            : base(message, pluginId, "Configuration", innerException)
        {
            ConfigurationKey = configurationKey;
        }
    }

    /// <summary>
    /// 插件超时异常
    /// </summary>
    public class PluginTimeoutException : PluginSystemException
    {
        public TimeSpan Timeout { get; }

        public PluginTimeoutException(string message, string pluginId, TimeSpan timeout, Exception innerException = null)
            : base(message, pluginId, "Timeout", innerException)
        {
            Timeout = timeout;
        }
    }

    /// <summary>
    /// 插件系统致命异常
    /// </summary>
    public class PluginSystemFatalException : PluginSystemException
    {
        public PluginSystemFatalException(string message, Exception innerException = null)
            : base(message, innerException)
        {
        }
    }
}
