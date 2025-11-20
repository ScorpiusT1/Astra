using Astra.Plugins.DataAcquisition.Configs;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;

namespace Astra.Plugins.DataAcquisition.ViewModels
{
    /// <summary>
    /// 传感器管理视图模型
    /// </summary>
    public partial class SensorManagementViewModel : ObservableObject
    {
        [ObservableProperty]
        private SensorConfig _selectedSensor;

        /// <summary>
        /// 传感器类型选项
        /// </summary>
        public Array SensorTypeOptions => Enum.GetValues(typeof(SensorType));

        /// <summary>
        /// 物理单位选项
        /// </summary>
        public Array PhysicalUnitOptions => Enum.GetValues(typeof(PhysicalUnit));

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

        public SensorManagementViewModel()
        {
        }

    }
}

