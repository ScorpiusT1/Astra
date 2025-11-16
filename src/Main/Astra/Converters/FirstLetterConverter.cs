using System;
using System.Globalization;
using System.Windows.Data;

namespace Astra.Converters
{
    /// <summary>
    /// 提取字符串首字母的转换器
    /// </summary>
    public class FirstLetterConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string str && !string.IsNullOrEmpty(str))
            {
                return str.Substring(0, 1).ToUpper();
            }
            return "?";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

