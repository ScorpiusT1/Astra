using System;
using System.Globalization;
using System.Windows.Data;
using Astra.Plugins.DataAcquisition.Configs;

namespace Astra.Plugins.DataAcquisition.Commons
{
    /// <summary>
    /// 物理单位到简写显示的转换器
    /// </summary>
    public class PhysicalUnitToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string unitStr)
            {
                // 尝试解析为枚举
                if (Enum.TryParse<PhysicalUnit>(unitStr, out var unit))
                {
                    return GetAbbreviation(unit);
                }
                // 如果不是枚举值，直接返回原字符串
                return unitStr;
            }
            
            if (value is PhysicalUnit unitEnum)
            {
                return GetAbbreviation(unitEnum);
            }
            
            return value?.ToString() ?? string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string str)
            {
                // 尝试从简写反推枚举值
                var enumValue = GetEnumFromAbbreviation(str);
                if (enumValue.HasValue)
                {
                    return enumValue.Value.ToString();
                }
                // 如果无法从简写反推，尝试直接解析
                if (Enum.TryParse<PhysicalUnit>(str, out var unit))
                {
                    return unit.ToString();
                }
            }
            
            return value?.ToString() ?? string.Empty;
        }

        /// <summary>
        /// 获取物理单位的简写
        /// </summary>
        private string GetAbbreviation(PhysicalUnit unit)
        {
            return unit switch
            {
                PhysicalUnit.Volt => "V",
                PhysicalUnit.MilliVolt => "mV",
                PhysicalUnit.Ampere => "A",
                PhysicalUnit.MilliAmpere => "mA",
                PhysicalUnit.MeterPerSecond2 => "m/s²",
                PhysicalUnit.G => "g",
                PhysicalUnit.Pascal => "Pa",
                PhysicalUnit.Newton => "N",
                PhysicalUnit.Meter => "m",
                PhysicalUnit.MilliMeter => "mm",
                PhysicalUnit.MeterPerSecond => "m/s",
                PhysicalUnit.RPM => "rpm",
                PhysicalUnit.Decibel => "dB",
                PhysicalUnit.Celsius => "°C",
                PhysicalUnit.MicroStrain => "με",
                _ => unit.ToString()
            };
        }

        /// <summary>
        /// 从简写获取枚举值
        /// </summary>
        private PhysicalUnit? GetEnumFromAbbreviation(string abbreviation)
        {
            return abbreviation?.ToLower() switch
            {
                "v" => PhysicalUnit.Volt,
                "mv" => PhysicalUnit.MilliVolt,
                "a" => PhysicalUnit.Ampere,
                "ma" => PhysicalUnit.MilliAmpere,
                "m/s²" or "m/s2" => PhysicalUnit.MeterPerSecond2,
                "g" => PhysicalUnit.G,
                "pa" => PhysicalUnit.Pascal,
                "n" => PhysicalUnit.Newton,
                "m" => PhysicalUnit.Meter,
                "mm" => PhysicalUnit.MilliMeter,
                "m/s" => PhysicalUnit.MeterPerSecond,
                "rpm" => PhysicalUnit.RPM,
                "db" => PhysicalUnit.Decibel,
                "°c" or "c" => PhysicalUnit.Celsius,
                "με" or "μe" => PhysicalUnit.MicroStrain,
                _ => null
            };
        }
    }
}

