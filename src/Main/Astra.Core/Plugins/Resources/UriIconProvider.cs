using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Astra.Core.Plugins.Resources
{
    /// <summary>
    /// URI 图标提供程序 - 支持 HTTP/HTTPS
    /// </summary>
    public class UriIconProvider : IIconProvider
    {
        private static readonly HttpClient _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(10)
        };

        public async Task<byte[]> GetIconAsync(string path)
        {
            try
            {
                var response = await _httpClient.GetAsync(path);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsByteArrayAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to load icon from URI {path}: {ex.Message}");
                return null;
            }
        }

        public bool CanHandle(string path)
        {
            return Uri.TryCreate(path, UriKind.Absolute, out var uri) &&
                   (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
        }
    }
}
