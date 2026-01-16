using System;
using System.Collections;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Astra.UI.Converters
{
    /// <summary>
    /// 分组项数量到可见性转换器
    /// 当分组中的项目数量为0时，隐藏分组
    /// 用于 PropertyEditor 中，当分组的所有属性都被隐藏时，自动隐藏该分组
    /// </summary>
    public class GroupItemCountToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // 处理整数类型的 ItemCount
            if (value is int count)
            {
                return count > 0 ? Visibility.Visible : Visibility.Collapsed;
            }

            // 处理 CollectionViewGroup 类型
            if (value is CollectionViewGroup group)
            {
                // ItemCount 已经反映了过滤后的项目数量
                return group.ItemCount > 0 ? Visibility.Visible : Visibility.Collapsed;
            }

            // 处理 ReadOnlyObservableCollection 类型（分组的 Items 集合）
            if (value is System.Collections.ICollection collection)
            {
                return collection.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            }

            // 默认可见
            return Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

