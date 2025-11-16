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
        /// <summary>
        /// 判断工厂是否支持给定的配置类型
        /// </summary>
        /// <param name="config">设备配置实例</param>
        /// <returns>能否处理该配置</returns>
        bool CanCreate(DeviceConfig config);

        /// <summary>
        /// 根据配置创建设备实例
        /// </summary>
        /// <param name="config">设备配置</param>
        /// <param name="serviceProvider">可选服务提供器</param>
        /// <returns>设备实例</returns>
        IDevice Create(DeviceConfig config, IServiceProvider serviceProvider = null);
    }
}


