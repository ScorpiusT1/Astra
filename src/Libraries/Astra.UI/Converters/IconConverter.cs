using System;
using System.Globalization;
using System.Windows.Data;
using FontAwesome.Sharp;
using Astra.UI;

namespace Astra.UI.Converters
{
    /// <summary>
    /// 图标代码转换器 - 将字符串图标代码转换为 FontAwesome 图标枚举
    /// </summary>
    public class IconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string iconCode && !string.IsNullOrEmpty(iconCode))
                return FontAwesomeIconResolver.Resolve(iconCode);

            return IconChar.Circle;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is IconChar icon)
                return icon.ToString();
            if (value is Enum iconEnum)
                return iconEnum.ToString();
            return "Circle";
        }
    }
}

