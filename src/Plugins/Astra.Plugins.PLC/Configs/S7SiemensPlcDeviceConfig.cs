using Astra.Core.Devices.Common;
using Astra.Core.Devices.Configuration;
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
    [TreeNodeConfig("西门子PLC", "🧠", typeof(S7SiemensPlcDeviceConfigView), typeof(S7SiemensPlcDeviceConfigViewModel), header: "西门子PLC")]
    [ConfigUI(typeof(S7SiemensPlcDeviceConfigView), typeof(S7SiemensPlcDeviceConfigViewModel))]
    public class S7SiemensPlcDeviceConfig : PlcDeviceConfig
    {
        private ushort _rack = 0;
        private ushort _slot = 1;

        public S7SiemensPlcDeviceConfig() : base()
        {
        }

        public S7SiemensPlcDeviceConfig(string configId) : this()
        {
            ConfigId = configId;
        }

        /// <summary>
        /// 机架号（S7 常见为 0）
        /// </summary>
        [HotUpdatable]
        public ushort Rack
        {
            get => _rack;
            set => SetProperty(ref _rack, value);
        }

        /// <summary>
        /// 槽号（S7 常见为 1）
        /// </summary>
        [HotUpdatable]
        public ushort Slot
        {
            get => _slot;
            set => SetProperty(ref _slot, value);
        }

        public override DeviceConfig Clone()
        {
            var json = Serialize();
            var clone = Deserialize<S7SiemensPlcDeviceConfig>(json);

            if (clone == null)
            {
                throw new InvalidOperationException("克隆 S7SiemensPlcDeviceConfig 失败");
            }

            if (clone is Astra.Core.Configuration.Base.ConfigBase cloneConfigBase)
            {
                cloneConfigBase.SetConfigId(Guid.NewGuid().ToString());
            }

            return clone;
        }

        public override string GenerateDeviceId()
        {
            var serialOrAddress = string.IsNullOrWhiteSpace(SerialNumber)
                ? $"{Ip}:{Port}/R{Rack}S{Slot}"
                : SerialNumber;

            return DeviceIdGenerator.Generate("PLC-S7", GroupId, SlotId, serialOrAddress, DeviceName);
        }

        public override OperationResult<bool> Validate()
        {
            var errors = new List<string>();

            var baseResult = base.Validate();
            if (!baseResult.Success && !string.IsNullOrWhiteSpace(baseResult.ErrorMessage))
            {
                errors.Add(baseResult.ErrorMessage);
            }

            return errors.Count > 0
                ? OperationResult<bool>.Failure(string.Join(Environment.NewLine, errors), ErrorCodes.InvalidConfig)
                : OperationResult<bool>.Succeed(true, "S7 西门子 PLC 配置校验通过");
        }
    }
}
