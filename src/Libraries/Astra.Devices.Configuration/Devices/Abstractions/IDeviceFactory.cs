using System;
using Astra.Core.Devices.Configuration;
using Astra.Core.Devices.Interfaces;

namespace Astra.Core.Devices.Abstractions
{
    /// <summary>
    /// 定义设备创建工厂的基础接口
    /// </summary>
    public interface IDeviceFactory
    {
        bool CanCreate(DeviceConfig config);
        IDevice Create(DeviceConfig config, IServiceProvider serviceProvider = null);
    }
}

