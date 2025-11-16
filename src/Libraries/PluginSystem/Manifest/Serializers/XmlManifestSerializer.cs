using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace Addins.Manifest.Serializers
{
    /// <summary>
    /// XML 清单序列化器
    /// </summary>
    public class XmlManifestSerializer : IManifestSerializer
    {
        private readonly XmlSerializer _serializer = new(typeof(AddinManifest));

        public AddinManifest Deserialize(Stream stream)
        {
            return _serializer.Deserialize(stream) as AddinManifest;
        }

        public void Serialize(AddinManifest manifest, Stream stream)
        {
            _serializer.Serialize(stream, manifest);
        }

        public bool CanHandle(string filePath)
        {
            return filePath.EndsWith(".addin", StringComparison.OrdinalIgnoreCase) ||
                   filePath.EndsWith(".xml", StringComparison.OrdinalIgnoreCase);
        }
    }

    public interface IManifestSerializer
    {
        AddinManifest Deserialize(Stream stream);
        void Serialize(AddinManifest manifest, Stream stream);
        bool CanHandle(string filePath);
    }
}
