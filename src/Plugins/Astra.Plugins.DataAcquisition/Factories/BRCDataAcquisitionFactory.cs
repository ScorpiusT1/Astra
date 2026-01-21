using Astra.Core.Devices.Abstractions;
using Astra.Core.Devices.Configuration;
using Astra.Core.Devices.Interfaces;
using Astra.Plugins.DataAcquisition.Devices;
using Microsoft.Extensions.DependencyInjection;

namespace Astra.Plugins.DataAcquisition.Factories
{
    /// <summary>
    /// BRC 数据采集设备工厂
    /// </summary>
    public class BRCDataAcquisitionFactory : DeviceFactoryBase<DataAcquisitionConfig, BRCDataAcquisitionDevice>
    {
        public override bool CanCreate(DeviceConfig config)
        {
            return config is DataAcquisitionConfig daqConfig
                && !string.IsNullOrWhiteSpace(daqConfig.Manufacturer)
                && daqConfig.Manufacturer.Equals("BRC", System.StringComparison.OrdinalIgnoreCase);
        }

        protected override BRCDataAcquisitionDevice CreateDevice(
            DataAcquisitionConfig config, 
            IServiceProvider serviceProvider = null)
        {
            // 从服务提供器获取依赖
            var messageBus = serviceProvider?.GetService<Astra.Core.Plugins.Messaging.IMessageBus>();
            var logger = serviceProvider?.GetService<Astra.Core.Logs.ILogger>();

            return new BRCDataAcquisitionDevice(config, messageBus, logger);
        }
    }
}

