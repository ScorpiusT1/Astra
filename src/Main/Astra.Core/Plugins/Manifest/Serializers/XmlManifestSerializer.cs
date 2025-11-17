using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace Astra.Core.Plugins.Manifest.Serializers
{
    /// <summary>
    /// XML 清单序列化器
    /// </summary>
    public class XmlManifestSerializer : IManifestSerializer
    {
        private XmlSerializer _serializer;

        private XmlSerializer Serializer
        {
            get
            {
                if (_serializer == null)
                {
                    // 延迟初始化，避免在构造函数中抛出异常
                    _serializer = new XmlSerializer(typeof(AddinManifest));
                }
                return _serializer;
            }
        }

        public AddinManifest Deserialize(Stream stream)
        {
            return Serializer.Deserialize(stream) as AddinManifest;
        }

        public void Serialize(AddinManifest manifest, Stream stream)
        {
            Serializer.Serialize(stream, manifest);
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
