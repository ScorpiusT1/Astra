using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Astra.UI.Converters
{
    /// <summary>
    /// 将 Brush 转换为 Color 的转换器
    /// 支持 SolidColorBrush、LinearGradientBrush、RadialGradientBrush
    /// </summary>
    public class BrushToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
            {
                // 返回默认颜色（灰色）
                return Colors.Gray;
            }

            switch (value)
            {
                case SolidColorBrush solidBrush:
                    return solidBrush.Color;

                case LinearGradientBrush linearBrush:
                    // 返回第一个 GradientStop 的颜色
                    if (linearBrush.GradientStops != null && linearBrush.GradientStops.Count > 0)
                    {
                        return linearBrush.GradientStops[0].Color;
                    }
                    return Colors.Gray;

                case RadialGradientBrush radialBrush:
                    // 返回第一个 GradientStop 的颜色
                    if (radialBrush.GradientStops != null && radialBrush.GradientStops.Count > 0)
                    {
                        return radialBrush.GradientStops[0].Color;
                    }
                    return Colors.Gray;

                default:
                    return Colors.Gray;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

