namespace Addins.Management.Commands
{
    /// <summary>
    /// 管理命令
    /// </summary>
    public class AdminCommand
    {
        public CommandType Type { get; set; }
        public string PluginId { get; set; }
        public Dictionary<string, object> Parameters { get; set; } = new();
    }
}
