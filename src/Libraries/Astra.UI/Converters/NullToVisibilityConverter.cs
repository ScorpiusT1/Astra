using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Astra.UI.Converters
{
    /// <summary>
    /// 将对象转换为可见性（null = Collapsed, not null = Visible）
    /// </summary>
    public class NullToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // 如果 parameter 是 "Inverse"，则反转逻辑
            bool inverse = parameter?.ToString() == "Inverse";

            if (inverse)
            {
                // null -> Visible, not null -> Collapsed
                return value == null ? Visibility.Visible : Visibility.Collapsed;
            }
            else
            {
                // null -> Collapsed, not null -> Visible
                return value != null ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

