using System;
using Astra.Core.Devices.Configuration;
using Astra.Core.Devices.Interfaces;

namespace Astra.Core.Devices.Abstractions
{
    /// <summary>
    /// 泛型设备工厂基类，约定配置类型与设备类型
    /// </summary>
    public abstract class DeviceFactoryBase<TConfig, TDevice> : IDeviceFactory
        where TConfig : DeviceConfig
        where TDevice : IDevice
    {
        public virtual bool CanCreate(DeviceConfig config) => config is TConfig;

        public IDevice Create(DeviceConfig config, IServiceProvider serviceProvider = null)
        {
            if (config is not TConfig typedConfig)
            {
                throw new ArgumentException($"配置类型 {config?.GetType().FullName} 与工厂要求的 {typeof(TConfig).FullName} 不匹配", nameof(config));
            }

            return CreateDevice(typedConfig, serviceProvider);
        }

        /// <summary>
        /// 子类实现具体设备构造逻辑
        /// </summary>
        protected abstract TDevice CreateDevice(TConfig config, IServiceProvider serviceProvider = null);
    }
}


