using System;
using System.Globalization;
using System.Windows.Data;

namespace Astra.Plugins.DataAcquisition.Commons
{
    /// <summary>
    /// 将 Hz 值转换为 KHz 显示的转换器
    /// </summary>
    public class HzToKHzConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double doubleValue)
            {
                // 将 Hz 转换为 KHz（除以 1000）
                double khzValue = doubleValue / 1000.0;
                // 格式化显示，保留3位小数
                return $"{khzValue:F3} KHz";
            }
            
            if (value is int intValue)
            {
                double khzValue = intValue / 1000.0;
                return $"{khzValue:F3} KHz";
            }
            
            return value?.ToString() ?? string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // 反向转换：从显示的 KHz 字符串转换回 Hz 数值
            if (value is string strValue)
            {
                // 移除 "KHz" 后缀和空格
                strValue = strValue?.Replace("KHz", "").Replace("kHz", "").Replace("khz", "").Trim();
                
                if (double.TryParse(strValue, NumberStyles.Float, culture, out double khzValue))
                {
                    // 将 KHz 转换回 Hz（乘以 1000）
                    return khzValue * 1000.0;
                }
            }
            
            // 如果无法解析，尝试直接解析为数字（可能是 Hz 值）
            if (value is string numStr && double.TryParse(numStr, NumberStyles.Float, culture, out double hzValue))
            {
                return hzValue;
            }
            
            return 0.0;
        }
    }
}

