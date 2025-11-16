using System;
using Astra.Core.Plugins.Models;
using Astra.Core.Plugins.Exceptions;
using Microsoft.Extensions.Logging;

namespace Astra.Core.Plugins.Security
{
	/// <summary>
	/// 权限网关：统一封装对 <see cref="IPermissionManager"/> 的权限校验，
	/// 对敏感操作（文件/网络/反射等）提供语义化的检查入口，并记录审计日志。
	/// </summary>
	public sealed class PermissionGateway : IPermissionGateway
	{
		private readonly IPermissionManager _permissionManager;
		private readonly Astra.Core.Plugins.Exceptions.IErrorLogger _logger;

		public PermissionGateway(IPermissionManager permissionManager, Astra.Core.Plugins.Exceptions.IErrorLogger logger = null)
		{
			_permissionManager = permissionManager ?? throw new ArgumentNullException(nameof(permissionManager));
			_logger = logger ?? new ConsoleErrorLogger();
		}

		/// <summary>
		/// 检查指定插件是否具备某项具体权限。
		/// </summary>
		/// <param name="pluginId">插件标识</param>
		/// <param name="permission">所需权限位</param>
		public void Check(string pluginId, PluginPermissions permission)
		{
			_permissionManager.CheckPermission(pluginId, permission);
		}

		/// <summary>
		/// 检查并审计：文件系统访问权限。
		/// </summary>
		public void CheckFileSystem(string pluginId)
		{
			Check(pluginId, PluginPermissions.FileSystem);
			_logger.LogInfoAsync($"FS access granted for {pluginId}");
		}

		/// <summary>
		/// 检查并审计：网络访问权限。
		/// </summary>
		public void CheckNetwork(string pluginId)
		{
			Check(pluginId, PluginPermissions.Network);
			_logger.LogInfoAsync($"Network access granted for {pluginId}");
		}

		/// <summary>
		/// 检查并审计：数据库访问权限（当前使用 Network 权限作为代表）。
		/// </summary>
		public void CheckDatabase(string pluginId)
		{
			// 数据库访问通常基于网络，此处沿用 Network 权限作为代表
			Check(pluginId, PluginPermissions.Network);
			_logger.LogInfoAsync($"Database access granted for {pluginId}");
		}

		/// <summary>
		/// 检查并审计：反射访问权限。
		/// </summary>
		public void CheckReflection(string pluginId)
		{
			Check(pluginId, PluginPermissions.Reflection);
			_logger.LogInfoAsync($"Reflection access granted for {pluginId}");
		}
	}
}

