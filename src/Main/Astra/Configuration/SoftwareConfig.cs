using System.Collections.ObjectModel;
using Astra.Core.Configuration.Base;
using Astra.UI.Abstractions.Attributes;

namespace Astra.Configuration
{
    /// <summary>
    /// 软件级测试配置：线体 / 工站 / DUT 数量与映射关系
    /// </summary>
    [TreeNodeConfig("软件配置", "🖥", typeof(Astra.Views.SoftwareConfigView), typeof(Astra.ViewModels.SoftwareConfigViewModel), order: 5, header: null, AllowAddOnRoot = false)]
    [ConfigUI(typeof(Astra.Views.SoftwareConfigView), typeof(Astra.ViewModels.SoftwareConfigViewModel))]
    public class SoftwareConfig : ConfigBase
    {
        private string _lineName = string.Empty;
        private string _stationName = string.Empty;
        private int _slotCount = 1;
        private int _groupCount = 1;
        private int _dutCount = 1;
        private ObservableCollection<DutConfig> _duts = new ObservableCollection<DutConfig>();
        private bool _isSyncingCounts;

        public SoftwareConfig() : base()
        {
            EnsureDutCollection();
        }

        public SoftwareConfig(string configId) : base(configId)
        {
            EnsureDutCollection();
        }

        /// <summary>线体名称</summary>
        public string LineName
        {
            get => _lineName;
            set => SetProperty(ref _lineName, value);
        }

        /// <summary>工站名称</summary>
        public string StationName
        {
            get => _stationName;
            set => SetProperty(ref _stationName, value);
        }

        /// <summary>
        /// 测试产品数量（DUT 数量）。
        /// 兼容字段：实际数量由 <see cref="SlotCount"/> × <see cref="GroupCount"/> 推导。
        /// 变更时会自动调整 <see cref="Duts"/> 集合长度。
        /// </summary>
        public int DutCount
        {
            get => _dutCount;
            set
            {
                if (value < 1) value = 1;
                if (SetProperty(ref _dutCount, value))
                {
                    if (!_isSyncingCounts)
                    {
                        _isSyncingCounts = true;
                        try
                        {
                            _groupCount = 1;
                            OnPropertyChanged(nameof(GroupCount));
                            _slotCount = value;
                            OnPropertyChanged(nameof(SlotCount));
                        }
                        finally
                        {
                            _isSyncingCounts = false;
                        }
                    }
                    SyncDutCollection();
                }
            }
        }

        /// <summary>
        /// 每个 Group 中的 Slot 数。
        /// </summary>
        public int SlotCount
        {
            get => _slotCount;
            set
            {
                if (value < 1) value = 1;
                if (SetProperty(ref _slotCount, value))
                {
                    UpdateDutCountFromSlotGroup();
                }
            }
        }

        /// <summary>
        /// Group 数量。
        /// </summary>
        public int GroupCount
        {
            get => _groupCount;
            set
            {
                if (value < 1) value = 1;
                if (SetProperty(ref _groupCount, value))
                {
                    UpdateDutCountFromSlotGroup();
                }
            }
        }

        /// <summary>
        /// DUT 配置列表，下标从 0 开始，对应 DUT1..N。
        /// </summary>
        public ObservableCollection<DutConfig> Duts
        {
            get => _duts;
            set
            {
                if (value == null) value = new ObservableCollection<DutConfig>();
                SetProperty(ref _duts, value);
                SyncDutCollection();
            }
        }

        /// <summary>
        /// 显示名称：优先使用 ConfigName，其次用“线体-工站”组合。
        /// </summary>
        public override string GetDisplayName()
        {
            if (!string.IsNullOrWhiteSpace(ConfigName))
                return ConfigName;

            if (!string.IsNullOrWhiteSpace(LineName) && !string.IsNullOrWhiteSpace(StationName))
                return $"{LineName}-{StationName}";

            if (!string.IsNullOrWhiteSpace(StationName))
                return StationName;

            return "软件配置";
        }

        private void EnsureDutCollection()
        {
            if (_duts == null)
            {
                _duts = new ObservableCollection<DutConfig>();
            }

            if (_duts.Count == 0)
            {
                UpdateDutCountFromSlotGroup();
                SyncDutCollection();
            }
        }

        private void UpdateDutCountFromSlotGroup()
        {
            if (_isSyncingCounts)
                return;

            int desired = _slotCount * _groupCount;
            if (desired < 1) desired = 1;

            if (_dutCount == desired)
            {
                SyncDutCollection();
                return;
            }

            _isSyncingCounts = true;
            try
            {
                _dutCount = desired;
                OnPropertyChanged(nameof(DutCount));
            }
            finally
            {
                _isSyncingCounts = false;
            }

            SyncDutCollection();
        }

        /// <summary>
        /// 根据 DutCount 自动增删 DUT 项。
        /// </summary>
        private void SyncDutCollection()
        {
            if (_duts == null)
                _duts = new ObservableCollection<DutConfig>();

            while (_duts.Count > _dutCount)
            {
                _duts.RemoveAt(_duts.Count - 1);
            }

            while (_duts.Count < _dutCount)
            {
                int index = _duts.Count + 1;
                _duts.Add(new DutConfig
                {
                    Index = index,
                    Name = $"DUT{index}"
                });
            }

            for (int i = 0; i < _duts.Count; i++)
            {
                var dut = _duts[i];
                dut.Index = i + 1;
                if (string.IsNullOrWhiteSpace(dut.Name))
                {
                    dut.Name = $"DUT{dut.Index}";
                }
            }
        }
    }

    /// <summary>
    /// 单个 DUT 的配置：脚本与触发器映射。
    /// 作为 SoftwareConfig 的子对象存在，不单独作为 IConfig。
    /// </summary>
    public class DutConfig : ConfigBase
    {
        private int _index;
        private string _name = string.Empty;
        private string _workflowId = string.Empty;
        private string _workflowName = string.Empty;
        private string _triggerConfigId = string.Empty;
        private string _triggerName = string.Empty;

        public DutConfig() : base() { }

        public DutConfig(string configId) : base(configId) { }

        /// <summary>DUT 编号（1..N）</summary>
        public int Index
        {
            get => _index;
            set => SetProperty(ref _index, value);
        }

        /// <summary>DUT 显示名称，例如 DUT1、DUT2</summary>
        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        /// <summary>
        /// 绑定的测试脚本标识（例如工作流 ID）。
        /// 具体含义由上层工作流管理模块解释。
        /// </summary>
        public string WorkflowId
        {
            get => _workflowId;
            set => SetProperty(ref _workflowId, value);
        }

        /// <summary>用于界面显示的脚本名称</summary>
        public string WorkflowName
        {
            get => _workflowName;
            set => SetProperty(ref _workflowName, value);
        }

        /// <summary>
        /// 绑定的触发器配置 ID，对应 TriggerConfig 的 ConfigId。
        /// </summary>
        public string TriggerConfigId
        {
            get => _triggerConfigId;
            set => SetProperty(ref _triggerConfigId, value);
        }

        /// <summary>用于界面显示的触发器名称</summary>
        public string TriggerName
        {
            get => _triggerName;
            set => SetProperty(ref _triggerName, value);
        }

        public override string GetDisplayName()
        {
            if (!string.IsNullOrWhiteSpace(Name))
                return Name;
            return $"DUT{Index}";
        }
    }
}

