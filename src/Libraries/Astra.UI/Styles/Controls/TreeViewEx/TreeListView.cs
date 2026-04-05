using System;
using System.Collections.Specialized;
using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
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
            PreviewMouseLeftButtonDown += OnPreviewMouseLeftButtonDown;
            PreviewMouseMove += OnPreviewMouseMove;
            DragOver += OnDragOver;
            Drop += OnDrop;
        }

        private void OnTreeListViewLoaded(object sender, RoutedEventArgs e)
        {
            HookColumnsCollection(Columns);
            ScheduleStretchLastColumn();
            // 首帧布局后模板、纵向滚动条与 ViewportWidth 才稳定，再补一次
            Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, () => ScheduleStretchLastColumn());
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

        /// <summary>
        /// 为 true 时，在布局后按各列当前宽度比例分配控件可用宽度，使所有列共同铺满水平空间。
        /// 启用时优先于 <see cref="StretchLastColumn"/>。
        /// </summary>
        public bool StretchAllColumns
        {
            get => (bool)GetValue(StretchAllColumnsProperty);
            set => SetValue(StretchAllColumnsProperty, value);
        }

        public static readonly DependencyProperty StretchAllColumnsProperty =
            DependencyProperty.Register(
                nameof(StretchAllColumns),
                typeof(bool),
                typeof(TreeListView),
                new FrameworkPropertyMetadata(false, (d, _) => ((TreeListView)d).ScheduleStretchLastColumn()));

        /// <summary>
        /// 指定某一列（0-based）吸收「控件可用宽度 − 其余各列宽度」的剩余空间。
        /// 为 -1 且 <see cref="StretchLastColumn"/> 为 true 时，行为与原先「只拉伸最后一列」一致。
        /// 与 <see cref="StretchAllColumns"/> 互斥：全列比例拉伸时忽略本属性。
        /// </summary>
        public int StretchFillColumnIndex
        {
            get => (int)GetValue(StretchFillColumnIndexProperty);
            set => SetValue(StretchFillColumnIndexProperty, value);
        }

        public static readonly DependencyProperty StretchFillColumnIndexProperty =
            DependencyProperty.Register(
                nameof(StretchFillColumnIndex),
                typeof(int),
                typeof(TreeListView),
                new FrameworkPropertyMetadata(-1, (d, _) => ((TreeListView)d).ScheduleStretchLastColumn()));

        /// <summary>
        /// 是否允许列头拖拽换列。默认关闭，仅保留列宽拉伸能力。
        /// </summary>
        public bool AllowsColumnReorder
        {
            get => (bool)GetValue(AllowsColumnReorderProperty);
            set => SetValue(AllowsColumnReorderProperty, value);
        }

        public static readonly DependencyProperty AllowsColumnReorderProperty =
            DependencyProperty.Register(
                nameof(AllowsColumnReorder),
                typeof(bool),
                typeof(TreeListView),
                new FrameworkPropertyMetadata(false));

        /// <summary>
        /// 是否启用行拖拽换位（同一父节点下重排）。默认关闭。
        /// </summary>
        public bool EnableRowDragReorder
        {
            get => (bool)GetValue(EnableRowDragReorderProperty);
            set => SetValue(EnableRowDragReorderProperty, value);
        }

        public static readonly DependencyProperty EnableRowDragReorderProperty =
            DependencyProperty.Register(
                nameof(EnableRowDragReorder),
                typeof(bool),
                typeof(TreeListView),
                new FrameworkPropertyMetadata(false));

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
        private bool _stretchRepeatRequested;
        private ScrollViewer? _partOuterScroll;
        private ScrollViewer? _partItemsScroll;
        private Point _dragStartPoint;
        private TreeListViewItem? _dragSourceContainer;

        /// <summary>列线、Thumb 与 DPI 舍入可能使渲染宽度略大于列宽之和，预留少量 DIP 避免外层横向滚动条。</summary>
        private const double ColumnLayoutFudgeDip = 16.0;

        public override void OnApplyTemplate()
        {
            if (_partItemsScroll != null)
            {
                _partItemsScroll.SizeChanged -= OnPartItemsScrollSizeChanged;
                _partItemsScroll.ScrollChanged -= OnPartItemsScrollScrollChanged;
            }

            if (_partOuterScroll != null)
                _partOuterScroll.SizeChanged -= OnPartOuterScrollSizeChanged;

            base.OnApplyTemplate();

            _partOuterScroll = GetTemplateChild("PART_OuterScroll") as ScrollViewer;
            _partItemsScroll = GetTemplateChild("PART_ItemsScroll") as ScrollViewer;

            if (_partItemsScroll != null)
            {
                _partItemsScroll.SizeChanged += OnPartItemsScrollSizeChanged;
                // 出现/隐藏纵向滚动条时 ViewportWidth 会变，但控件 ActualWidth 常不变，SizeChanged 不会触发，导致填充列未重算而出现横向条
                _partItemsScroll.ScrollChanged += OnPartItemsScrollScrollChanged;
            }

            if (_partOuterScroll != null)
                _partOuterScroll.SizeChanged += OnPartOuterScrollSizeChanged;

            ScheduleStretchLastColumn();
        }

        private void OnPartItemsScrollScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (Math.Abs(e.ViewportWidthChange) > 0.01)
                ScheduleStretchLastColumn();
        }

        private void OnPartItemsScrollSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (e.WidthChanged)
                ScheduleStretchLastColumn();
        }

        private void OnPartOuterScrollSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (e.WidthChanged)
                ScheduleStretchLastColumn();
        }

        /// <summary>
        /// 由首列等动态列宽变更后调用，使 <see cref="StretchFillColumnIndex"/> / <see cref="StretchLastColumn"/> 重新分配剩余宽度。
        /// </summary>
        public void InvalidateColumnFillLayout() => ScheduleStretchLastColumn();

        private void ScheduleStretchLastColumn()
        {
            if (!StretchAllColumns && !StretchLastColumn && StretchFillColumnIndex < 0)
                return;
            if (_stretchScheduled)
            {
                _stretchRepeatRequested = true;
                return;
            }

            _stretchScheduled = true;
            _stretchRepeatRequested = false;
            Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, RunStretchLastColumnPass);
        }

        private void RunStretchLastColumnPass()
        {
            StretchLastColumnToFill();
            _stretchScheduled = false;
            if (_stretchRepeatRequested)
            {
                _stretchRepeatRequested = false;
                ScheduleStretchLastColumn();
            }
        }

        private void StretchLastColumnToFill()
        {
            if (StretchAllColumns)
            {
                StretchAllColumnsProportional();
                return;
            }

            var columns = Columns;
            if (columns == null || columns.Count < 2)
                return;

            var fillIndex = ResolveStretchFillColumnIndex(columns);
            if (fillIndex < 0)
                return;

            var available = GetAvailableWidthForColumns();
            if (available <= 2)
                return;

            double sumOthers = 0;
            for (var i = 0; i < columns.Count; i++)
            {
                if (i == fillIndex)
                    continue;
                var w = columns[i].Width;
                if (double.IsNaN(w) || w <= 0)
                    return;
                sumOthers += w;
            }

            var fillCol = columns[fillIndex];
            var target = Math.Max(LastColumnMinWidth, available - sumOthers);
            if (Math.Abs(fillCol.Width - target) > 0.5)
                fillCol.Width = target;

            // 列宽舍入可能需多轮收束，否则合计仍微大于视口会误出横向滚动条
            for (var iter = 0; iter < 8; iter++)
            {
                double total = 0;
                for (var i = 0; i < columns.Count; i++)
                    total += columns[i].Width;
                if (total <= available + 0.5)
                    break;
                var over = total - available;
                var w = fillCol.Width - over;
                if (w < LastColumnMinWidth - 0.5)
                    break;
                fillCol.Width = Math.Max(LastColumnMinWidth, w);
            }
        }

        private int ResolveStretchFillColumnIndex(GridViewColumnCollection columns)
        {
            if (StretchFillColumnIndex >= 0)
                return StretchFillColumnIndex < columns.Count ? StretchFillColumnIndex : -1;
            if (StretchLastColumn)
                return columns.Count - 1;
            return -1;
        }

        private double GetAvailableWidthForColumns()
        {
            double available = ActualWidth;
            if (available <= 2 || double.IsNaN(available))
                return 0;

            available -= Padding.Left + Padding.Right;
            available -= BorderThickness.Left + BorderThickness.Right;

            // 与行区域一致：内层 ScrollViewer 出现纵向滚动条时 ViewportWidth 小于列头；外层视口亦参与取 min，避免略宽。
            var itemsViewport = TryGetItemsAreaViewportWidth();
            if (itemsViewport > 2 && !double.IsNaN(itemsViewport))
                available = Math.Min(available, itemsViewport);

            if (_partOuterScroll != null)
            {
                var ow = _partOuterScroll.ViewportWidth;
                if (ow > 2 && !double.IsNaN(ow))
                    available = Math.Min(available, ow);
            }

            available -= ColumnLayoutFudgeDip;
            return available > 2 ? available : 0;
        }

        /// <summary>
        /// 优先使用模板部件 <c>PART_ItemsScroll</c>；无部件时按 Border→ScrollViewer→DockPanel→内层 ScrollViewer 回退查找。
        /// </summary>
        private double TryGetItemsAreaViewportWidth()
        {
            if (_partItemsScroll != null && _partItemsScroll.ViewportWidth > 2 && !double.IsNaN(_partItemsScroll.ViewportWidth))
                return _partItemsScroll.ViewportWidth;

            if (VisualTreeHelper.GetChildrenCount(this) == 0)
                return 0;
            if (VisualTreeHelper.GetChild(this, 0) is not Border border)
                return 0;
            if (VisualTreeHelper.GetChildrenCount(border) == 0)
                return 0;
            if (VisualTreeHelper.GetChild(border, 0) is not ScrollViewer outerSv)
                return 0;
            if (VisualTreeHelper.GetChildrenCount(outerSv) == 0)
                return 0;
            if (VisualTreeHelper.GetChild(outerSv, 0) is not DockPanel dock)
                return 0;
            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(dock); i++)
            {
                if (VisualTreeHelper.GetChild(dock, i) is ScrollViewer innerSv)
                    return innerSv.ViewportWidth;
            }

            return 0;
        }

        /// <summary>
        /// 按布局前各列宽度比例为所有列分配可用宽度，末列吸收舍入误差。
        /// </summary>
        private void StretchAllColumnsProportional()
        {
            var columns = Columns;
            if (columns == null || columns.Count == 0)
                return;

            var available = GetAvailableWidthForColumns();
            if (available <= 2)
                return;

            var snapshot = new double[columns.Count];
            double sum = 0;
            for (var i = 0; i < columns.Count; i++)
            {
                var w = columns[i].Width;
                if (double.IsNaN(w) || w <= 0)
                    return;
                snapshot[i] = w;
                sum += w;
            }

            if (sum <= 0)
                return;

            double allocated = 0;
            for (var i = 0; i < columns.Count - 1; i++)
            {
                var c = columns[i];
                var target = available * (snapshot[i] / sum);
                if (Math.Abs(c.Width - target) > 0.5)
                    c.Width = target;
                allocated += c.Width;
            }

            var last = columns[^1];
            var lastTarget = available - allocated;
            if (Math.Abs(last.Width - lastTarget) > 0.5)
                last.Width = lastTarget;

            double total = 0;
            for (var i = 0; i < columns.Count; i++)
                total += columns[i].Width;
            if (total > available + 0.5)
            {
                var over = total - available;
                var w = last.Width - over;
                if (w > 2)
                    last.Width = w;
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

        private void OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!EnableRowDragReorder)
                return;

            _dragStartPoint = e.GetPosition(this);
            _dragSourceContainer = FindAncestor<TreeListViewItem>(e.OriginalSource as DependencyObject);
        }

        private void OnPreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (!EnableRowDragReorder || e.LeftButton != MouseButtonState.Pressed || _dragSourceContainer == null)
                return;

            var current = e.GetPosition(this);
            if (Math.Abs(current.X - _dragStartPoint.X) < SystemParameters.MinimumHorizontalDragDistance &&
                Math.Abs(current.Y - _dragStartPoint.Y) < SystemParameters.MinimumVerticalDragDistance)
            {
                return;
            }

            var sourceItem = ItemContainerGenerator.ItemFromContainer(_dragSourceContainer);
            if (sourceItem == null || Equals(sourceItem, DependencyProperty.UnsetValue))
                return;

            DragDrop.DoDragDrop(_dragSourceContainer, sourceItem, DragDropEffects.Move);
        }

        private void OnDragOver(object sender, DragEventArgs e)
        {
            if (!EnableRowDragReorder || _dragSourceContainer == null)
            {
                e.Effects = DragDropEffects.None;
                e.Handled = true;
                return;
            }

            var targetContainer = FindAncestor<TreeListViewItem>(e.OriginalSource as DependencyObject);
            if (targetContainer == null)
            {
                e.Effects = DragDropEffects.None;
                e.Handled = true;
                return;
            }

            var sourceParent = ItemsControl.ItemsControlFromItemContainer(_dragSourceContainer);
            var targetParent = ItemsControl.ItemsControlFromItemContainer(targetContainer);
            e.Effects = ReferenceEquals(sourceParent, targetParent) ? DragDropEffects.Move : DragDropEffects.None;
            e.Handled = true;
        }

        private void OnDrop(object sender, DragEventArgs e)
        {
            if (!EnableRowDragReorder || _dragSourceContainer == null)
                return;

            var targetContainer = FindAncestor<TreeListViewItem>(e.OriginalSource as DependencyObject);
            if (targetContainer == null || ReferenceEquals(targetContainer, _dragSourceContainer))
                return;

            var sourceParent = ItemsControl.ItemsControlFromItemContainer(_dragSourceContainer);
            var targetParent = ItemsControl.ItemsControlFromItemContainer(targetContainer);
            if (!ReferenceEquals(sourceParent, targetParent) || sourceParent == null)
                return;

            var sourceIndex = sourceParent.ItemContainerGenerator.IndexFromContainer(_dragSourceContainer);
            var targetIndex = targetParent.ItemContainerGenerator.IndexFromContainer(targetContainer);
            if (sourceIndex < 0 || targetIndex < 0 || sourceIndex == targetIndex)
                return;

            if (sourceParent.ItemsSource is IList list && !list.IsReadOnly && sourceIndex < list.Count)
            {
                var moving = list[sourceIndex];
                list.RemoveAt(sourceIndex);
                if (targetIndex > sourceIndex)
                    targetIndex--;
                list.Insert(Math.Max(0, Math.Min(targetIndex, list.Count)), moving);
                e.Handled = true;
            }
        }

        private static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
        {
            while (current != null)
            {
                if (current is T target)
                    return target;
                current = VisualTreeHelper.GetParent(current);
            }

            return null;
        }
    }
}
