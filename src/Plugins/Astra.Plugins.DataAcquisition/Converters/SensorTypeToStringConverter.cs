using System;
using System.Globalization;
using System.Windows.Data;
using Astra.Plugins.DataAcquisition.Configs;

namespace Astra.Plugins.DataAcquisition.Converters
{
    /// <summary>
    /// 传感器类型到中文显示的转换器
    /// </summary>
    public class SensorTypeToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is SensorType sensorType)
            {
                return sensorType switch
                {
                    SensorType.None => "无",
                    SensorType.Accelerometer => "加速度计",
                    SensorType.Microphone => "麦克风",
                    SensorType.Force => "力传感器",
                    SensorType.Pressure => "压力传感器",
                    SensorType.Displacement => "位移传感器",
                    SensorType.Velocity => "速度传感器",
                    SensorType.Tachometer => "转速传感器",
                    SensorType.StrainGauge => "应变片",
                    SensorType.Voltage => "电压信号",
                    SensorType.Current => "电流信号",
                    SensorType.Temperature => "温度传感器",
                    _ => sensorType.ToString()
                };
            }
            return value?.ToString() ?? string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string str)
            {
                return str switch
                {
                    "无" => SensorType.None,
                    "加速度计" => SensorType.Accelerometer,
                    "麦克风" => SensorType.Microphone,
                    "力传感器" => SensorType.Force,
                    "压力传感器" => SensorType.Pressure,
                    "位移传感器" => SensorType.Displacement,
                    "速度传感器" => SensorType.Velocity,
                    "转速传感器" => SensorType.Tachometer,
                    "应变片" => SensorType.StrainGauge,
                    "电压信号" => SensorType.Voltage,
                    "电流信号" => SensorType.Current,
                    "温度传感器" => SensorType.Temperature,
                    _ => Enum.TryParse<SensorType>(str, out var result) ? result : SensorType.None
                };
            }
            
            if (value is SensorType sensorType)
            {
                return sensorType;
            }
            
            return SensorType.None;
        }
    }
}

