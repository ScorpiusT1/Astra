using Astra.Models;
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Astra.Converters
{
    /// <summary>
    /// 树节点右键菜单可见性转换器。
    /// ImportExport: 仅在“根节点且未挂载配置”时显示（分类节点）。
    /// Save: 在“节点挂载了配置”时显示（包含根节点直挂配置与子节点配置）。
    /// </summary>
    public class TreeNodeContextMenuVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var node = value as TreeNode;
            var mode = parameter?.ToString();

            if (node == null)
            {
                return Visibility.Collapsed;
            }

            return mode switch
            {
                "ImportExport" => (node.Parent == null && node.Config == null)
                    ? Visibility.Visible
                    : Visibility.Collapsed,
                "Save" => node.Config != null
                    ? Visibility.Visible
                    : Visibility.Collapsed,
                _ => Visibility.Collapsed
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
