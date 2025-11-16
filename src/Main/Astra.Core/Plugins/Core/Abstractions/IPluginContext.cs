using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Astra.Core.Plugins.Configuration;
using Astra.Core.Plugins.Messaging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Astra.Core.Plugins.Abstractions
{

    /// <summary>
    /// 插件上下文接口 - 接口隔离原则
    /// </summary>
    public interface IPluginContext
    {
		/// <summary>
		/// 宿主服务注册表（兼容存量接口）。
		/// </summary>
		IServiceRegistry Services { get; }

		/// <summary>
		/// 消息总线（插件间通信）。
		/// </summary>
		IMessageBus MessageBus { get; }

		/// <summary>
		/// 简易配置存储（与 IConfiguration 并存）。
		/// </summary>
		IConfigurationStore Configuration { get; }

		/// <summary>
		/// 标准 .NET 服务提供器（用于 ILogger/IOptions 等）。
		/// </summary>
		IServiceProvider ServiceProvider { get; }

		/// <summary>
		/// 标准配置根（支持热重载）。
		/// </summary>
		Microsoft.Extensions.Configuration.IConfiguration ConfigurationRoot { get; }

		/// <summary>
		/// 日志（按插件命名）。
		/// </summary>
		ILogger Logger { get; }

		/// <summary>
		/// 事件总线别名（与 MessageBus 等价）。
		/// </summary>
		IMessageBus EventBus { get; }

		/// <summary>
		/// 插件物理目录。
		/// </summary>
		string PluginDirectory { get; }

		/// <summary>
		/// 权限网关（敏感操作统一校验入口）。
		/// </summary>
		Security.IPermissionGateway PermissionGateway { get; }

		/// <summary>
		/// 宿主接口。
		/// </summary>
		IPluginHost Host { get; }
    }
}
