using System;
using System.Collections.Generic;
using Astra.Core.Foundation.Common;

namespace Astra.Core.Devices.Interfaces
{
    /// <summary>
    /// 可配置接口
    /// </summary>
    public interface IConfigurable<TConfig> where TConfig : Configuration.DeviceConfig
    {
        TConfig CurrentConfig { get; }
        OperationResult ApplyConfig(TConfig newConfig);
        OperationResult<bool> ValidateConfig(TConfig config);
        List<string> GetHotUpdateableProperties();
        event EventHandler<Configuration.ConfigChangedEventArgs<TConfig>> ConfigChanged;
    }
}

