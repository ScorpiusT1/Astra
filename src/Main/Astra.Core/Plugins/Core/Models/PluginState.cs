namespace Astra.Core.Plugins.Models
{
    /// <summary>
    /// 插件状态枚举
    /// </summary>
    public enum PluginState
    {
        Discovered,
        Loading,
        Loaded,
        Initializing,
        Running,
        Stopping,
        Stopped,
        Failed,
        Unloading
    }
}
