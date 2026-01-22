using System;
using System.Globalization;
using System.Windows.Data;

namespace Astra.Converters
{
    /// <summary>
    /// 文本截断转换器
    /// 如果文本长度超过指定值，则截断并添加 "..."
    /// </summary>
    public class TextTruncateConverter : IValueConverter
    {
        /// <summary>
        /// 默认最大长度
        /// </summary>
        public int DefaultMaxLength { get; set; } = 30;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
                return string.Empty;

            string text = value.ToString();
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            // 从参数中获取最大长度，如果没有指定则使用默认值
            int maxLength = DefaultMaxLength;
            if (parameter != null)
            {
                if (parameter is int intParam)
                {
                    maxLength = intParam;
                }
                else if (int.TryParse(parameter.ToString(), out int parsedLength))
                {
                    maxLength = parsedLength;
                }
            }

            // 如果文本长度小于等于最大长度，直接返回
            if (text.Length <= maxLength)
                return text;

            // 截断文本并添加 "..."
            return text.Substring(0, maxLength) + "...";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // 不支持反向转换
            throw new NotImplementedException();
        }
    }
}

