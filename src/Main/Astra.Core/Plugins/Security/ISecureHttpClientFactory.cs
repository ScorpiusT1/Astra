using System.Net.Http;

namespace Astra.Core.Plugins.Security
{
	public interface ISecureHttpClientFactory
	{
		HttpClient Create(string pluginId);
	}
}

