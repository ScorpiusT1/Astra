using System.Threading.Tasks;

namespace Astra.Core.Plugins.Security
{
	public interface ISecureFileSystem
	{
		Task<string> ReadAllTextAsync(string pluginId, string path, System.Text.Encoding encoding = null);
		Task WriteAllTextAsync(string pluginId, string path, string contents, System.Text.Encoding encoding = null, bool overwrite = true);
	}
}

