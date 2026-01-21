using Astra.Core.Devices;
using Astra.Core.Devices.Specifications;
using Astra.Plugins.DataAcquisition.Configs;
using System.Collections.Generic;

namespace Astra.Plugins.DataAcquisition.Specifications
{
    /// <summary>
    /// 数据采集设备规格初始化器
    /// </summary>
    public static class DataAcquisitionSpecificationInitializer
    {
        /// <summary>
        /// 初始化所有数据采集设备规格
        /// </summary>
        public static void Initialize()
        {
            var specifications = new List<IDeviceSpecification>();

            // BRC 厂家规格
            // BRC 型号1
            specifications.Add(new DeviceSpecification
            {
                DeviceType = DeviceType.DataAcquisition,
                Manufacturer = "BRC",
                Model = "BRC6804",
                DisplayName = "BRC - 6804",
                Description = "BRC厂家数据采集卡，4通道，最高102.4kHz采样率",
                Constraints = new Dictionary<string, object>
                {
                    ["ChannelCount"] = 4,
                    ["MaxSampleRate"] = 102400.0,
                    ["MinSampleRate"] = 1000.0,
                    ["MaxBufferSize"] = 16384,
                    ["MinBufferSize"] = 1024,
                    ["AllowedSampleRates"] = new List<double>
                    {
                         1024.0, 1280.0, 1563.0, 1920.0, 2560.0, 3072.0,
                        3413.333, 3657.143, 3938.462, 4266.667, 4654.545,
                        5120.0, 5688.889, 6400.0, 7314.286, 8533.333,
                        10240.0, 12800.0, 17066.667, 25600.0, 48000.0, 51200.0,
                        102400.0
                    },
                    // 支持的耦合方式（触发方式：AC / DC）
                    ["AllowedCouplingModes"] = new List<CouplingMode>
                    {
                        CouplingMode.AC,
                        CouplingMode.DC
                    },
                    // 支持的触发电平（单位：mA）
                    ["AllowedTriggerLevels"] = new List<double>
                    {
                        0.0,
                        4.0
                    }
                },
                Capabilities = new Dictionary<string, object>
                {
                    ["SupportsSyncSampling"] = true
                }
            });

            // BRC 型号2
            specifications.Add(new DeviceSpecification
            {
                DeviceType = DeviceType.DataAcquisition,
                Manufacturer = "BRC",
                Model = "BRC6809",
                DisplayName = $"BRC - 6809",
                Description = "BRC厂家数据采集卡，16通道，最高102.4kHz采样率",
                Constraints = new Dictionary<string, object>
                {
                    ["ChannelCount"] = 16,
                    ["MaxSampleRate"] = 102400.0,
                    ["MinSampleRate"] = 1000.0,
                    ["MaxBufferSize"] = 32768,
                    ["MinBufferSize"] = 2048,
                    ["AllowedSampleRates"] = new List<double>
                    {
                        1000.0, 2000.0, 4000.0, 8000.0, 10240.0, 12800.0,
                        16000.0, 20000.0, 25600.0, 32000.0, 40000.0, 51200.0,
                        64000.0, 80000.0, 102400.0
                    },
                    ["AllowedCouplingModes"] = new List<CouplingMode>
                    {
                        CouplingMode.AC,
                        CouplingMode.DC
                    },
                    ["AllowedTriggerLevels"] = new List<double>
                    {
                        0.0,
                        4.0
                    }
                },
                Capabilities = new Dictionary<string, object>
                {
                    ["SupportsSyncSampling"] = true
                }
            });

            // MGS 厂家规格
            // MGS 型号1
            specifications.Add(new DeviceSpecification
            {
                DeviceType = DeviceType.DataAcquisition,
                Manufacturer = "MGS",
                Model = "Model1",
                DisplayName = "MGS - 型号1",
                Description = "MGS厂家数据采集卡，32通道，最高204.8kHz采样率",
                Constraints = new Dictionary<string, object>
                {
                    ["ChannelCount"] = 32,
                    ["MaxSampleRate"] = 204800.0,
                    ["MinSampleRate"] = 1000.0,
                    ["MaxBufferSize"] = 65536,
                    ["MinBufferSize"] = 4096,
                    ["AllowedSampleRates"] = new List<double>
                    {
                        1000.0, 2000.0, 4000.0, 8000.0, 10240.0, 12800.0,
                        16000.0, 20000.0, 25600.0, 32000.0, 40000.0, 51200.0,
                        64000.0, 80000.0, 102400.0, 128000.0, 160000.0, 204800.0
                    },
                    //["AllowedCouplingModes"] = new List<CouplingMode>
                    //{
                    //    CouplingMode.AC,
                    //    CouplingMode.DC
                    //},
                    //["AllowedTriggerLevels"] = new List<double>
                    //{
                    //    0.0,
                    //    4.0
                    //}
                },
                Capabilities = new Dictionary<string, object>
                {
                    ["SupportsSyncSampling"] = false
                }
            });

            // MGS 型号2
            specifications.Add(new DeviceSpecification
            {
                DeviceType = DeviceType.DataAcquisition,
                Manufacturer = "MGS",
                Model = "Model2",
                DisplayName = "MGS - 型号2",
                Description = "MGS厂家数据采集卡，64通道，最高409.6kHz采样率",
                Constraints = new Dictionary<string, object>
                {
                    ["ChannelCount"] = 64,
                    ["MaxSampleRate"] = 409600.0,
                    ["MinSampleRate"] = 1000.0,
                    ["MaxBufferSize"] = 131072,
                    ["MinBufferSize"] = 8192,
                    ["AllowedSampleRates"] = new List<double>
                    {
                        1000.0, 2000.0, 4000.0, 8000.0, 10240.0, 12800.0,
                        16000.0, 20000.0, 25600.0, 32000.0, 40000.0, 51200.0,
                        64000.0, 80000.0, 102400.0, 128000.0, 160000.0, 204800.0,
                        256000.0, 320000.0, 409600.0
                    },
                    //["AllowedCouplingModes"] = new List<CouplingMode>
                    //{
                    //    CouplingMode.AC,
                    //    CouplingMode.DC
                    //},
                    //["AllowedTriggerLevels"] = new List<double>
                    //{
                    //    0.0,
                    //    4.0
                    //}
                },
                Capabilities = new Dictionary<string, object>
                {
                    ["SupportsSyncSampling"] = true
                }
            });

            DeviceSpecificationRegistry.RegisterRange(specifications);
        }
    }
}

