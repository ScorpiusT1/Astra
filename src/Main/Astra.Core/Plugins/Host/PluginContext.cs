using Astra.Core.Plugins.Configuration;
using Astra.Core.Plugins.Abstractions;
using Astra.Core.Plugins.Messaging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Astra.Core.Plugins.Host
{
    public partial class PluginHost
    {
        private class PluginContext : IPluginContext
        {
            /// <summary>
            /// 宿主服务注册表（兼容存量接口）。
            /// </summary>
            public IServiceRegistry Services { get; set; }
            /// <summary>
            /// 消息总线（插件间通信）。
            /// </summary>
            public IMessageBus MessageBus { get; set; }
            /// <summary>
            /// 简易配置存储（与 IConfiguration 并存）。
            /// </summary>
            public IConfigurationStore Configuration { get; set; }
            /// <summary>
            /// 标准 .NET 服务提供器（用于 ILogger/IOptions 等）。
            /// </summary>
            public IServiceProvider ServiceProvider { get; set; }
            /// <summary>
            /// 标准配置根（支持热重载）。
            /// </summary>
            public IConfiguration ConfigurationRoot { get; set; }
            /// <summary>
            /// 日志实例（按插件命名）。
            /// </summary>
            public ILogger Logger { get; set; }
            /// <summary>
            /// 事件总线别名（与 MessageBus 等价）。
            /// </summary>
            public IMessageBus EventBus { get; set; }
            /// <summary>
            /// 插件物理目录路径。
            /// </summary>
            public string PluginDirectory { get; set; }
            /// <summary>
            /// 宿主接口。
            /// </summary>
            public IPluginHost Host { get; set; }
            /// <summary>
            /// 权限网关（敏感操作统一校验入口）。
            /// </summary>
            public Security.IPermissionGateway PermissionGateway { get; set; }
        }
    }
}
