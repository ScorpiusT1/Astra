using Astra.Core.Devices;
using Astra.Core.Devices.Attributes;
using Astra.Core.Devices.Common;
using Astra.Core.Devices.Configuration;
using Astra.Core.Foundation.Common;
using Astra.Plugins.DataAcquisition.ViewModels;
using Astra.Plugins.DataAcquisition.Views;
using Astra.UI.Abstractions.Attributes;
using System;
using System.Collections.Generic;

namespace Astra.Plugins.DataAcquisition.Devices
{
    [DeviceConfigUI(typeof(DataAcquisitionDeviceConfigView), typeof(DataAcquisitionDeviceConfigViewModel))]
    [TreeNodeConfig("采集卡", "📊", typeof(DataAcquisitionDeviceConfigView), typeof(DataAcquisitionDeviceConfigViewModel))]
    public class DataAcquisitionConfig : DeviceConfig
    {
        private string _serialNumber = string.Empty;

        public DataAcquisitionConfig() : base()
        {
            ConfigType = typeof(DataAcquisitionConfig);
            ConfigId = Guid.NewGuid().ToString();
            InitializeDeviceInfo(DeviceType.DataAcquisition);
           
        }

        /// <summary>
        /// 设备序列号
        /// </summary>
        public string SerialNumber
        {
            get => _serialNumber;
            set
            {
                var oldValue = _serialNumber;
                SetProperty(ref _serialNumber, value);

                if (!string.Equals(oldValue, value, StringComparison.OrdinalIgnoreCase))
                {
                    DeviceId = GenerateDeviceId();
                }
            }
        }

        private double _sampleRate = 51200.0;
        private int _channelCount = 8;
        private int _bufferSize = 8_192;
        private bool _autoStart = true;

        /// <summary>
        /// 采样率（Hz）
        /// </summary>
        [HotUpdatable]
        public double SampleRate
        {
            get => _sampleRate;
            set => SetProperty(ref _sampleRate, value);
        }

        /// <summary>
        /// 通道数量
        /// </summary>
        [HotUpdatable]
        public int ChannelCount
        {
            get => _channelCount;
            set => SetProperty(ref _channelCount, value);
        }

        /// <summary>
        /// 单帧缓冲区大小（采样点）
        /// </summary>
        [RequireRestart("缓冲区大小变更需要重新建立采集缓存")]
        public int BufferSize
        {
            get => _bufferSize;
            set => SetProperty(ref _bufferSize, value);
        }

        /// <summary>
        /// 插件启动时是否自动开始采集
        /// </summary>
        public bool AutoStart
        {
            get => _autoStart;
            set => SetProperty(ref _autoStart, value);
        }

        public override DeviceConfig Clone()
        {
            var clone = new DataAcquisitionConfig
            {
                DeviceName = DeviceName,
                SerialNumber = SerialNumber,
                SampleRate = SampleRate,
                ChannelCount = ChannelCount,
                BufferSize = BufferSize,
                AutoStart = AutoStart,
                IsEnabled = IsEnabled,
                GroupId = GroupId,
                SlotId = SlotId,
                // IConfig 接口属性
                Version = Version,              
                ModifiedAt = ModifiedAt,
                ConfigName = ConfigName
            };
            return clone;
        }

        public override string GenerateDeviceId()
        {
            return DeviceIdGenerator.Generate("DAQ", GroupId, SlotId, SerialNumber, DeviceName);
        }

        public override OperationResult<bool> Validate()
        {
            var errors = new List<string>();

            var baseResult = base.Validate();
            if (!baseResult.Success && !string.IsNullOrWhiteSpace(baseResult.ErrorMessage))
            {
                errors.Add(baseResult.ErrorMessage);
            }

            if (SampleRate <= 0)
            {
                errors.Add("采样率必须大于 0 Hz");
            }

            if (ChannelCount <= 0)
            {
                errors.Add("通道数量必须大于 0");
            }

            if (BufferSize <= 0)
            {
                errors.Add("缓冲区大小必须大于 0");
            }

            if (errors.Count > 0)
            {
                return OperationResult<bool>.Failure(string.Join(Environment.NewLine, errors));
            }

            return OperationResult<bool>.Succeed(true, "采集卡配置验证通过");
        }
    }
}
