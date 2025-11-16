namespace Addins.Management.Commands
{
    /// <summary>
    /// 插件信息
    /// </summary>
    public class PluginInfo
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Version { get; set; }
        public string State { get; set; }
        public bool IsEnabled { get; set; }
    }
}
