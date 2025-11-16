using System;
using System.Threading.Tasks;
using Astra.Core.Plugins.Exceptions;

namespace Astra.Core.Plugins.Security
{
	/// <summary>
	/// 安全审计记录器：将安全相关事件统一写入 <see cref="IErrorLogger"/>，
	/// 通过统一前缀标识便于后续筛查。
	/// </summary>
	public sealed class SecurityAuditLogger : ISecurityAuditLogger
	{
		private readonly IErrorLogger _logger;

		public SecurityAuditLogger(IErrorLogger logger = null)
		{
			_logger = logger ?? new ConsoleErrorLogger();
		}

		/// <summary>
		/// 记录安全信息级别事件。
		/// </summary>
		/// <param name="message">事件消息</param>
		/// <param name="pluginId">关联插件Id（可选）</param>
		/// <returns>异步任务</returns>
		public Task InfoAsync(string message, string pluginId = null)
		{
			return _logger.LogInfoAsync($"[SECURITY][INFO] {message}", pluginId);
		}

		/// <summary>
		/// 记录安全警告级别事件。
		/// </summary>
		/// <param name="message">事件消息</param>
		/// <param name="pluginId">关联插件Id（可选）</param>
		/// <returns>异步任务</returns>
		public Task WarnAsync(string message, string pluginId = null)
		{
			return _logger.LogWarningAsync($"[SECURITY][WARN] {message}", pluginId);
		}

		/// <summary>
		/// 记录安全错误级别事件。
		/// </summary>
		/// <param name="message">事件消息</param>
		/// <param name="pluginId">关联插件Id（可选）</param>
		/// <param name="ex">可选异常</param>
		/// <returns>异步任务</returns>
		public Task ErrorAsync(string message, string pluginId = null, Exception ex = null)
		{
			if (ex != null)
			{
				return _logger.LogErrorAsync(ex, $"[SECURITY][ERROR] {message}");
			}
			return _logger.LogErrorAsync(new Exception($"[SECURITY][ERROR] {message}"), pluginId);
		}
	}
}

