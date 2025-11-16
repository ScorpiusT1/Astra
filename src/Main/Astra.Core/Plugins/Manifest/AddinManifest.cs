using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Astra.Core.Plugins.Manifest
{
    /// <summary>
    /// Addin 清单文件模型
    /// </summary>
    public class AddinManifest
    {
        public string Schema { get; set; } = "http://pluginsystem.org/schema/v1";
        public AddinInfo Addin { get; set; } = new();
        public List<ExtensionPoint> ExtensionPoints { get; set; } = new();
        public List<Extension> Extensions { get; set; } = new();
    }
}
