namespace Astra.Core.Plugins.Abstractions
{
    /// <summary>
    /// 插件宿主接口
    /// </summary>
    public interface IPluginHost
    {
        IReadOnlyList<IPlugin> LoadedPlugins { get; }
        Task<IPlugin> LoadPluginAsync(string path);
        Task UnloadPluginAsync(string pluginId);
        Task<T> GetServiceAsync<T>() where T : class;
        
        /// <summary>
        /// 从指定目录发现插件并按依赖顺序加载
        /// </summary>
        /// <param name="pluginDirectory">插件根目录</param>
        Task DiscoverAndLoadPluginsAsync(string pluginDirectory);
    }
}
