using Astra.Core.Devices.Configuration;
using Astra.Core.Devices.Common;
using Astra.Core.Devices;
using Astra.Core.Devices.Attributes;
using Astra.Core.Foundation.Common;
using Astra.Plugins.PLC.ViewModels;
using Astra.Plugins.PLC.Views;
using Astra.UI.Abstractions.Attributes;
using System;
using System.Collections.Generic;

namespace Astra.Plugins.PLC.Configs
{
    [TreeNodeConfig("PLC", "🧠", typeof(PlcDeviceConfigView), typeof(PlcDeviceConfigViewModel), header: "通用PLC")]
    [ConfigUI(typeof(PlcDeviceConfigView), typeof(PlcDeviceConfigViewModel))]
    public class PlcDeviceConfig : DeviceConfig
    {
        private string _ip = "127.0.0.1";
        private ushort _port = 102;
        private int _connectTimeoutMs = 3000;
        private int _readWriteTimeoutMs = 3000;
        private bool _autoReconnect = true;
        private int _reconnectIntervalMs = 3000;

        public PlcDeviceConfig() : base()
        {
            ConfigName = "PLC设备";
            InitializeDeviceInfo(DeviceType.PLC);
        }

        public PlcDeviceConfig(string configId) : this()
        {
            ConfigId = configId;
        }

        /// <summary>
        /// PLC IP 地址
        /// </summary>
        [HotUpdatable]
        public string Ip
        {
            get => _ip;
            set => SetProperty(ref _ip, value);
        }

        /// <summary>
        /// PLC 端口（S7 默认 102）
        /// </summary>
        [HotUpdatable]
        public ushort Port
        {
            get => _port;
            set => SetProperty(ref _port, value);
        }

        /// <summary>
        /// 连接超时时间（毫秒）
        /// </summary>
        [RequireRestart("连接超时变更建议重连生效")]
        public int ConnectTimeoutMs
        {
            get => _connectTimeoutMs;
            set => SetProperty(ref _connectTimeoutMs, value);
        }

        /// <summary>
        /// 读写超时时间（毫秒）
        /// </summary>
        [HotUpdatable]
        public int ReadWriteTimeoutMs
        {
            get => _readWriteTimeoutMs;
            set => SetProperty(ref _readWriteTimeoutMs, value);
        }

        /// <summary>
        /// 是否自动重连
        /// </summary>
        [HotUpdatable]
        public bool AutoReconnect
        {
            get => _autoReconnect;
            set => SetProperty(ref _autoReconnect, value);
        }

        /// <summary>
        /// 自动重连间隔（毫秒）
        /// </summary>
        [HotUpdatable]
        public int ReconnectIntervalMs
        {
            get => _reconnectIntervalMs;
            set => SetProperty(ref _reconnectIntervalMs, value);
        }

        public override DeviceConfig Clone()
        {
            var json = Serialize();
            var clone = Deserialize<PlcDeviceConfig>(json);

            SetCloneMetadata(clone);
            return clone;
        }

        public override string GenerateDeviceId()
        {
            var serialOrAddress = string.IsNullOrWhiteSpace(SerialNumber)
                ? $"{Ip}:{Port}"
                : SerialNumber;

            return DeviceIdGenerator.Generate("PLC", GroupId, SlotId, serialOrAddress, DeviceName);
        }

        /// <summary>
        /// PLC 设备始终要求填写厂家与型号。
        /// </summary>
        protected override bool RequiresManufacturerAndModel() => true;

        public override OperationResult<bool> Validate()
        {
            var errors = new List<string>();

            var baseResult = base.Validate();
            if (!baseResult.Success && !string.IsNullOrWhiteSpace(baseResult.ErrorMessage))
            {
                errors.Add(baseResult.ErrorMessage);
            }

            if (string.IsNullOrWhiteSpace(Ip))
            {
                errors.Add("IP 地址不能为空");
            }

            if (Port == 0)
            {
                errors.Add("端口号必须大于 0");
            }

            if (ConnectTimeoutMs <= 0)
            {
                errors.Add("连接超时时间必须大于 0");
            }

            if (ReadWriteTimeoutMs <= 0)
            {
                errors.Add("读写超时时间必须大于 0");
            }

            if (ReconnectIntervalMs <= 0)
            {
                errors.Add("自动重连间隔必须大于 0");
            }

            return errors.Count > 0
                ? OperationResult<bool>.Failure(string.Join(Environment.NewLine, errors), ErrorCodes.InvalidConfig)
                : OperationResult<bool>.Succeed(true, "PLC 配置校验通过");
        }

        private static void SetCloneMetadata(PlcDeviceConfig clone)
        {
            if (clone == null)
            {
                throw new InvalidOperationException("克隆 PlcDeviceConfig 失败");
            }

            if (clone is Astra.Core.Configuration.Base.ConfigBase cloneConfigBase)
            {
                cloneConfigBase.SetConfigId(Guid.NewGuid().ToString());
            }
        }
    }
}
