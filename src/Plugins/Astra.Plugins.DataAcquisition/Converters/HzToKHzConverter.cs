using System;
using System.Globalization;
using System.Windows.Data;

namespace Astra.Plugins.DataAcquisition.Converters
{
    /// <summary>
    /// Hz 到 kHz 的转换器（用于显示）
    /// </summary>
    public class HzToKHzConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double hz)
            {
                var khz = hz / 1000.0;
                // 根据数值大小决定小数位数
                if (khz >= 100)
                {
                    return $"{khz:F1} kHz";
                }
                else if (khz >= 10)
                {
                    return $"{khz:F2} kHz";
                }
                else
                {
                    return $"{khz:F3} kHz";
                }
            }
            
            return value?.ToString() ?? string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // 将 kHz 文本转换回 Hz 数值
            if (value is string str && !string.IsNullOrWhiteSpace(str))
            {
                // 移除 "kHz" 单位文本
                str = str.Trim().Replace("kHz", "").Replace("KHz", "").Replace("KHZ", "").Trim();
                
                // 尝试解析为 double
                if (double.TryParse(str, NumberStyles.Float, culture ?? CultureInfo.CurrentCulture, out var khz))
                {
                    // 转换为 Hz
                    return khz * 1000.0;
                }
            }
            
            // 如果无法解析，返回 0 或抛出异常
            return 0.0;
        }
    }
}

