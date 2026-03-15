using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace Astra.Converters
{
    /// <summary>
    /// 将 <see cref="SoftwareStatus"/> 转换为主题主色（用于状态灯光晕等）。
    /// Running=绿, Connecting=青, Warning=橙, Error=红。
    /// </summary>
    public class SoftwareStatusToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not SoftwareStatus status)
                return GetFallbackColor();

            string key = status switch
            {
                SoftwareStatus.Running => "SuccessColor",
                SoftwareStatus.Connecting => "InfoColor",
                SoftwareStatus.Warning => "WarningColor",
                SoftwareStatus.Error => "DangerColor",
                _ => "SuccessColor"
            };

            try
            {
                var color = Application.Current?.FindResource(key) as Color?;
                return color ?? GetFallbackColor();
            }
            catch
            {
                return GetFallbackColor();
            }
        }

        private static Color GetFallbackColor()
            => Color.FromRgb(0x4C, 0xAF, 0x50);

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
