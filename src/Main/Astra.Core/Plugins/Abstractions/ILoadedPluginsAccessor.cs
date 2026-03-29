using System.Collections.Generic;

namespace Astra.Core.Plugins.Abstractions
{
    /// <summary>
    /// 只读访问当前宿主中已加载的插件列表（供 Astra.Engine 等组件使用，避免直接依赖 <see cref="IPluginHost"/> 的加载/卸载能力）。
    /// </summary>
    public interface ILoadedPluginsAccessor
    {
        /// <summary>已加载插件（顺序与加载顺序一致）。插件尚未完成加载时可能为空列表。</summary>
        IReadOnlyList<IPlugin> LoadedPlugins { get; }
    }
}
