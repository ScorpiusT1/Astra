using System;
using System.Collections.Generic;

namespace Astra.Core.Configuration
{
    /// <summary>
    /// 配置容器接口
    /// </summary>
    public interface IConfigContainer<T> where T : class, IConfig
    {
        List<T> Configs { get; set; }
        DateTime LastModified { get; set; }
        string? Description { get; set; }
    }
}

