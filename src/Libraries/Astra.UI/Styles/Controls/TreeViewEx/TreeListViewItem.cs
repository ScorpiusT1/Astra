using System.Windows;
using System.Windows.Controls;

namespace Astra.UI.Styles.Controls.TreeViewEx
{
    /// <summary>
    /// <see cref="TreeListView"/> 的行容器，提供层级深度供首列缩进绑定。
    /// </summary>
    public class TreeListViewItem : TreeViewItem
    {
        static TreeListViewItem()
        {
            DefaultStyleKeyProperty.OverrideMetadata(
                typeof(TreeListViewItem),
                new FrameworkPropertyMetadata(typeof(TreeListViewItem)));
        }

        /// <summary>
        /// 节点在树中的层级（根为 0）。用于与 <see cref="LevelToIndentConverter"/> / <see cref="LevelToIndentMultiConverter"/> 配合。
        /// </summary>
        public int Level
        {
            get
            {
                if (_level == -1)
                {
                    var parent = ItemsControl.ItemsControlFromItemContainer(this) as TreeListViewItem;
                    _level = parent != null ? parent.Level + 1 : 0;
                }

                return _level;
            }
        }

        protected override DependencyObject GetContainerForItemOverride()
        {
            return new TreeListViewItem();
        }

        protected override bool IsItemItsOwnContainerOverride(object item)
        {
            return item is TreeListViewItem;
        }

        private int _level = -1;
    }
}
