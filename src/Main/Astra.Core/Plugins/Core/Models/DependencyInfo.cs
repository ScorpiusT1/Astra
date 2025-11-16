namespace Astra.Core.Plugins.Models
{
    /// <summary>
    /// 依赖信息
    /// </summary>
    public class DependencyInfo
    {
        public string PluginId { get; set; }
        public VersionRange VersionRange { get; set; }
        public bool IsOptional { get; set; }
    }
}
