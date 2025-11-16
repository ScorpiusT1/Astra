using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Astra.Core.Plugins.Manifest.Serializers
{
    /// <summary>
    /// YAML 清单序列化器  
    /// </summary>
    public class YamlManifestSerializer : IManifestSerializer
    {
        private readonly IDeserializer _deserializer;
        private readonly ISerializer _serializer;

        public YamlManifestSerializer()
        {
            _deserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build();

            _serializer = new SerializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build();
        }

        public AddinManifest Deserialize(Stream stream)
        {
            using var reader = new StreamReader(stream);
            var yaml = reader.ReadToEnd();
            return _deserializer.Deserialize<AddinManifest>(yaml);
        }

        public void Serialize(AddinManifest manifest, Stream stream)
        {
            using var writer = new StreamWriter(stream);
            var yaml = _serializer.Serialize(manifest);
            writer.Write(yaml);
        }

        public bool CanHandle(string filePath)
        {
            return filePath.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase) ||
                   filePath.EndsWith(".yml", StringComparison.OrdinalIgnoreCase);
        }
    }
}
