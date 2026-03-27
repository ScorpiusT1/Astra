using Astra.Core.Configuration.Abstractions;
using Astra.Core.Configuration.Base;
using Astra.Core.Foundation.Common;
using Astra.Plugins.PLC.ViewModels;
using Astra.Plugins.PLC.Views;
using Astra.UI.Abstractions.Attributes;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Astra.Plugins.PLC.Configs
{
    public enum PlcIODataType
    {
        Auto = 0,
        Bool,
        Int16,
        UInt16,
        Int32,
        UInt32,
        Float,
        Double,
        String
    }

    [TreeNodeConfig("IO配置", "🔌", typeof(PlcIoConfigView), typeof(PlcIoConfigViewModel), order: 6, header: "PLC IO点位", AllowAddOnRoot = false)]
    [ConfigUI(typeof(PlcIoConfigView), typeof(PlcIoConfigViewModel))]
    public class IOConfig : ConfigBase
    {
        private ObservableCollection<IoPointModel> _ios = new();
        private bool _enableDragReorder = true;

        /// <summary>
        /// IO 点位集合（Name/Address/PipeLabel/Tag 等均定义在 <see cref="IoPointModel"/>）。
        /// </summary>
        public ObservableCollection<IoPointModel> IOs
        {
            get => _ios;
            set => SetProperty(ref _ios, value ?? new ObservableCollection<IoPointModel>());
        }

        /// <summary>
        /// 是否允许在 IO 列表中拖拽调整顺序。
        /// </summary>
        public bool EnableDragReorder
        {
            get => _enableDragReorder;
            set => SetProperty(ref _enableDragReorder, value);
        }

        public IOConfig()
        {
            ConfigName = "IO配置";
            if (string.IsNullOrWhiteSpace(ConfigId))
            {
                SetConfigId(Guid.NewGuid().ToString());
            }
        }

        public IOConfig(string configId) : this()
        {
            ConfigId = configId;
        }

        public override string GetDisplayName()
        {
            return string.IsNullOrWhiteSpace(ConfigName) ? "IO配置" : ConfigName.Trim();
        }

        public override IConfig Clone()
        {
            var json = Serialize();
            var clone = Deserialize<IOConfig>(json);
            if (clone is ConfigBase cloneConfigBase)
            {
                cloneConfigBase.SetConfigId(Guid.NewGuid().ToString());
            }

            return clone ?? throw new InvalidOperationException("克隆 IOConfig 失败");
        }

        public OperationResult<bool> Validate()
        {
            var errors = new List<string>();

            if (IOs == null)
            {
                errors.Add("IOs 不能为空");
            }
            else
            {
                foreach (var io in IOs)
                {
                    if (io == null)
                    {
                        continue;
                    }

                    var r = io.Validate();
                    if (!r.Success && !string.IsNullOrWhiteSpace(r.ErrorMessage))
                    {
                        errors.Add($"[{io.Name}] {r.ErrorMessage}");
                    }
                }

                var duplicated = IOs
                    .Where(i => i != null && !string.IsNullOrWhiteSpace(i.Name))
                    .GroupBy(i => i.Name.Trim(), StringComparer.OrdinalIgnoreCase)
                    .Where(g => g.Count() > 1)
                    .Select(g => g.Key)
                    .ToList();
                if (duplicated.Count > 0)
                {
                    errors.Add($"存在重复 IO名称: {string.Join(", ", duplicated)}");
                }
            }

            return errors.Count > 0
                ? OperationResult<bool>.Failure(string.Join(Environment.NewLine, errors))
                : OperationResult<bool>.Succeed(true, "IO配置校验通过");
        }
    }
}
