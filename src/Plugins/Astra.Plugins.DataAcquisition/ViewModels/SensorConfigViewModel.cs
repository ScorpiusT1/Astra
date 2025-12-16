using Astra.Core.Configuration;
using Astra.Plugins.DataAcquisition.Configs;
using Astra.Plugins.DataAcquisition.Commons;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Astra.Plugins.DataAcquisition.ViewModels
{
    /// <summary>
    /// 传感器管理视图模型
    /// </summary>
    public partial class SensorConfigViewModel : ObservableObject
    {
        [ObservableProperty]
        private SensorConfig _selectedSensor;

        private static readonly PhysicalUnitToStringConverter _unitConverter = new PhysicalUnitToStringConverter();

        /// <summary>
        /// 传感器类型选项
        /// </summary>
        public Array SensorTypeOptions => Enum.GetValues(typeof(SensorType));

        /// <summary>
        /// 物理单位选项（显示简写）
        /// </summary>
        public List<PhysicalUnitDisplayItem> PhysicalUnitOptions
        {
            get
            {
                var units = Enum.GetValues(typeof(PhysicalUnit)).Cast<PhysicalUnit>();
                return units.Select(u => new PhysicalUnitDisplayItem
                {
                    EnumValue = u,
                    DisplayText = _unitConverter.Convert(u.ToString(), typeof(string), null, System.Globalization.CultureInfo.CurrentCulture)?.ToString() ?? u.ToString(),
                    FullName = u.ToString()
                }).ToList();
            }
        }

        /// <summary>
        /// 灵敏度单位选项
        /// </summary>
        public List<string> SensitivityUnitOptions { get; } = new List<string>
        {
            "mV/V",
            "V/V",
            "pC/g",
            "mV/g",
            "pC/Pa",
            "mV/Pa",
            "pC/N",
            "mV/N",
            "V/g",
            "V/Pa",
            "V/N"
        };

        public SensorConfigViewModel(IConfig config)
        {
            _selectedSensor = config as SensorConfig;
        }

    }
}

