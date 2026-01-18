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
    /// 比例转GridLength转换器
    /// </summary>
    public class RatioToGridLengthConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double ratio)
            {
                return new GridLength(ratio, GridUnitType.Star);
            }
            return new GridLength(0.4, GridUnitType.Star);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
