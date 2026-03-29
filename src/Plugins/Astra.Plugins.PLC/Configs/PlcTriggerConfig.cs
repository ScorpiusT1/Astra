using Astra.Core.Triggers.Configuration;
using Astra.Plugins.PLC.ViewModels;
using Astra.Plugins.PLC.Views;
using Astra.UI.Abstractions.Attributes;

namespace Astra.Plugins.PLC.Configs
{
    /// <summary>
    /// PLC 触发器配置：是否限制重复触发、相同信号最小间隔、PLC 与监控 IO 等。
    /// </summary>
    [TreeNodeConfig("PLC触发器", "⏱", typeof(PlcTriggerConfigView), typeof(PlcTriggerConfigViewModel), order: 7)]
    [ConfigUI(typeof(PlcTriggerConfigView), typeof(PlcTriggerConfigViewModel))]
    public class PlcTriggerConfig : TriggerBaseConfig
    {
        private bool _isEnabled = true;
        private AntiRepeatConfig _antiRepeat = new AntiRepeatConfig();
        private string _plcDeviceName = string.Empty;
        private string _ioPointName = string.Empty;

        public PlcTriggerConfig() : base() { }

        public PlcTriggerConfig(string configId) : base(configId) { }

        /// <summary>是否启用该触发器配置</summary>
        public bool IsEnabled
        {
            get => _isEnabled;
            set => SetProperty(ref _isEnabled, value);
        }

        /// <summary>
        /// 防重复：<see cref="AntiRepeatConfig.Enabled"/> 为 true 时，对相同信号在 <see cref="AntiRepeatConfig.MinIntervalMs"/> 内的重复触发进行抑制。
        /// </summary>
        public AntiRepeatConfig AntiRepeat
        {
            get => _antiRepeat;
            set => SetProperty(ref _antiRepeat, value);
        }

        /// <summary>PLC 设备名称（与设备配置中的名称一致）</summary>
        public string PlcDeviceName
        {
            get => _plcDeviceName;
            set => SetProperty(ref _plcDeviceName, value ?? string.Empty);
        }

        /// <summary>监控的 IO 名称（来自 IO 配置中的名称）</summary>
        public string IoPointName
        {
            get => _ioPointName;
            set => SetProperty(ref _ioPointName, value ?? string.Empty);
        }

        public override string GetDisplayName()
        {
            if (!string.IsNullOrWhiteSpace(ConfigName))
                return ConfigName;
            return "PLC触发器";
        }
    }
}
