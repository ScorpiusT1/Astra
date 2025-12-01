using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Astra.UI.Converters
{
    /// <summary>
    /// 反转的布尔值到可见性转换器
    /// 当值为true时返回Collapsed，为false时返回Visible
    /// 支持通过ConverterParameter="Inverted"来反转结果
    /// </summary>
    public class InvertedBooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                // 如果参数是"Inverted"，则反转逻辑
                bool invert = parameter?.ToString() == "Inverted";
                
                if (invert)
                {
                    // 反转模式：true -> Visible, false -> Collapsed
                    return boolValue ? Visibility.Visible : Visibility.Collapsed;
                }
                else
                {
                    // 正常模式：true -> Collapsed, false -> Visible
                    return boolValue ? Visibility.Collapsed : Visibility.Visible;
                }
            }
            
            return Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Visibility visibility)
            {
                bool invert = parameter?.ToString() == "Inverted";
                
                if (invert)
                {
                    return visibility == Visibility.Visible;
                }
                else
                {
                    return visibility == Visibility.Collapsed;
                }
            }
            
            return false;
        }
    }
}

