using Astra.Core.Configuration.Base;
using Astra.Core.Triggers.Configuration;
using Astra.Core.Triggers.Enums;
using Astra.UI.Abstractions.Attributes;

namespace Astra.Engine.Triggers
{
    /// <summary>
    /// 通用触发器配置：描述一个逻辑触发器及其参数。
    /// </summary>
    [TreeNodeConfig("触发器", "⏱", typeof(Astra.Engine.Views.TriggerConfigView), typeof(Astra.Engine.ViewModels.TriggerConfigViewModel), order: 7)]
    [ConfigUI(typeof(Astra.Engine.Views.TriggerConfigView), typeof(Astra.Engine.ViewModels.TriggerConfigViewModel))]
    public class TriggerConfig : ConfigBase
    {
        private string _displayName = string.Empty;
        private TriggerSource _source = TriggerSource.ManualScan;
        private WorkMode _workMode = WorkMode.Manual;
        private bool _isEnabled = true;
        private string _engineTypeName = string.Empty;
        private AntiRepeatConfig _antiRepeat = new AntiRepeatConfig();
        private string _plcIdentifier = string.Empty;
        private string _plcMonitorAddress = string.Empty;
        private string _plcSnAddress = string.Empty;

        public TriggerConfig() : base() { }

        public TriggerConfig(string configId) : base(configId) { }

        /// <summary>触发器名称（界面显示）</summary>
        public string DisplayName
        {
            get => _displayName;
            set => SetProperty(ref _displayName, value);
        }

        /// <summary>触发来源（扫码 / PLC / 手动等）</summary>
        public TriggerSource Source
        {
            get => _source;
            set => SetProperty(ref _source, value);
        }

        /// <summary>工作模式（单机 / 线体等）</summary>
        public WorkMode WorkMode
        {
            get => _workMode;
            set => SetProperty(ref _workMode, value);
        }

        /// <summary>是否启用</summary>
        public bool IsEnabled
        {
            get => _isEnabled;
            set => SetProperty(ref _isEnabled, value);
        }

        /// <summary>
        /// 触发器引擎类型全名，例如某个 TriggerBase 派生类的 AssemblyQualifiedName。
        /// 由运行时通过反射解析。
        /// </summary>
        public string EngineTypeName
        {
            get => _engineTypeName;
            set => SetProperty(ref _engineTypeName, value);
        }

        /// <summary>防重复触发设置</summary>
        public AntiRepeatConfig AntiRepeat
        {
            get => _antiRepeat;
            set => SetProperty(ref _antiRepeat, value);
        }

        /// <summary>PLC 标识（例如设备名称或 IP，用于区分具体 PLC）</summary>
        public string PlcIdentifier
        {
            get => _plcIdentifier;
            set => SetProperty(ref _plcIdentifier, value);
        }

        /// <summary>PLC 监控 IO 点位地址（例如 M0.0、DB1.DBX0.0 等）</summary>
        public string PlcMonitorAddress
        {
            get => _plcMonitorAddress;
            set => SetProperty(ref _plcMonitorAddress, value);
        }

        /// <summary>PLC 读取 SN 的地址（可选），未配置时使用固定 SN</summary>
        public string PlcSnAddress
        {
            get => _plcSnAddress;
            set => SetProperty(ref _plcSnAddress, value);
        }

        public override string GetDisplayName()
        {
            if (!string.IsNullOrWhiteSpace(ConfigName))
                return ConfigName;
            if (!string.IsNullOrWhiteSpace(DisplayName))
                return DisplayName;
            return "触发器";
        }
    }
}

