using Astra.Core.Devices.Abstractions;
using Astra.Core.Devices.Configuration;
using Astra.Core.Devices.Interfaces;
using Astra.Plugins.DataAcquisition.Devices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Astra.Plugins.DataAcquisition.Factories
{
    /// <summary>
    /// MGS 数据采集设备工厂
    /// </summary>
    public class MGSDataAcquisitionFactory : DeviceFactoryBase<DataAcquisitionConfig, DataAcquisitionDevice>
    {
        public override bool CanCreate(DeviceConfig config)
        {
            return config is DataAcquisitionConfig daqConfig
                && !string.IsNullOrWhiteSpace(daqConfig.Manufacturer)
                && daqConfig.Manufacturer.Equals("MGS", System.StringComparison.OrdinalIgnoreCase);
        }

        protected override DataAcquisitionDevice CreateDevice(
            DataAcquisitionConfig config, 
            IServiceProvider serviceProvider = null)
        {
            // 从服务提供器获取依赖
            var messageBus = serviceProvider?.GetService<Astra.Core.Plugins.Messaging.IMessageBus>();
            
            // 从 ILoggerFactory 创建日志器
            Microsoft.Extensions.Logging.ILogger logger = null;
            var loggerFactory = serviceProvider?.GetService<Microsoft.Extensions.Logging.ILoggerFactory>();
            if (loggerFactory != null)
            {
                logger = loggerFactory.CreateLogger<DataAcquisitionDevice>();
            }

            // 注意：如果将来需要创建 MGSDataAcquisitionDevice 类，可以在这里替换
            // 目前使用通用的 DataAcquisitionDevice
            return new DataAcquisitionDevice(config, messageBus, logger);
        }
    }
}

