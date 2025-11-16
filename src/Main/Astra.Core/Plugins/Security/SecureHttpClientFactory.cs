using System;
using System.Net.Http;

namespace Astra.Core.Plugins.Security
{
	/// <summary>
	/// 受权限网关保护的 <see cref="HttpClient"/> 工厂。
	/// 在创建客户端前执行网络访问权限校验，并记录审计日志。
	/// </summary>
	public sealed class SecureHttpClientFactory : ISecureHttpClientFactory, IDisposable
	{
		private readonly IPermissionGateway _gateway;
		private readonly ISecurityAuditLogger _audit;
		private readonly HttpMessageHandler _handler;

		/// <summary>
		/// 构造安全 <see cref="HttpClient"/> 工厂。
		/// </summary>
		/// <param name="gateway">权限网关</param>
		/// <param name="audit">安全审计记录器</param>
		/// <param name="handler">自定义消息处理管道（可选）</param>
		public SecureHttpClientFactory(IPermissionGateway gateway, ISecurityAuditLogger audit, HttpMessageHandler handler = null)
		{
			_gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
			_audit = audit ?? throw new ArgumentNullException(nameof(audit));
			_handler = handler ?? new HttpClientHandler();
		}

		/// <summary>
		/// 创建 <see cref="HttpClient"/> 实例（创建前做网络权限检查并记录审计）。
		/// </summary>
		/// <param name="pluginId">调用方插件Id</param>
		/// <returns>HttpClient 实例</returns>
		public HttpClient Create(string pluginId)
		{
			_gateway.CheckNetwork(pluginId);
			_audit?.InfoAsync("HttpClient created", pluginId);
			return new HttpClient(_handler, disposeHandler: false);
		}

		/// <summary>
		/// 释放内部消息处理器（如持有）。
		/// </summary>
		public void Dispose()
		{
			_handler?.Dispose();
		}
	}
}

