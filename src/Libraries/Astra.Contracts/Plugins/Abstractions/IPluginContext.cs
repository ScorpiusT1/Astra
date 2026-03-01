using Astra.Core.Plugins.Messaging;
using Microsoft.Extensions.Logging;

namespace Astra.Core.Plugins.Abstractions
{
    /// <summary>
    /// 插件上下文接口（共享契约：小而稳定）
    /// </summary>
    public interface IPluginContext
    {
        /// <summary>
        /// 标准 .NET 服务提供器（用于 ILogger/IOptions 等）。
        /// </summary>
        IServiceProvider ServiceProvider { get; }

        /// <summary>
        /// 消息总线（插件间通信）。
        /// </summary>
        IMessageBus MessageBus { get; }

        /// <summary>
        /// 日志实例（按插件命名）。
        /// </summary>
        ILogger Logger { get; }

        /// <summary>
        /// 插件物理目录路径。
        /// </summary>
        string PluginDirectory { get; }
    }
}

