using System;
using System.Globalization;
using System.Windows.Data;

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
            {
                // 尝试将字符串转换为 FontAwesome.Sharp 的图标枚举
                // 如果转换失败，返回默认图标
                try
                {
                    // 使用反射尝试获取图标枚举值
                    var iconType = typeof(FontAwesome.Sharp.IconChar);
                    if (Enum.TryParse(iconType, iconCode, true, out var iconEnum))
                    {
                        return iconEnum;
                    }
                }
                catch
                {
                    // 如果转换失败，返回默认图标
                }
            }
            
            // 返回默认图标（Circle）
            return FontAwesome.Sharp.IconChar.Circle;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Enum iconEnum)
            {
                return iconEnum.ToString();
            }
            return "Circle";
        }
    }
}

