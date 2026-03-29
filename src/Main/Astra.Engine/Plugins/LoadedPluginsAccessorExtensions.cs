using System;
using Astra.Core.Plugins.Abstractions;

namespace Astra.Engine.Plugins
{
    /// <summary>
    /// 配合 <see cref="ILoadedPluginsAccessor"/> 使用的查询扩展（Engine 内通过 DI 注入访问器即可枚举全部已加载插件）。
    /// </summary>
    public static class LoadedPluginsAccessorExtensions
    {
        public static IPlugin? FindById(this ILoadedPluginsAccessor accessor, string pluginId)
        {
            if (accessor == null || string.IsNullOrWhiteSpace(pluginId))
            {
                return null;
            }

            var id = pluginId.Trim();
            foreach (var p in accessor.LoadedPlugins)
            {
                if (p != null && string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase))
                {
                    return p;
                }
            }

            return null;
        }
    }
}
