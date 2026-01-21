using System;
using System.Globalization;
using System.Windows.Data;

namespace Astra.Plugins.DataAcquisition.Converters
{
    /// <summary>
    /// 检查值是否为非空的转换器（用于启用/禁用控件）
    /// </summary>
    public class IsNotNullConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string str)
            {
                return !string.IsNullOrWhiteSpace(str);
            }
            return value != null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

