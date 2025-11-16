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
    }
}
