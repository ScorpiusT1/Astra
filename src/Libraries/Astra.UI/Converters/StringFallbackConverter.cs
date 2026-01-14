using System;
using System.Globalization;
using System.Windows.Data;

namespace Astra.UI.Converters
{
    /// <summary>
    /// 字符串回退转换器
    /// 返回第一个非空字符串值
    /// </summary>
    public class StringFallbackConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values == null || values.Length == 0)
                return "未命名流程";

            foreach (var value in values)
            {
                if (value is string str && !string.IsNullOrWhiteSpace(str))
                {
                    return str;
                }
            }

            return "未命名流程";
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}











