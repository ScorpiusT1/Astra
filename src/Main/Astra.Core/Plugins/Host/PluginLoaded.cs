using Astra.Core.Plugins.Abstractions;
using Astra.Core.Plugins.Loading;
using Astra.Core.Plugins.Models;

namespace Astra.Core.Plugins.Host
{
    public partial class PluginHost
    {
        private class PluginLoaded
        {
            /// <summary>
            /// 插件的静态描述信息（来自清单解析）。
            /// </summary>
            public PluginDescriptor Descriptor { get; set; }
            /// <summary>
            /// 插件运行时实例。
            /// </summary>
            public IPlugin Instance { get; set; }
            /// <summary>
            /// 该插件对应的可回收加载上下文。
            /// </summary>
            public PluginAssemblyLoadContext LoadContext { get; set; }
        }
    }
}
