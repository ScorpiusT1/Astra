using System;
using System.Globalization;
using System.Windows.Data;

namespace Astra.Converters
{
    /// <summary>UTC <see cref="DateTime"/> 转本地显示字符串。</summary>
    public sealed class UtcToLocalDateTimeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is DateTime dt)
                return dt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", culture);
            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
