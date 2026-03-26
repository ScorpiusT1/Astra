using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace Astra.Converters
{
    public class IoValueDisplayConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var str = value?.ToString() ?? "";
            if (str.Equals("True", StringComparison.OrdinalIgnoreCase)) return "ON";
            if (str.Equals("False", StringComparison.OrdinalIgnoreCase)) return "OFF";
            return str; // 模拟量直接原样显示
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
