using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Addins.Resources
{
    /// <summary>
    /// 图标缓存管理
    /// </summary>
    public interface IIconProvider
    {
        Task<byte[]> GetIconAsync(string path);
        bool CanHandle(string path);
    }
}
