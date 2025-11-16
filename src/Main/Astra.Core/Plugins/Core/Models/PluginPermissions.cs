namespace Astra.Core.Plugins.Models
{
    /// <summary>
    /// 插件权限
    /// </summary>
    [Flags]
    public enum PluginPermissions
    {
        None = 0,
        FileSystem = 1 << 0,
        Network = 1 << 1,
        Registry = 1 << 2,
        Reflection = 1 << 3,
        NativeCode = 1 << 4,
        EnvironmentVariables = 1 << 5,
        All = ~0
    }
}
