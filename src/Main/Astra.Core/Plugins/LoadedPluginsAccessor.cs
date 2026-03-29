using System;
using System.Collections.Generic;
using Astra.Core.Plugins.Abstractions;

namespace Astra.Core.Plugins
{
    /// <summary>
    /// 将 <see cref="IPluginHost.LoadedPlugins"/> 暴露为 <see cref="ILoadedPluginsAccessor"/>。
    /// </summary>
    public sealed class LoadedPluginsAccessor : ILoadedPluginsAccessor
    {
        private readonly IPluginHost _host;

        public LoadedPluginsAccessor(IPluginHost host)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));
        }

        public IReadOnlyList<IPlugin> LoadedPlugins => _host.LoadedPlugins;
    }
}
