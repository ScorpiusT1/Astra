using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace Astra.Core.Plugins.Manifest
{
    /// <summary>
    /// Addin 清单文件模型
    /// </summary>
    public class AddinManifest
    {
        public string Schema { get; set; } = "http://pluginsystem.org/schema/v1";
        public AddinInfo Addin { get; set; } = new();
        
        [XmlArray("ExtensionPoints")]
        [XmlArrayItem("ExtensionPoint")]
        public List<ExtensionPoint> ExtensionPoints { get; set; } = new();
        
        [XmlArray("Extensions")]
        [XmlArrayItem("Extension")]
        public List<Extension> Extensions { get; set; } = new();
        
        /// <summary>
        /// 节点列表 - 插件提供的节点工具项
        /// </summary>
        [XmlArray("Nodes")]
        [XmlArrayItem("NodeInfo")]
        public List<NodeInfo> Nodes { get; set; } = new();
    }
}
