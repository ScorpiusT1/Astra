using System;
using System.Globalization;
using System.Windows.Data;

namespace Astra.Converters
{
    /// <summary>
    /// 字符串到布尔值转换器
    /// 用于导航按钮的IsChecked绑定
    /// </summary>
    public class StringToBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string currentPageKey && parameter is string targetPageKey)
            {
                return string.Equals(currentPageKey, targetPageKey, StringComparison.OrdinalIgnoreCase);
            }
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // 对于RadioButton的IsChecked绑定，我们不需要ConvertBack
            // 因为导航是通过Command处理的，不是通过IsChecked属性
            if (value is bool isChecked && isChecked && parameter is string pageKey)
            {
                return pageKey;
            }
            return null;
        }
    }
}
