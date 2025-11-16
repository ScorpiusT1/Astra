using Addins.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Addins.Core.Discovery
{
    /// <summary>
    /// 文件系统插件发现 - 自动发现插件
    /// </summary>
    public interface IPluginDiscovery
    {
        Task<IEnumerable<PluginDescriptor>> DiscoverAsync(string path);
    }
}
