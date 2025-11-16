using System;
using System.Threading.Tasks;

namespace Astra.Core.Plugins.Security
{
	public interface ISecurityAuditLogger
	{
		Task InfoAsync(string message, string pluginId = null);
		Task WarnAsync(string message, string pluginId = null);
		Task ErrorAsync(string message, string pluginId = null, Exception ex = null);
	}
}

