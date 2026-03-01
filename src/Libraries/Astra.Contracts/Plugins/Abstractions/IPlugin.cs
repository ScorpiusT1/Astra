using System;
using System.Threading;
using System.Threading.Tasks;
using Astra.Core.Plugins.Health;

namespace Astra.Core.Plugins.Abstractions
{
	/// <summary>
	/// 插件基础接口（共享契约：小而稳定）
	/// </summary>
	public interface IPlugin : IDisposable, IAsyncDisposable
	{
		string Id { get; }
		string Name { get; }
		Version Version { get; }

		/// <summary>
		/// 初始化插件。
		/// </summary>
		Task InitializeAsync(IPluginContext context, CancellationToken cancellationToken = default);

		/// <summary>
		/// 启用（进入工作状态）。
		/// </summary>
		Task OnEnableAsync(CancellationToken cancellationToken = default);

		/// <summary>
		/// 禁用（退出工作状态）。
		/// </summary>
		Task OnDisableAsync(CancellationToken cancellationToken = default);

		/// <summary>
		/// 健康检查。
		/// </summary>
		Task<HealthCheckResult> CheckHealthAsync();

		/// <summary>
		/// 异步释放。
		/// </summary>
		ValueTask DisposeAsync();
	}
}

