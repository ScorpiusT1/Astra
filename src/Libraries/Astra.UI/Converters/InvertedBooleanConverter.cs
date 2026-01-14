using System;
using System.Globalization;
using System.Windows.Data;

namespace Astra.UI.Converters
{
    /// <summary>
    /// 反转的布尔值转换器
    /// 当值为true时返回false，为false时返回true
    /// </summary>
    public class InvertedBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return !boolValue;
            }
            
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return !boolValue;
            }
            
            return false;
        }
    }
}

