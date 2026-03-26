using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Astra.UI.Styles.Controls.TreeViewEx
{
    /// <summary>
    /// 将层级转换为左侧缩进边距。单路绑定（仅 Level）时使用固定步长 <see cref="DefaultIndentSize"/>，或通过 ConverterParameter 传入步长。
    /// </summary>
    public class LevelToIndentConverter : IValueConverter
    {
        public const double DefaultIndentSize = 19d;

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            var indent = DefaultIndentSize;
            if (parameter != null && double.TryParse(parameter.ToString(), NumberStyles.Any, culture, out var p))
                indent = p;

            var level = 0;
            if (value is int i)
                level = i;

            return new Thickness(level * indent, 0, 0, 0);
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    /// <summary>
    /// 多路绑定：Level（TreeListViewItem） + IndentSize（TreeListView），用于缩进随控件属性动态变化。
    /// </summary>
    public class LevelToIndentMultiConverter : IMultiValueConverter
    {
        public object Convert(object?[]? values, Type targetType, object? parameter, CultureInfo culture)
        {
            if (values == null || values.Length < 2)
                return new Thickness(0);

            var level = values[0] is int l ? l : 0;
            var indent = values[1] is double d ? d : LevelToIndentConverter.DefaultIndentSize;
            if (indent <= 0)
                indent = LevelToIndentConverter.DefaultIndentSize;

            return new Thickness(level * indent, 0, 0, 0);
        }

        public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
