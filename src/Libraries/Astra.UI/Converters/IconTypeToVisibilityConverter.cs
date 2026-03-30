using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using Astra.UI;

namespace Astra.UI.Converters
{
    /// <summary>
    /// 根据图标字符串判断是否为 FontAwesome 图标，并返回可见性。
    /// - 当字符串是有效的 FontAwesome.Sharp.IconChar 名称时返回 Visible；
    /// - 否则返回 Collapsed。
    /// 通过 ConverterParameter="invert" 可以反转结果，用于 Emoji/字符图标显示。
    /// </summary>
    public class IconTypeToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isFontAwesome = false;

            if (value is string iconCode && !string.IsNullOrWhiteSpace(iconCode))
                isFontAwesome = FontAwesomeIconResolver.IsKnownIconName(iconCode);

            bool invert = parameter is string param &&
                          param.Equals("invert", StringComparison.OrdinalIgnoreCase);

            bool result = invert ? !isFontAwesome : isFontAwesome;
            return result ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }
}

