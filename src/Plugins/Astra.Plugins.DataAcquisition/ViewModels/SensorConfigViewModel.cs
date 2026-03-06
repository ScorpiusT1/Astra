using Astra.Core.Configuration;
using Astra.Plugins.DataAcquisition.Configs;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;

namespace Astra.Plugins.DataAcquisition.ViewModels
{
    /// <summary>
    /// 传感器管理视图模型
    /// </summary>
    public partial class SensorConfigViewModel : ObservableObject
    {
        [ObservableProperty]
        private SensorConfig _selectedSensor;

        /// <summary>
        /// 传感器类型选项
        /// </summary>
        public Array SensorTypeOptions => Enum.GetValues(typeof(SensorType));

        /// <summary>
        /// 传感器转换方式选项
        /// </summary>
        public Array ConversionModeOptions => Enum.GetValues(typeof(SensorConversionMode));

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

