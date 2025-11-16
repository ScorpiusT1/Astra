using System;
using Astra.Core.Plugins.Models;

namespace Astra.Core.Plugins.Security
{
	public interface IPermissionGateway
	{
		void Check(string pluginId, PluginPermissions permission);

		// 语义化检查，便于调用方可读
		void CheckFileSystem(string pluginId);
	 void CheckNetwork(string pluginId);
		void CheckDatabase(string pluginId);
		void CheckReflection(string pluginId);
	}
}

