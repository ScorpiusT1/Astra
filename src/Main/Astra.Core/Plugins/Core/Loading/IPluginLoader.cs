using Astra.Core.Plugins.Abstractions;
using Astra.Core.Plugins.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Astra.Core.Plugins.Loading
{
    /// <summary>
    /// 插件加载器接口
    /// </summary>
    public interface IPluginLoader
    {
        /// <summary>
        /// 从程序集路径加载插件
        /// </summary>
        Task<IPlugin> LoadAsync(PluginDescriptor descriptor);

        /// <summary>
        /// 卸载插件
        /// </summary>
        Task UnloadAsync(string pluginId);

        /// <summary>
        /// 重新加载插件
        /// </summary>
        Task<IPlugin> ReloadAsync(string pluginId);

        /// <summary>
		/// 获取加载上下文
        /// </summary>
		PluginAssemblyLoadContext GetLoadContext(string pluginId);
    }
}
