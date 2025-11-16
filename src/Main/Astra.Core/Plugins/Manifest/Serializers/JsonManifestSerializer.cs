using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Astra.Core.Plugins.Manifest.Serializers
{
    /// <summary>
    /// JSON 清单序列化器
    /// </summary>
    public class JsonManifestSerializer : IManifestSerializer
    {
        private readonly JsonSerializerOptions _options = new()
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        public AddinManifest Deserialize(Stream stream)
        {
            return JsonSerializer.Deserialize<AddinManifest>(stream, _options);
        }

        public void Serialize(AddinManifest manifest, Stream stream)
        {
            JsonSerializer.Serialize(stream, manifest, _options);
        }

        public bool CanHandle(string filePath)
        {
            return filePath.EndsWith(".json", StringComparison.OrdinalIgnoreCase);
        }
    }
}
