using System;
using System.Globalization;
using System.Reflection;
using System.Windows.Data;

namespace Astra.UI.Converters
{
    /// <summary>
    /// 位置转换器 - 支持从 X/Y 属性或 Position.X/Position.Y 获取位置值
    /// </summary>
    public class PositionConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
                return 0.0;

            // parameter 指定要获取的坐标轴："X" 或 "Y"
            string axis = parameter?.ToString() ?? "X";

            try
            {
                // 首先尝试直接获取 X 或 Y 属性
                var directProp = value.GetType().GetProperty(axis);
                if (directProp != null)
                {
                    var directValue = directProp.GetValue(value);
                    if (directValue != null && double.TryParse(directValue.ToString(), out double result))
                    {
                        return result;
                    }
                }

                // 如果直接属性不存在，尝试从 Position 属性获取
                var positionProp = value.GetType().GetProperty("Position");
                if (positionProp != null)
                {
                    var position = positionProp.GetValue(value);
                    if (position != null)
                    {
                        var positionType = position.GetType();
                        var axisProp = positionType.GetProperty(axis);
                        if (axisProp != null)
                        {
                            var axisValue = axisProp.GetValue(position);
                            if (axisValue != null && double.TryParse(axisValue.ToString(), out double result))
                            {
                                return result;
                            }
                        }
                    }
                }
            }
            catch
            {
                // 如果发生任何错误，返回 0
            }

            return 0.0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

