using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Astra.UI.Converters
{
    /// <summary>
    /// 整数到可见性的转换器
    /// 当整数值等于参数指定值时返回 Visible，否则返回 Collapsed
    /// </summary>
    public class IntToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int intValue && parameter is string paramStr)
            {
                if (int.TryParse(paramStr, out int targetValue))
                {
                    return intValue == targetValue ? Visibility.Visible : Visibility.Collapsed;
                }
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

