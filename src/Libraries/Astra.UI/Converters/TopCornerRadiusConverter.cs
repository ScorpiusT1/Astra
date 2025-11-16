using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;

namespace Astra.UI.Converters
{
    /// <summary>
    /// 顶部圆角转换器
    /// </summary>
    public class TopCornerRadiusConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is CornerRadius cornerRadius)
            {
                return new CornerRadius(cornerRadius.TopLeft, cornerRadius.TopRight, 0, 0);
            }
            return new CornerRadius(0);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// 底部圆角转换器
    /// </summary>
    public class BottomCornerRadiusConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is CornerRadius cornerRadius)
            {
                return new CornerRadius(0, 0, cornerRadius.BottomRight, cornerRadius.BottomLeft);
            }
            return new CornerRadius(0);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
