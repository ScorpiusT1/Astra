using System;
using System.Globalization;
using System.Windows.Data;

namespace Astra.Plugins.DataAcquisition.Commons
{
    /// <summary>
    /// 布尔值反转转换器
    /// 用于 RadioButton 等控件的 IsChecked 绑定
    /// </summary>
    public class BooleanInverseConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return !boolValue;
            }
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return !boolValue;
            }
            return false;
        }
    }
}

