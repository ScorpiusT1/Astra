using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Astra.Core.Plugins.Security
{
	/// <summary>
	/// 受权限网关保护的文件系统访问门面。
	/// 所有调用需提供 <c>pluginId</c> 以执行权限校验与审计。
	/// </summary>
	public sealed class SecureFileSystem : ISecureFileSystem
	{
		private readonly IPermissionGateway _gateway;
		private readonly ISecurityAuditLogger _audit;

		public SecureFileSystem(IPermissionGateway gateway, ISecurityAuditLogger audit)
		{
			_gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
			_audit = audit ?? throw new ArgumentNullException(nameof(audit));
		}

		/// <summary>
		/// 读取文本文件（带文件系统权限校验与审计）。
		/// </summary>
		/// <param name="pluginId">调用方插件Id</param>
		/// <param name="path">文件路径</param>
		/// <param name="encoding">文本编码，默认UTF8</param>
		/// <returns>文件内容</returns>
		public async Task<string> ReadAllTextAsync(string pluginId, string path, Encoding encoding = null)
		{
			_gateway.CheckFileSystem(pluginId);
			_audit?.InfoAsync($"File read: {Sanitize(path)}", pluginId);
			return await Task.Run(() => File.ReadAllText(path, encoding ?? Encoding.UTF8));
		}

		/// <summary>
		/// 写入文本文件（带文件系统权限校验与审计）。
		/// </summary>
		/// <param name="pluginId">调用方插件Id</param>
		/// <param name="path">文件路径</param>
		/// <param name="contents">要写入的内容</param>
		/// <param name="encoding">文本编码，默认UTF8</param>
		/// <param name="overwrite">是否覆盖已存在文件</param>
		/// <exception cref="IOException">当 overwrite=false 且目标已存在时抛出</exception>
		public async Task WriteAllTextAsync(string pluginId, string path, string contents, Encoding encoding = null, bool overwrite = true)
		{
			_gateway.CheckFileSystem(pluginId);
			_audit?.InfoAsync($"File write: {Sanitize(path)} overwrite={overwrite}", pluginId);
			await Task.Run(() =>
			{
				if (!overwrite && File.Exists(path)) throw new IOException("File already exists");
				File.WriteAllText(path, contents ?? string.Empty, encoding ?? Encoding.UTF8);
			});
		}

		private static string Sanitize(string path) => SafeExceptionFormatter.Sanitize(path);
	}
}

