using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Reflection;
using System.Windows.Data;

namespace Astra.UI.Converters
{
    /// <summary>
    /// 枚举值到显示文本的转换器
    /// 支持 DescriptionAttribute 和 DisplayAttribute
    /// </summary>
    public class EnumToDisplayTextConverter : IValueConverter
    {
        public static readonly EnumToDisplayTextConverter Instance = new EnumToDisplayTextConverter();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
                return string.Empty;

            var enumType = value.GetType();
            
            // 如果不是枚举类型，直接返回字符串
            if (!enumType.IsEnum)
                return value.ToString();

            var enumValue = value as Enum;
            if (enumValue == null)
                return string.Empty;

            // 获取枚举值的字段信息
            var fieldInfo = enumType.GetField(enumValue.ToString());
            if (fieldInfo == null)
                return enumValue.ToString();

            // 优先检查 DisplayAttribute
            var displayAttr = fieldInfo.GetCustomAttribute<DisplayAttribute>();
            if (displayAttr != null)
            {
                var name = displayAttr.GetName();
                if (!string.IsNullOrEmpty(name))
                    return name;
            }

            // 其次检查 DescriptionAttribute
            var descAttr = fieldInfo.GetCustomAttribute<DescriptionAttribute>();
            if (descAttr != null && !string.IsNullOrEmpty(descAttr.Description))
            {
                return descAttr.Description;
            }

            // 如果都没有，返回枚举名称本身
            return enumValue.ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || !targetType.IsEnum)
                return null;

            var stringValue = value.ToString();
            if (string.IsNullOrEmpty(stringValue))
                return null;

            // 首先尝试直接匹配枚举名称
            if (Enum.TryParse(targetType, stringValue, true, out var enumValue))
            {
                return enumValue;
            }

            // 如果直接匹配失败，尝试通过描述或显示名称匹配
            var enumFields = targetType.GetFields(BindingFlags.Public | BindingFlags.Static);
            
            foreach (var field in enumFields)
            {
                // 检查 DisplayAttribute
                var displayAttr = field.GetCustomAttribute<DisplayAttribute>();
                if (displayAttr != null)
                {
                    var name = displayAttr.GetName();
                    if (string.Equals(name, stringValue, StringComparison.OrdinalIgnoreCase))
                    {
                        return field.GetValue(null);
                    }
                }

                // 检查 DescriptionAttribute
                var descAttr = field.GetCustomAttribute<DescriptionAttribute>();
                if (descAttr != null && string.Equals(descAttr.Description, stringValue, StringComparison.OrdinalIgnoreCase))
                {
                    return field.GetValue(null);
                }
            }

            // 如果都匹配不到，返回默认值或 null
            return null;
        }
    }
}

