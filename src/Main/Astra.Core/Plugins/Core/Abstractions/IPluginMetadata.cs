using Astra.Core.Plugins.Models;

namespace Astra.Core.Plugins.Abstractions
{
    /// <summary>
    /// 插件元数据接口
    /// </summary>
    public interface IPluginMetadata
    {
        string Id { get; }
        string Name { get; }
        Version Version { get; }
        string Description { get; }
        string Author { get; }
        IReadOnlyList<DependencyInfo> Dependencies { get; }
        IReadOnlyDictionary<string, string> Properties { get; }
    }
}
