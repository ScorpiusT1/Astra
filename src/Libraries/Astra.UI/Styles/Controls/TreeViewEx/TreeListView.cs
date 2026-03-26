using System;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Astra.UI.Styles.Controls.TreeViewEx
{
    /// <summary>
    /// 树形多列列表：在 <see cref="TreeView"/> 上增加 <see cref="GridView"/> 式列头与行内多列布局。
    /// 行数据绑定与 <see cref="TreeView"/> 相同；列定义请使用 <see cref="Columns"/>，并在 <see cref="GridViewColumn.DisplayMemberBinding"/> 或 <see cref="GridViewColumn.CellTemplate"/> 中绑定业务属性。
    /// </summary>
    public class TreeListView : TreeView
    {
        static TreeListView()
        {
            DefaultStyleKeyProperty.OverrideMetadata(
                typeof(TreeListView),
                new FrameworkPropertyMetadata(typeof(TreeListView)));
        }

        public TreeListView()
        {
            SetCurrentValue(ColumnsProperty, new GridViewColumnCollection());
            Loaded += OnTreeListViewLoaded;
            SizeChanged += OnTreeListViewSizeChanged;
        }

        private void OnTreeListViewLoaded(object sender, RoutedEventArgs e)
        {
            HookColumnsCollection(Columns);
            ScheduleStretchLastColumn();
        }

        private void OnTreeListViewSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (e.WidthChanged)
                ScheduleStretchLastColumn();
        }

        /// <summary>
        /// 为 true 时，在布局后将最后一列宽度调整为「控件可用宽度 − 前面各列宽度」，
        /// 使表格行在水平方向铺满容器（前面各列需为固定数值宽度，不能为 Auto）。
        /// </summary>
        public bool StretchLastColumn
        {
            get => (bool)GetValue(StretchLastColumnProperty);
            set => SetValue(StretchLastColumnProperty, value);
        }

        public static readonly DependencyProperty StretchLastColumnProperty =
            DependencyProperty.Register(
                nameof(StretchLastColumn),
                typeof(bool),
                typeof(TreeListView),
                new FrameworkPropertyMetadata(true, (d, _) => ((TreeListView)d).ScheduleStretchLastColumn()));

        /// <summary>最后一列拉伸时的最小宽度（像素）。</summary>
        public double LastColumnMinWidth
        {
            get => (double)GetValue(LastColumnMinWidthProperty);
            set => SetValue(LastColumnMinWidthProperty, value);
        }

        public static readonly DependencyProperty LastColumnMinWidthProperty =
            DependencyProperty.Register(
                nameof(LastColumnMinWidth),
                typeof(double),
                typeof(TreeListView),
                new FrameworkPropertyMetadata(60d, (d, _) => ((TreeListView)d).ScheduleStretchLastColumn()));

        private GridViewColumnCollection? _hookedColumns;

        private void HookColumnsCollection(GridViewColumnCollection? cols)
        {
            if (_hookedColumns != null)
            {
                _hookedColumns.CollectionChanged -= OnColumnsCollectionChanged;
                _hookedColumns = null;
            }

            if (cols != null)
            {
                _hookedColumns = cols;
                cols.CollectionChanged += OnColumnsCollectionChanged;
            }
        }

        private void OnColumnsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            ScheduleStretchLastColumn();
        }

        private bool _stretchScheduled;

        private void ScheduleStretchLastColumn()
        {
            if (!StretchLastColumn)
                return;
            if (_stretchScheduled)
                return;
            _stretchScheduled = true;
            Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, StretchLastColumnToFill);
        }

        private void StretchLastColumnToFill()
        {
            _stretchScheduled = false;
            if (!StretchLastColumn)
                return;

            var columns = Columns;
            if (columns == null || columns.Count < 2)
                return;

            double available = ActualWidth;
            if (available <= 2 || double.IsNaN(available))
                return;

            available -= Padding.Left + Padding.Right;
            available -= BorderThickness.Left + BorderThickness.Right;

            double sumFixed = 0;
            for (var i = 0; i < columns.Count - 1; i++)
            {
                var w = columns[i].Width;
                if (double.IsNaN(w) || w <= 0)
                    return;
                sumFixed += w;
            }

            var last = columns[^1];
            var target = Math.Max(LastColumnMinWidth, available - sumFixed);
            if (Math.Abs(last.Width - target) > 0.5)
                last.Width = target;
        }

        protected override DependencyObject GetContainerForItemOverride()
        {
            return new TreeListViewItem();
        }

        protected override bool IsItemItsOwnContainerOverride(object item)
        {
            return item is TreeListViewItem;
        }

        /// <summary>首列单元格模板（树线 + 折叠按钮右侧区域）。为 null 时由行数据默认呈现（等价于简单 <see cref="ContentPresenter"/>）。</summary>
        public DataTemplate? FirstColumnCellTemplate
        {
            get => (DataTemplate?)GetValue(FirstColumnCellTemplateProperty);
            set => SetValue(FirstColumnCellTemplateProperty, value);
        }

        public static readonly DependencyProperty FirstColumnCellTemplateProperty =
            DependencyProperty.Register(
                nameof(FirstColumnCellTemplate),
                typeof(DataTemplate),
                typeof(TreeListView),
                new FrameworkPropertyMetadata(null));

        /// <summary>层级缩进单位宽度（像素），与 <see cref="LevelToIndentMultiConverter"/> 配合使用。</summary>
        public double IndentSize
        {
            get => (double)GetValue(IndentSizeProperty);
            set => SetValue(IndentSizeProperty, value);
        }

        public static readonly DependencyProperty IndentSizeProperty =
            DependencyProperty.Register(
                nameof(IndentSize),
                typeof(double),
                typeof(TreeListView),
                new FrameworkPropertyMetadata(19d));

        public Style? ColumnHeaderStyle
        {
            get => (Style?)GetValue(ColumnHeaderStyleProperty);
            set => SetValue(ColumnHeaderStyleProperty, value);
        }

        public static readonly DependencyProperty ColumnHeaderStyleProperty =
            DependencyProperty.Register(
                nameof(ColumnHeaderStyle),
                typeof(Style),
                typeof(TreeListView),
                new FrameworkPropertyMetadata(null));

        public GridViewColumnCollection Columns
        {
            get => (GridViewColumnCollection)GetValue(ColumnsProperty);
            set => SetValue(ColumnsProperty, value);
        }

        public static readonly DependencyProperty ColumnsProperty =
            DependencyProperty.Register(
                nameof(Columns),
                typeof(GridViewColumnCollection),
                typeof(TreeListView),
                new FrameworkPropertyMetadata(null, OnColumnsPropertyChanged));

        private static void OnColumnsPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var tree = (TreeListView)d;
            tree.HookColumnsCollection(tree.Columns);
            tree.ScheduleStretchLastColumn();
        }
    }
}
