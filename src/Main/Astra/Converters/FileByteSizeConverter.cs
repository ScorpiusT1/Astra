using System;
using System.Globalization;
using System.Windows.Data;

namespace Astra.Converters
{
    /// <summary>将字节数格式化为可读大小。</summary>
    public sealed class FileByteSizeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is long l)
                return Format(l);
            if (value is int i)
                return Format(i);
            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();

        private static string Format(long bytes)
        {
            if (bytes < 1024) return bytes.ToString(CultureInfo.InvariantCulture) + " B";
            double v = bytes;
            var units = new[] { "KB", "MB", "GB", "TB" };
            var u = 0;
            v /= 1024.0;
            while (v >= 1024.0 && u < units.Length - 1)
            {
                v /= 1024.0;
                u++;
            }

            return v.ToString("0.##", CultureInfo.InvariantCulture) + " " + units[u];
        }
    }
}
