using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Astra.Core.Plugins.Models
{
    /// <summary>
    /// 插件描述符 - 数据传输对象
    /// </summary>
    public class PluginDescriptor
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public Version Version { get; set; }
        public string Description { get; set; }
        public string Author { get; set; }
        public string AssemblyPath { get; set; }
        public string TypeName { get; set; }
        public List<DependencyInfo> Dependencies { get; set; } = new();
        public Dictionary<string, string> Properties { get; set; } = new();
        public PluginState State { get; set; }
        public DateTime LoadedTime { get; set; }
        public string IconPath { get; set; }
        public PluginPermissions Permissions { get; set; }
    }
}
