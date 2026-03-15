using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace Astra.Converters
{
    /// <summary>
    /// 将 <see cref="SoftwareStatus"/> 转换为状态指示灯用的径向渐变画刷（使用主题色）。
    /// Running=绿, Connecting=青, Warning=橙, Error=红。
    /// </summary>
    public class SoftwareStatusToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not SoftwareStatus status)
                return GetFallbackBrush();

            var (mainKey, darkKey, transparentKey) = GetResourceKeys(status);
            try
            {
                var main = Application.Current?.FindResource(mainKey) as Color?;
                var dark = Application.Current?.FindResource(darkKey) as Color?;
                var transparent = Application.Current?.FindResource(transparentKey) as Color?;
                if (main == null || dark == null || transparent == null)
                    return GetFallbackBrush();

                return new RadialGradientBrush
                {
                    GradientStops =
                    {
                        new GradientStop(main.Value, 0),
                        new GradientStop(dark.Value, 0.6),
                        new GradientStop(transparent.Value, 1)
                    }
                };
            }
            catch
            {
                return GetFallbackBrush();
            }
        }

        private static (string Main, string Dark, string Transparent) GetResourceKeys(SoftwareStatus status)
        {
            return status switch
            {
                SoftwareStatus.Running => ("SuccessColor", "DarkSuccessColor", "SuccessColorTransparent"),
                SoftwareStatus.Connecting => ("InfoColor", "DarkInfoColor", "InfoColorTransparent"),
                SoftwareStatus.Warning => ("WarningColor", "DarkWarningColor", "WarningColorTransparent"),
                SoftwareStatus.Error => ("DangerColor", "DarkDangerColor", "DangerColorTransparent"),
                _ => ("SuccessColor", "DarkSuccessColor", "SuccessColorTransparent")
            };
        }

        private static Brush GetFallbackBrush()
        {
            try
            {
                var c = (Color?)Application.Current?.FindResource("SuccessColor");
                if (c != null)
                    return new SolidColorBrush(c.Value);
            }
            catch { }
            return new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
