using System;
using System.Collections.Specialized;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Data;
using Astra.Core.Nodes.Models;
using Astra.Core.Nodes.Geometry;
using HandyControl.Tools.Extension;
using Astra.UI.Commands;
using CommandManager = Astra.UI.Commands.CommandManager;

namespace Astra.UI.Controls
{
    /// <summary>
    /// 无限画布自定义控件 - 动态引用 Colors.xml
    /// </summary>
    [TemplatePart(Name = PART_ContentCanvas, Type = typeof(Canvas))]
    [TemplatePart(Name = PART_GridLayer, Type = typeof(Canvas))]
    [TemplatePart(Name = PART_AlignmentLayer, Type = typeof(Canvas))]
    [TemplatePart(Name = PART_MinimapContainer, Type = typeof(Border))]
    [TemplatePart(Name = PART_MinimapCanvas, Type = typeof(Canvas))]
    [TemplatePart(Name = PART_ViewportIndicator, Type = typeof(System.Windows.Controls.Primitives.Thumb))]
    [TemplatePart(Name = PART_MinimapCollapseButton, Type = typeof(Button))]
    [TemplatePart(Name = PART_MinimapExpandButton, Type = typeof(Button))]
    [TemplatePart(Name = PART_MinimapFitButton, Type = typeof(Button))]
    public partial class InfiniteCanvas : Control
    {
        private const string PART_ContentCanvas = "PART_ContentCanvas";
        private const string PART_GridLayer = "PART_GridLayer";
        private const string PART_AlignmentLayer = "PART_AlignmentLayer";
        private const string PART_MinimapContainer = "PART_MinimapContainer";
        private const string PART_MinimapCanvas = "PART_MinimapCanvas";
        private const string PART_ViewportIndicator = "PART_ViewportIndicator";
        private const string PART_MinimapCollapseButton = "PART_MinimapCollapseButton";
        private const string PART_MinimapExpandButton = "PART_MinimapExpandButton";
        private const string PART_MinimapFitButton = "PART_MinimapFitButton";

        static InfiniteCanvas()
        {
            DefaultStyleKeyProperty.OverrideMetadata(
                typeof(InfiniteCanvas),
                new FrameworkPropertyMetadata(typeof(InfiniteCanvas)));

            // 确保 InfiniteCanvas 可以获取焦点以接收鼠标滚轮事件
            FocusableProperty.OverrideMetadata(
                typeof(InfiniteCanvas),
                new FrameworkPropertyMetadata(true));
        }

        /// <summary>
        /// 实例构造函数
        /// 确保 SelectedItems 默认可用，避免框选后拖动多选节点时集合为空
        /// </summary>
        public InfiniteCanvas()
        {
            SelectedItems ??= new ObservableCollection<object>();
            
            // 监听控件卸载事件以清理资源
            Unloaded += OnInfiniteCanvasUnloaded;
        }

        /// <summary>
        /// 查找父级的 FlowEditor 并获取其 WorkflowTab
        /// </summary>
        private UI.Models.WorkflowTab FindWorkflowTab()
        {
            var parent = VisualTreeHelper.GetParent(this);
            while (parent != null)
            {
                if (parent is UI.Controls.FlowEditor flowEditor)
                {
                    return flowEditor.DataContext as UI.Models.WorkflowTab;
                }
                parent = VisualTreeHelper.GetParent(parent);
            }
            return null;
        }

        /// <summary>
        /// 控件卸载时清理资源
        /// </summary>
        private void OnInfiniteCanvasUnloaded(object sender, RoutedEventArgs e)
        {
            // 停止并清理定时器
            if (_minimapUpdateTimer != null)
            {
                _minimapUpdateTimer.Stop();
                _minimapUpdateTimer.Tick -= (s, ev) => UpdateMinimapThrottled();
                _minimapUpdateTimer = null;
            }

            // 取消订阅布局更新事件
            if (_contentCanvas != null)
            {
                _contentCanvas.LayoutUpdated -= OnContentCanvasLayoutUpdated;
            }

            // 取消订阅集合变化事件
            if (_itemsCollectionNotify != null)
            {
                _itemsCollectionNotify.CollectionChanged -= OnItemsCollectionChanged;
                _itemsCollectionNotify = null;
            }

            if (_edgeCollectionNotify != null)
            {
                _edgeCollectionNotify.CollectionChanged -= OnEdgeCollectionChanged;
                _edgeCollectionNotify = null;
            }

            // 取消订阅所有节点的属性变化事件
            if (ItemsSource != null)
            {
                foreach (var item in ItemsSource)
                {
                    if (item is System.ComponentModel.INotifyPropertyChanged notifyItem)
                    {
                        notifyItem.PropertyChanged -= OnNodePropertyChanged;
                    }
                }
            }
        }

        #region 依赖属性 - 使用 DynamicResource

        // ============ 视图变换 ============

        public static readonly DependencyProperty ScaleProperty =
            DependencyProperty.Register(nameof(Scale), typeof(double), typeof(InfiniteCanvas),
                new PropertyMetadata(1.0, OnScaleChanged, CoerceScale));

        public double Scale
        {
            get => (double)GetValue(ScaleProperty);
            set => SetValue(ScaleProperty, value);
        }

        private static object CoerceScale(DependencyObject d, object baseValue)
        {
            var canvas = (InfiniteCanvas)d;
            var value = (double)baseValue;
            return Math.Max(canvas.MinScale, Math.Min(canvas.MaxScale, value));
        }

        public static readonly DependencyProperty MinScaleProperty =
            DependencyProperty.Register(nameof(MinScale), typeof(double), typeof(InfiniteCanvas),
                new PropertyMetadata(0.4));  // 最小缩放40%

        public double MinScale
        {
            get => (double)GetValue(MinScaleProperty);
            set => SetValue(MinScaleProperty, value);
        }

        public static readonly DependencyProperty MaxScaleProperty =
            DependencyProperty.Register(nameof(MaxScale), typeof(double), typeof(InfiniteCanvas),
                new PropertyMetadata(2.0));  // 最大缩放200%

        public double MaxScale
        {
            get => (double)GetValue(MaxScaleProperty);
            set => SetValue(MaxScaleProperty, value);
        }

        public static readonly DependencyProperty PanXProperty =
            DependencyProperty.Register(nameof(PanX), typeof(double), typeof(InfiniteCanvas),
                new PropertyMetadata(0.0, OnTransformChanged));

        public double PanX
        {
            get => (double)GetValue(PanXProperty);
            set => SetValue(PanXProperty, value);
        }

        public static readonly DependencyProperty PanYProperty =
            DependencyProperty.Register(nameof(PanY), typeof(double), typeof(InfiniteCanvas),
                new PropertyMetadata(0.0, OnTransformChanged));

        public double PanY
        {
            get => (double)GetValue(PanYProperty);
            set => SetValue(PanYProperty, value);
        }

        // ============ 颜色配置（动态引用 Colors.xml）============

        /// <summary>
        /// 网格画刷 - 默认绑定到 BorderBrush
        /// </summary>
        public static readonly DependencyProperty GridBrushProperty =
            DependencyProperty.Register(nameof(GridBrush), typeof(Brush), typeof(InfiniteCanvas),
                new FrameworkPropertyMetadata(
                    null,
                    FrameworkPropertyMetadataOptions.AffectsRender,
                    OnGridSettingsChanged));

        public Brush GridBrush
        {
            get => (Brush)GetValue(GridBrushProperty);
            set => SetValue(GridBrushProperty, value);
        }

        /// <summary>
        /// 对齐线画刷 - 默认绑定到 SuccessBrush
        /// </summary>
        public static readonly DependencyProperty AlignmentLineBrushProperty =
            DependencyProperty.Register(nameof(AlignmentLineBrush), typeof(Brush), typeof(InfiniteCanvas),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        public Brush AlignmentLineBrush
        {
            get => (Brush)GetValue(AlignmentLineBrushProperty);
            set => SetValue(AlignmentLineBrushProperty, value);
        }

        /// <summary>
        /// 选择框边框画刷 - 默认绑定到 PrimaryBrush
        /// </summary>
        public static readonly DependencyProperty SelectionBorderBrushProperty =
            DependencyProperty.Register(nameof(SelectionBorderBrush), typeof(Brush), typeof(InfiniteCanvas),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        public Brush SelectionBorderBrush
        {
            get => (Brush)GetValue(SelectionBorderBrushProperty);
            set => SetValue(SelectionBorderBrushProperty, value);
        }

        // ============ 网格配置 ============

        public static readonly DependencyProperty ShowGridProperty =
            DependencyProperty.Register(nameof(ShowGrid), typeof(bool), typeof(InfiniteCanvas),
                new PropertyMetadata(true, OnGridSettingsChanged));

        public bool ShowGrid
        {
            get => (bool)GetValue(ShowGridProperty);
            set => SetValue(ShowGridProperty, value);
        }

        public static readonly DependencyProperty GridSpacingProperty =
            DependencyProperty.Register(nameof(GridSpacing), typeof(double), typeof(InfiniteCanvas),
                new PropertyMetadata(40.0, OnGridSettingsChanged));

        public double GridSpacing
        {
            get => (double)GetValue(GridSpacingProperty);
            set => SetValue(GridSpacingProperty, value);
        }

        // ============ 对齐功能 ============

        public static readonly DependencyProperty EnableAlignmentProperty =
            DependencyProperty.Register(nameof(EnableAlignment), typeof(bool), typeof(InfiniteCanvas),
                new PropertyMetadata(true));

        public bool EnableAlignment
        {
            get => (bool)GetValue(EnableAlignmentProperty);
            set => SetValue(EnableAlignmentProperty, value);
        }

        public static readonly DependencyProperty AlignmentToleranceProperty =
            DependencyProperty.Register(nameof(AlignmentTolerance), typeof(double), typeof(InfiniteCanvas),
                new PropertyMetadata(20.0));  // 对齐辅助线触发范围（屏幕px，默认 5）

        public double AlignmentTolerance
        {
            get => (double)GetValue(AlignmentToleranceProperty);
            set => SetValue(AlignmentToleranceProperty, value);
        }

        // ============ 缩略图配置 ============

        public static readonly DependencyProperty ShowMinimapProperty =
            DependencyProperty.Register(nameof(ShowMinimap), typeof(bool), typeof(InfiniteCanvas),
                new PropertyMetadata(true, OnShowMinimapChanged));

        private static void OnShowMinimapChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var canvas = (InfiniteCanvas)d;
            canvas.UpdateMinimapVisibility();
        }

        public bool ShowMinimap
        {
            get => (bool)GetValue(ShowMinimapProperty);
            set => SetValue(ShowMinimapProperty, value);
        }

        public static readonly DependencyProperty MinimapWidthProperty =
            DependencyProperty.Register(nameof(MinimapWidth), typeof(double), typeof(InfiniteCanvas),
                new PropertyMetadata(220.0));

        public double MinimapWidth
        {
            get => (double)GetValue(MinimapWidthProperty);
            set => SetValue(MinimapWidthProperty, value);
        }

        public static readonly DependencyProperty MinimapHeightProperty =
            DependencyProperty.Register(nameof(MinimapHeight), typeof(double), typeof(InfiniteCanvas),
                new PropertyMetadata(170.0));

        public double MinimapHeight
        {
            get => (double)GetValue(MinimapHeightProperty);
            set => SetValue(MinimapHeightProperty, value);
        }

        public static readonly DependencyProperty IsMinimapCollapsedProperty =
            DependencyProperty.Register(nameof(IsMinimapCollapsed), typeof(bool), typeof(InfiniteCanvas),
                new PropertyMetadata(false, OnMinimapCollapsedChanged));

        public bool IsMinimapCollapsed
        {
            get => (bool)GetValue(IsMinimapCollapsedProperty);
            set => SetValue(IsMinimapCollapsedProperty, value);
        }

        /// <summary>
        /// 小地图边界约束模式：true=限制视口在内容边界内，false=无限画布模式
        /// </summary>
        public static readonly DependencyProperty MinimapBoundaryConstraintProperty =
            DependencyProperty.Register(nameof(MinimapBoundaryConstraint), typeof(bool), typeof(InfiniteCanvas),
                new PropertyMetadata(true)); // 默认启用边界约束

        public bool MinimapBoundaryConstraint
        {
            get => (bool)GetValue(MinimapBoundaryConstraintProperty);
            set => SetValue(MinimapBoundaryConstraintProperty, value);
        }

        /// <summary>
        /// 小地图自动适应模式：true=自动调整显示所有节点，false=固定比例
        /// </summary>
        public static readonly DependencyProperty MinimapAutoFitProperty =
            DependencyProperty.Register(nameof(MinimapAutoFit), typeof(bool), typeof(InfiniteCanvas),
                new PropertyMetadata(true, OnMinimapSettingsChanged));

        public bool MinimapAutoFit
        {
            get => (bool)GetValue(MinimapAutoFitProperty);
            set => SetValue(MinimapAutoFitProperty, value);
        }

        // ============ 数据源 ============

        public static readonly DependencyProperty ItemsSourceProperty =
            DependencyProperty.Register(nameof(ItemsSource), typeof(IEnumerable), typeof(InfiniteCanvas),
                new PropertyMetadata(null, OnItemsSourceChanged));

        public IEnumerable ItemsSource
        {
            get => (IEnumerable)GetValue(ItemsSourceProperty);
            set => SetValue(ItemsSourceProperty, value);
        }

        /// <summary>
        /// 撤销/重做管理器（由 FlowEditor 注入）
        /// </summary>
        public Commands.CommandManager UndoRedoManager
        {
            get => _undoRedoManager;
            set
            {
                _undoRedoManager = value;
                
                // 🔧 设置批量操作回调
                if (_undoRedoManager != null)
                {
                    _undoRedoManager.OnBatchOperationBegin = () => BeginBatchUpdate();
                    _undoRedoManager.OnBatchOperationEnd = () => EndBatchUpdate();
                }
            }
        }

        /// <summary>
        /// 剪贴板：存储复制的节点（原始节点，未克隆）
        /// </summary>
        public List<Astra.Core.Nodes.Models.Node> ClipboardNodes { get; set; }

        /// <summary>
        /// 剪贴板：存储复制的连线（原始连线，未克隆）
        /// </summary>
        public List<Astra.Core.Nodes.Models.Edge> ClipboardEdges { get; set; }

        /// <summary>
        /// 复制时节点的边界框（用于保持相对位置）
        /// </summary>
        public Rect? ClipboardBounds { get; set; }

        public static readonly DependencyProperty ItemTemplateProperty =
            DependencyProperty.Register(nameof(ItemTemplate), typeof(DataTemplate), typeof(InfiniteCanvas),
                new PropertyMetadata(null));

        public DataTemplate ItemTemplate
        {
            get => (DataTemplate)GetValue(ItemTemplateProperty);
            set => SetValue(ItemTemplateProperty, value);
        }

        public static readonly DependencyProperty ItemTemplateSelectorProperty =
            DependencyProperty.Register(nameof(ItemTemplateSelector), typeof(DataTemplateSelector), typeof(InfiniteCanvas),
                new PropertyMetadata(null, OnItemTemplateSelectorChanged));

        public DataTemplateSelector ItemTemplateSelector
        {
            get => (DataTemplateSelector)GetValue(ItemTemplateSelectorProperty);
            set => SetValue(ItemTemplateSelectorProperty, value);
        }

        // ============ 交互配置 ============

        public static readonly DependencyProperty EnablePanningProperty =
            DependencyProperty.Register(nameof(EnablePanning), typeof(bool), typeof(InfiniteCanvas),
                new PropertyMetadata(true));

        public bool EnablePanning
        {
            get => (bool)GetValue(EnablePanningProperty);
            set => SetValue(EnablePanningProperty, value);
        }

        public static readonly DependencyProperty EnableZoomProperty =
            DependencyProperty.Register(nameof(EnableZoom), typeof(bool), typeof(InfiniteCanvas),
                new PropertyMetadata(true));

        public bool EnableZoom
        {
            get => (bool)GetValue(EnableZoomProperty);
            set => SetValue(EnableZoomProperty, value);
        }

        public static readonly DependencyProperty PanModifierKeyProperty =
            DependencyProperty.Register(nameof(PanModifierKey), typeof(ModifierKeys), typeof(InfiniteCanvas),
                new PropertyMetadata(ModifierKeys.Control));

        public ModifierKeys PanModifierKey
        {
            get => (ModifierKeys)GetValue(PanModifierKeyProperty);
            set => SetValue(PanModifierKeyProperty, value);
        }

        /// <summary>
        /// 是否启用框选功能
        /// </summary>
        public static readonly DependencyProperty EnableBoxSelectionProperty =
            DependencyProperty.Register(
                nameof(EnableBoxSelection),
                typeof(bool),
                typeof(InfiniteCanvas),
                new PropertyMetadata(true));

        public bool EnableBoxSelection
        {
            get => (bool)GetValue(EnableBoxSelectionProperty);
            set => SetValue(EnableBoxSelectionProperty, value);
        }

        /// <summary>
        /// 当前选中的项集合
        /// </summary>
        public static readonly DependencyProperty SelectedItemsProperty =
            DependencyProperty.Register(
                nameof(SelectedItems),
                typeof(IList),
                typeof(InfiniteCanvas),
                new PropertyMetadata(null, OnSelectedItemsChanged));

        public IList SelectedItems
        {
            get => (IList)GetValue(SelectedItemsProperty);
            set => SetValue(SelectedItemsProperty, value);
        }

        /// <summary>
        /// 当前选中的节点数量（只读，用于状态栏显示）
        /// </summary>
        public static readonly DependencyProperty SelectedItemsCountProperty =
            DependencyProperty.Register(
                nameof(SelectedItemsCount),
                typeof(int),
                typeof(InfiniteCanvas),
                new PropertyMetadata(0));

        public int SelectedItemsCount
        {
            get => (int)GetValue(SelectedItemsCountProperty);
            private set => SetValue(SelectedItemsCountProperty, value);
        }

        /// <summary>
        /// 画布上所有节点的总数（只读，用于状态栏显示）
        /// </summary>
        public static readonly DependencyProperty TotalItemsCountProperty =
            DependencyProperty.Register(
                nameof(TotalItemsCount),
                typeof(int),
                typeof(InfiniteCanvas),
                new PropertyMetadata(0));

        public int TotalItemsCount
        {
            get => (int)GetValue(TotalItemsCountProperty);
            private set => SetValue(TotalItemsCountProperty, value);
        }

        /// <summary>
        /// 是否锁定画布（锁定后禁止拖拽节点和框选）
        /// </summary>
        public static readonly DependencyProperty IsLockedProperty =
            DependencyProperty.Register(
                nameof(IsLocked),
                typeof(bool),
                typeof(InfiniteCanvas),
                new PropertyMetadata(false));

        public bool IsLocked
        {
            get => (bool)GetValue(IsLockedProperty);
            set => SetValue(IsLockedProperty, value);
        }

        private static void OnSelectedItemsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var canvas = (InfiniteCanvas)d;
            
            // 取消订阅旧集合的变化事件
            if (e.OldValue is INotifyCollectionChanged oldNotify)
            {
                oldNotify.CollectionChanged -= canvas.OnSelectedItemsCollectionChanged;
            }
            
            // 订阅新集合的变化事件
            if (e.NewValue is INotifyCollectionChanged newNotify)
            {
                newNotify.CollectionChanged += canvas.OnSelectedItemsCollectionChanged;
            }
            
            // 立即更新数量
            canvas.UpdateSelectedItemsCount();
            canvas.UpdateSelectedGroupBox();
        }

        private void OnSelectedItemsCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            UpdateSelectedItemsCount();
            UpdateSelectedGroupBox();
        }

        private void UpdateSelectedItemsCount()
        {
            SelectedItemsCount = SelectedItems?.Count ?? 0;
        }

        /// <summary>
        /// 更新节点总数
        /// </summary>
        private void UpdateTotalItemsCount()
        {
            if (ItemsSource == null)
            {
                TotalItemsCount = 0;
                return;
            }

            // 计算集合中的项目数量
            int count = 0;
            foreach (var item in ItemsSource)
            {
                count++;
            }
            TotalItemsCount = count;
        }

        #endregion

        #region 路由事件

        public static readonly RoutedEvent ViewTransformChangedEvent =
            EventManager.RegisterRoutedEvent(
                nameof(ViewTransformChanged),
                RoutingStrategy.Bubble,
                typeof(RoutedEventHandler),
                typeof(InfiniteCanvas));

        public event RoutedEventHandler ViewTransformChanged
        {
            add => AddHandler(ViewTransformChangedEvent, value);
            remove => RemoveHandler(ViewTransformChangedEvent, value);
        }

        #endregion

        #region 私有字段

        private Canvas _contentCanvas;
        private Canvas _gridLayer;
        private Canvas _alignmentLayer;
        private ScaleTransform _scaleTransform;
        private TranslateTransform _translateTransform;
        private ViewportState _state = new();
        private Border _minimapContainer;
        private Canvas _minimapCanvas;
        private System.Windows.Controls.Primitives.Thumb _viewportIndicator;
        private Button _minimapCollapseButton;
        private Button _minimapExpandButton;
        private Button _minimapFitButton;
        private bool _isNavigatingMinimap;
        
        // 🔧 当前显示的对齐线坐标（用于松开鼠标时的精确对齐）
        private List<double> _currentAlignmentVerticals = null;  // 垂直对齐线的X坐标
        private List<double> _currentAlignmentHorizontals = null;  // 水平对齐线的Y坐标
        private bool _isDraggingViewportIndicator;  // 是否正在拖拽视口指示器
        private Point _viewportIndicatorDragStart;  // 视口指示器拖拽起始点（小地图坐标）
        private FrameworkElement _transformTarget; // 专门用于承载缩放/平移变换的视觉元素
        private INotifyCollectionChanged _itemsCollectionNotify;
        private CommandManager _undoRedoManager;

        // 框选相关字段
        private bool _isBoxSelecting;
        private Point _selectionStartPoint;
        private Rectangle _selectionBox;
        private Rectangle _selectedGroupBox;
        private List<object> _selectedItems = new List<object>();

        // 性能优化：节流控制
        private DateTime _lastGridUpdateTime = DateTime.MinValue;
        private const int GridUpdateThrottleMs = 16; // 约60fps
        
        // 小地图更新节流
        private DateTime _lastMinimapUpdateTime = DateTime.MinValue;
        private const int MinimapUpdateThrottleMs = 100; // 每100ms最多更新一次
        private System.Windows.Threading.DispatcherTimer _minimapUpdateTimer;
        private bool _suppressMinimapUpdateAfterDrag; // 拖拽结束后抑制一次小地图重算，防止跳回
        private bool _minimapNeedsRecalc = true; // 小地图是否需要重算（内容/尺寸变化时置为 true）

        // 🔧 缩放节流控制（超过10个节点时启用）
        private DateTime _lastZoomTime = DateTime.MinValue;
        private const int ZoomThrottleMs = 16; // 约60fps
        private bool _isZooming = false; // 是否正在缩放（用于跳过连线刷新）
        private System.Windows.Threading.DispatcherTimer _zoomEndTimer; // 缩放结束定时器

        // 🔧 节点属性变化节流控制（避免频繁更新）
        private DateTime _lastNodePropertyUpdateTime = DateTime.MinValue;
        private const int NodePropertyUpdateThrottleMs = 20; // 50fps（与连线刷新频率一致）
        private System.Windows.Threading.DispatcherTimer _nodePropertyUpdateTimer;
        private const int PerformanceNodeThreshold = 10; // 节点数量阈值：超过此数量启用性能优化

        #endregion

        #region 模板应用

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            _contentCanvas = GetTemplateChild(PART_ContentCanvas) as Canvas;
            _gridLayer = GetTemplateChild(PART_GridLayer) as Canvas;
            _alignmentLayer = GetTemplateChild(PART_AlignmentLayer) as Canvas;
            _minimapContainer = GetTemplateChild(PART_MinimapContainer) as Border;
            _minimapCanvas = GetTemplateChild(PART_MinimapCanvas) as Canvas;
            _viewportIndicator = GetTemplateChild(PART_ViewportIndicator) as System.Windows.Controls.Primitives.Thumb;
            _minimapCollapseButton = GetTemplateChild(PART_MinimapCollapseButton) as Button;
            _minimapExpandButton = GetTemplateChild(PART_MinimapExpandButton) as Button;
            _minimapFitButton = GetTemplateChild(PART_MinimapFitButton) as Button;

            // 清除 XAML 中可能存在的 Visibility 绑定，由代码完全接管控制权
            // 避免 Binding 和 Code Behind 冲突导致的状态不一致（如缩小后无法还原）
            if (_minimapContainer != null) BindingOperations.ClearBinding(_minimapContainer, VisibilityProperty);
            if (_minimapExpandButton != null) BindingOperations.ClearBinding(_minimapExpandButton, VisibilityProperty);
            if (_minimapCollapseButton != null) BindingOperations.ClearBinding(_minimapCollapseButton, VisibilityProperty);

            // 初始化缩略图可见性
            UpdateMinimapVisibility();

            // 获取框选矩形（如果模板中有）
            _selectionBox = GetTemplateChild("PART_SelectionBox") as Rectangle;
            if (_selectionBox != null)
            {
                _selectionBox.Visibility = Visibility.Collapsed;
            }

            _selectedGroupBox = GetTemplateChild("PART_SelectedGroupBox") as Rectangle;
            if (_selectedGroupBox != null)
            {
                _selectedGroupBox.Visibility = Visibility.Collapsed;
                // 保持命中检测开启，让右键点在虚线框区域时不会触发节点级右键菜单
                // 左键拖动与判断仍由代码逻辑控制
            }

            // 启用拖放功能
            AllowDrop = true;
            IsHitTestVisible = true;

            // 注意：拖放事件处理已移除（方法未定义）

            // 如果外部已经通过绑定设置了 ItemTemplateSelector，第一次应用模板时就清除默认的 ItemTemplate
            // 否则 InfiniteCanvas 会先用默认的 NodeControl 渲染，导致主流程引用节点出现错误样式
            if (ItemTemplateSelector != null && ItemTemplate != null)
            {
                ItemTemplate = null;
                System.Diagnostics.Debug.WriteLine("[InfiniteCanvas] 初始化时清除默认 ItemTemplate，优先使用 ItemTemplateSelector");
            }
            
            // 获取内部的 TemplateSelectorItemsControl，并确保模板设置正确
            if (_contentCanvas != null)
            {
                var itemsControl = _contentCanvas.Children.OfType<TemplateSelectorItemsControl>().FirstOrDefault();
                if (itemsControl != null)
                {
                    if (ItemTemplateSelector != null)
                    {
                        // 如果设置了 ItemTemplateSelector，确保 ItemsControl 的 ItemTemplate 为 null
                        itemsControl.ItemTemplate = null;
                        System.Diagnostics.Debug.WriteLine("[InfiniteCanvas] 清除 ItemsControl 的 ItemTemplate，使用 ItemTemplateSelector");
                    }
                    else if (ItemTemplate != null)
                    {
                        // 如果没有 ItemTemplateSelector，将 InfiniteCanvas 的 ItemTemplate 传递给 ItemsControl
                        itemsControl.ItemTemplate = ItemTemplate;
                        System.Diagnostics.Debug.WriteLine("[InfiniteCanvas] 将 ItemTemplate 传递给 ItemsControl");
                    }
                }
            }

            if (_contentCanvas != null)
            {
                // 确保内容画布启用拖放
                _contentCanvas.AllowDrop = true;

                EnsureEdgeLayer();

                // 锁定真正承载缩放/平移的目标（只对内容做变换，不缩放命中区域）
                ResolveTransformTarget();

                // 订阅拖放事件（仅保留已定义的方法）
                _contentCanvas.PreviewDragOver += OnContentCanvasDragOver;
                _contentCanvas.PreviewDragEnter += OnContentCanvasDragEnter;
                _contentCanvas.DragOver += OnContentCanvasDragOver;
                _contentCanvas.DragEnter += OnContentCanvasDragEnter;
                // 注意：OnContentCanvasDragLeave 和 OnContentCanvasDrop 未定义，已移除注册

                // 🆕 初始化服务层
                InitializeTransformService();

                InitializeTransforms();
                InitializeEventHandlers();
                InitializeMinimapUpdateTimer();
            }

            if (_minimapCanvas != null && _viewportIndicator != null)
            {
                InitializeMinimap();
            }

            InitializeMinimapButtons();

            // 延迟更新网格和缩略图，等待布局完成后再绘制
            Dispatcher.BeginInvoke(new Action(() =>
            {
                UpdateGrid();
                UpdateMinimap();
                UpdateViewportIndicator();
                
                // 刷新 ItemsControl 的模板，确保模板选择器正确应用
                RefreshItemsControlTemplate();
                
                // 延迟验证并修复容器模板
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    VerifyAndFixContainerTemplates();
                }), System.Windows.Threading.DispatcherPriority.Loaded);
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        /// <summary>
        /// 确定用于应用缩放/平移变换的视觉对象。
        /// 仅对实际内容（ItemsControl）做变换，保持命中区域与画布大小一致，避免缩放后可交互区域缩小。
        /// </summary>
        private void ResolveTransformTarget()
        {
            // 模板中 ItemsControl 是 PART_ContentCanvas 的直接子元素
            var itemsHost = _contentCanvas?.Children.OfType<ItemsControl>().FirstOrDefault();

            // 优先对 ItemsControl 施加变换，保持外层 Canvas（透明背景）尺寸不变以承接命中测试
            _transformTarget = itemsHost as FrameworkElement ?? _contentCanvas;
        }

        private void InitializeTransforms()
        {
            if (_transformTarget == null)
            {
                return;
            }

            // 创建共享的 ScaleTransform 和 TranslateTransform
            _scaleTransform = new ScaleTransform(Scale, Scale);
            _translateTransform = new TranslateTransform(PanX, PanY);

            // 为 transformTarget 创建 TransformGroup
            var transformGroup = new TransformGroup();
            transformGroup.Children.Add(_scaleTransform);
            transformGroup.Children.Add(_translateTransform);
            _transformTarget.RenderTransform = transformGroup;
            _transformTarget.RenderTransformOrigin = new Point(0, 0);

            ApplyTransformsToLayers();
        }

        /// <summary>
        /// 将当前平移/缩放应用到连线层和预览层（使用独立的 Transform 对象，避免同一 Transform 多父问题）
        /// 修改：复用 _scaleTransform 和 _translateTransform 以确保同步更新
        /// </summary>
        private void ApplyTransformsToLayers()
        {
            // 确保变换对象已初始化
            if (_scaleTransform == null || _translateTransform == null)
                return;

            if (_edgeLayer != null)
            {
                var edgeTransformGroup = new TransformGroup();
                // 复用相同的变换对象，实现同步更新
                edgeTransformGroup.Children.Add(_scaleTransform);
                edgeTransformGroup.Children.Add(_translateTransform);
                _edgeLayer.RenderTransform = edgeTransformGroup;
                _edgeLayer.RenderTransformOrigin = new Point(0, 0);
            }
            if (_connectionPreviewLayer != null)
            {
                var previewTransformGroup = new TransformGroup();
                // 复用相同的变换对象，实现同步更新
                previewTransformGroup.Children.Add(_scaleTransform);
                previewTransformGroup.Children.Add(_translateTransform);
                _connectionPreviewLayer.RenderTransform = previewTransformGroup;
                _connectionPreviewLayer.RenderTransformOrigin = new Point(0, 0);
            }
        }


        private void InitializeMinimap()
        {
            if (_minimapCanvas == null || _viewportIndicator == null) return;

            // 设置视口指示器基本属性
            _viewportIndicator.IsHitTestVisible = true;
            _viewportIndicator.Focusable = true;
            _viewportIndicator.Cursor = Cursors.Hand;
            Panel.SetZIndex(_viewportIndicator, 1000);
            
            // 使用新的简化事件处理（直接拖动，无需 Shift）
            _minimapCanvas.PreviewMouseLeftButtonDown += OnMinimapMouseDownSimplified;
            _minimapCanvas.PreviewMouseMove += OnMinimapMouseMoveSimplified;
            _minimapCanvas.PreviewMouseLeftButtonUp += OnMinimapMouseUpSimplified;
            
            // 滚轮缩放
            _viewportIndicator.MouseWheel += OnViewportIndicatorMouseWheel;

            // 监听布局更新，确保视口指示器正确显示
            _minimapCanvas.LayoutUpdated += (s, e) => UpdateViewportIndicator();

            // 绑定右键菜单的适应画布事件
            if (_minimapCanvas.ContextMenu != null)
            {
                var fitMenuItem = _minimapCanvas.ContextMenu.Items.OfType<MenuItem>()
                    .FirstOrDefault(m => m.Name == "PART_MinimapContextFit");
                if (fitMenuItem != null)
                {
                    fitMenuItem.Click += (s, e) => FitToScreen();
                }
            }
        }

        private void InitializeMinimapButtons()
        {
            // 折叠按钮
            if (_minimapCollapseButton != null)
            {
                _minimapCollapseButton.Click += (s, e) => IsMinimapCollapsed = true;
            }

            // 展开按钮
            if (_minimapExpandButton != null)
            {
                _minimapExpandButton.Click += (s, e) => IsMinimapCollapsed = false;
            }

            // 适应画布按钮
            if (_minimapFitButton != null)
            {
                _minimapFitButton.Click += (s, e) => FitToScreen();
            }
        }

        /// <summary>
        /// 初始化小地图更新定时器（用于拖动节点时的实时更新）
        /// </summary>
        private void InitializeMinimapUpdateTimer()
        {
            // 创建定时器，用于在拖动等操作时定期更新小地图
            _minimapUpdateTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(MinimapUpdateThrottleMs)
            };
            _minimapUpdateTimer.Tick += (s, e) =>
            {
                // 定时器触发时更新小地图
                UpdateMinimapThrottled();
            };

            // 监听内容画布的布局更新（节点拖动时会触发）
            if (_contentCanvas != null)
            {
                _contentCanvas.LayoutUpdated += OnContentCanvasLayoutUpdated;
            }
        }

        /// <summary>
        /// 内容画布布局更新时的回调（节点拖动、大小变化等会触发）
        /// </summary>
        private void OnContentCanvasLayoutUpdated(object sender, EventArgs e)
        {
            // 启动或重置定时器（防止频繁更新）
            if (!_minimapUpdateTimer.IsEnabled)
            {
                _minimapUpdateTimer.Start();
            }
        }

        /// <summary>
        /// 带节流的小地图更新（避免频繁刷新）
        /// </summary>
        private void UpdateMinimapThrottled()
        {
            var now = DateTime.Now;
            if ((now - _lastMinimapUpdateTime).TotalMilliseconds < MinimapUpdateThrottleMs)
            {
                return;
            }

            _lastMinimapUpdateTime = now;
            
            // 停止定时器直到下次布局更新
            _minimapUpdateTimer.Stop();

            // 更新小地图和视口指示器
            UpdateMinimap();
            UpdateViewportIndicator();
        }

        /// <summary>
        /// 请求更新小地图（带节流，用于拖动等频繁操作）
        /// 使用增量更新策略，不重新计算边界，只更新节点位置
        /// </summary>
        public void RequestMinimapUpdate()
        {
            // 使用节流机制，避免过于频繁的更新
            var now = DateTime.Now;
            if ((now - _lastMinimapUpdateTime).TotalMilliseconds >= MinimapUpdateThrottleMs)
            {
                _lastMinimapUpdateTime = now;
                UpdateMinimapIncremental();
            }
        }

        /// <summary>
        /// 增量更新小地图（仅更新节点位置，不重新计算边界）
        /// </summary>
        private void UpdateMinimapIncremental()
        {
            if (_minimapCanvas == null || !ShowMinimap || _contentCanvas == null || IsMinimapCollapsed)
                return;

            if (_isDraggingViewportIndicator)
                return;
            
            // 如果还没有计算过边界和缩放，先进行完整更新
            if (_minimapContentBounds.IsEmpty || _minimapScale <= 0)
            {
                _minimapNeedsRecalc = true;
                UpdateMinimap();
                return;
            }
            
            // 只更新节点矩形的位置，不清空画布
            var itemsControl = _contentCanvas?.Children.OfType<ItemsControl>().FirstOrDefault();
            if (itemsControl == null || ItemsSource == null)
                return;
            
            // 获取小地图上的所有节点矩形（OfType<Rectangle> 已自动排除 Thumb 类型的视口指示器）
            var minimapRects = _minimapCanvas.Children.OfType<Rectangle>()
                .ToList();
            
            int index = 0;
            foreach (var item in ItemsSource)
            {
                var nodeDimensions = GetItemDimensions(item, itemsControl);
                if (!nodeDimensions.HasValue)
                    continue;
                
                var (x, y, width, height) = nodeDimensions.Value;
                
                // 如果有对应的矩形，更新其位置
                if (index < minimapRects.Count)
                {
                    var rect = minimapRects[index];
                    Canvas.SetLeft(rect, (x - _minimapContentBounds.Left) * _minimapScale);
                    Canvas.SetTop(rect, (y - _minimapContentBounds.Top) * _minimapScale);
                }
                
                index++;
            }
        }
        
        /// <summary>
        /// 强制立即更新小地图（忽略节流，用于重要操作后的立即刷新）
        /// </summary>
        public void ForceUpdateMinimap()
        {
            _suppressMinimapUpdateAfterDrag = false;
            _minimapNeedsRecalc = true;
            _lastMinimapUpdateTime = DateTime.MinValue;
            UpdateMinimap();
            UpdateViewportIndicator();
        }

        #endregion

        #region 坐标转换

        public Point ScreenToCanvas(Point screenPoint)
        {
            // 优先使用服务层
            if (_transformService != null)
            {
                return _transformService.ScreenToCanvas(screenPoint);
            }
            
            // 回退到原有逻辑（服务未初始化时）
            return new Point(
                (screenPoint.X - PanX) / Scale,
                (screenPoint.Y - PanY) / Scale
            );
        }

        public Point CanvasToScreen(Point canvasPoint)
        {
            // 优先使用服务层
            if (_transformService != null)
            {
                return _transformService.CanvasToScreen(canvasPoint);
            }
            
            // 回退到原有逻辑（服务未初始化时）
            return new Point(
                canvasPoint.X * Scale + PanX,
                canvasPoint.Y * Scale + PanY
            );
        }

        /// <summary>
        /// 获取鼠标位置的逻辑坐标（统一的未缩放/未平移坐标系）
        /// </summary>
        private Point GetLogicalMousePoint(MouseEventArgs e)
        {
            // 优先使用 transformTarget (内容画布) 坐标
            // WPF 的 GetPosition 会自动处理 RenderTransform 的逆变换，直接返回逻辑坐标
            if (_transformTarget != null)
            {
                try
                {
                    return e.GetPosition(_transformTarget);
                }
                catch
                {
                    // 忽略，使用回退方案
                }
            }

            // 回退：使用 ScreenToCanvas
            return ScreenToCanvas(e.GetPosition(this));
        }

        /// <summary>
        /// 获取鼠标位置的逻辑坐标（统一的未缩放/未平移坐标系）- MouseButtonEventArgs 重载
        /// </summary>
        private Point GetLogicalMousePoint(MouseButtonEventArgs e)
        {
            // 优先使用 transformTarget (内容画布) 坐标
            // WPF 的 GetPosition 会自动处理 RenderTransform 的逆变换，直接返回逻辑坐标
            if (_transformTarget != null)
            {
                try
                {
                    return e.GetPosition(_transformTarget);
                }
                catch
                {
                    // 忽略，使用回退方案
                }
            }

            // 回退：使用 ScreenToCanvas
            return ScreenToCanvas(e.GetPosition(this));
        }

        #endregion

        #region 视图操作

        public void ResetView()
        {
            // 优先使用服务层
            if (_transformService != null)
            {
                _transformService.ResetView();
                return;
            }
            
            // 回退到原有逻辑
            Scale = 1.0;
            PanX = 0;
            PanY = 0;
        }

        public void ZoomToPoint(Point screenPoint, double zoomFactor)
        {
            if (!EnableZoom) return;

            // 🔧 性能优化：节点数量超过10时，启用缩放节流
            int nodeCount = ItemsSource?.Cast<object>().Count() ?? 0;
            if (nodeCount > PerformanceNodeThreshold)
            {
                var now = DateTime.Now;
                if ((now - _lastZoomTime).TotalMilliseconds < ZoomThrottleMs)
                {
                    // 节流期间，重置缩放结束定时器
                    if (_zoomEndTimer != null && _zoomEndTimer.IsEnabled)
                    {
                        _zoomEndTimer.Stop();
                        _zoomEndTimer.Start();
                    }
                    return;
                }
                _lastZoomTime = now;

                // 标记正在缩放（用于跳过连线刷新）
                _isZooming = true;

                // 启动或重置缩放结束定时器（100ms后认为缩放结束）
                if (_zoomEndTimer == null)
                {
                    _zoomEndTimer = new System.Windows.Threading.DispatcherTimer
                    {
                        Interval = TimeSpan.FromMilliseconds(100)
                    };
                    _zoomEndTimer.Tick += (s, e) =>
                    {
                        _isZooming = false;
                        _zoomEndTimer.Stop();
                        
                        // 🔧 缩放结束后，统一更新所有内容
                        _lastGridUpdateTime = DateTime.MinValue; // 重置节流，确保立即更新
                        UpdateGrid();
                        UpdateViewportIndicator();
                        UpdateSelectedGroupBox();
                        UpdateMinimap();
                        RefreshEdgesImmediate();
                        
                        System.Diagnostics.Debug.WriteLine("[缩放] 结束，已更新所有内容");
                    };
                }
                _zoomEndTimer.Stop();
                _zoomEndTimer.Start();
            }

            // 优先使用服务层
            if (_transformService != null)
            {
                _transformService.ZoomToPoint(screenPoint, zoomFactor);
                return;
            }
            
            // 回退到原有逻辑
            var canvasBefore = ScreenToCanvas(screenPoint);
            Scale *= zoomFactor;
            var canvasAfter = ScreenToCanvas(screenPoint);

            PanX += (canvasAfter.X - canvasBefore.X) * Scale;
            PanY += (canvasAfter.Y - canvasBefore.Y) * Scale;
        }

        public void Pan(double deltaX, double deltaY)
        {
            if (!EnablePanning) return;

            // 优先使用服务层
            if (_transformService != null)
            {
                _transformService.Pan(deltaX, deltaY);
                return;
            }
            
            // 回退到原有逻辑
            PanX += deltaX;
            PanY += deltaY;
        }

        /// <summary>
        /// 适应画布：自动调整视图使所有节点可见
        /// </summary>
        public void FitToScreen()
        {
            if (ItemsSource == null || ActualWidth <= 0 || ActualHeight <= 0)
                return;

            var bounds = GetContentBounds();
            if (bounds.IsEmpty)
            {
                // 没有内容时重置到默认视图
                ResetView();
                return;
            }

            // 优先使用服务层
            if (_transformService != null)
            {
                _transformService.FitToScreen(bounds, ActualWidth, ActualHeight);
                
                // 立即更新小地图
                UpdateMinimap();
                UpdateViewportIndicator();
                return;
            }

            // 回退到原有逻辑
            // 添加边距（10%）
            const double marginPercent = 0.1;
            var marginX = bounds.Width * marginPercent;
            var marginY = bounds.Height * marginPercent;
            var paddedBounds = new Rect(
                bounds.X - marginX,
                bounds.Y - marginY,
                bounds.Width + marginX * 2,
                bounds.Height + marginY * 2
            );

            // 计算缩放比例以适应视口
            var scaleX = ActualWidth / paddedBounds.Width;
            var scaleY = ActualHeight / paddedBounds.Height;
            var targetScale = Math.Min(scaleX, scaleY);

            // 限制缩放范围
            targetScale = Math.Max(MinScale, Math.Min(MaxScale, targetScale));

            // 计算居中位置
            var centerX = paddedBounds.Left + paddedBounds.Width / 2;
            var centerY = paddedBounds.Top + paddedBounds.Height / 2;

            // 设置缩放和平移
            Scale = targetScale;
            PanX = ActualWidth / 2 - centerX * targetScale;
            PanY = ActualHeight / 2 - centerY * targetScale;

            // 立即更新小地图
            UpdateMinimap();
            UpdateViewportIndicator();
        }

        #endregion

        #region 网格绘制（动态使用 GridBrush）

        private void UpdateGrid()
        {
            if (_gridLayer == null || !ShowGrid) return;

            var width = ActualWidth;
            var height = ActualHeight;

            // 如果尺寸无效，跳过绘制（但不启用节流，允许下次重试）
            if (width <= 0 || height <= 0)
                return;

            // 节流控制：避免频繁更新
            var now = DateTime.Now;
            if ((now - _lastGridUpdateTime).TotalMilliseconds < GridUpdateThrottleMs)
                return;

            _lastGridUpdateTime = now;

            _gridLayer.Children.Clear();

            // 🔧 网格间距固定（屏幕像素），不随画布缩放变化
            var spacing = GridSpacing;

            if (spacing < 5) return;

            // 动态获取网格画刷
            var gridBrush = GridBrush ?? TryFindResource("BorderBrush") as Brush ?? Brushes.LightGray;

            // 🔧 性能优化：使用预计算的线条数量，避免在循环中创建过多对象
            int vLineCount = (int)(width / spacing) + 1;
            int hLineCount = (int)(height / spacing) + 1;

            // 绘制垂直线
            for (double x = PanX % spacing; x < width; x += spacing)
            {
                _gridLayer.Children.Add(new Line
                {
                    X1 = x,
                    Y1 = 0,
                    X2 = x,
                    Y2 = height,
                    Stroke = gridBrush,
                    StrokeThickness = 0.5
                });
            }

            // 绘制水平线
            for (double y = PanY % spacing; y < height; y += spacing)
            {
                _gridLayer.Children.Add(new Line
                {
                    X1 = 0,
                    Y1 = y,
                    X2 = width,
                    Y2 = y,
                    Stroke = gridBrush,
                    StrokeThickness = 0.5
                });
            }
        }

        #endregion

        #region 对齐辅助线（动态使用 AlignmentLineBrush）

        /// <summary>
        /// 显示对齐辅助线
        /// </summary>
        public void ShowAlignmentLines(
            IEnumerable<double> verticalPositions,
            IEnumerable<double> horizontalPositions,
            Rect? movingBounds = null,
            List<(double x, double y1, double y2)> verticalExtents = null,
            List<(double y, double x1, double x2)> horizontalExtents = null)
        {
            if (_alignmentLayer == null || !EnableAlignment) return;

            _alignmentLayer.Children.Clear();

            // ✅ 动态获取对齐线画刷
            var lineBrush = AlignmentLineBrush ?? TryFindResource("SuccessBrush") as Brush ?? Brushes.Green;
            var width = ActualWidth;
            var height = ActualHeight;

            // 默认长度（缩短显示范围，避免对齐线过长干扰）
            const double linePadding = 6;
            double defaultVTop = 0, 
            defaultVBottom = height;
            double defaultHLeft = 0, 
            defaultHRight = width;

            if (movingBounds.HasValue && movingBounds.Value.Width > 0 && movingBounds.Value.Height > 0)
            {
                var mb = movingBounds.Value;
                defaultVTop = Math.Max(0, CanvasToScreen(new Point(0, mb.Top - linePadding)).Y);
                defaultVBottom = Math.Min(height, CanvasToScreen(new Point(0, mb.Bottom + linePadding)).Y);
                defaultHLeft = Math.Max(0, CanvasToScreen(new Point(mb.Left - linePadding, 0)).X);
                defaultHRight = Math.Min(width, CanvasToScreen(new Point(mb.Right + linePadding, 0)).X);
            }

            // 🔧 绘制垂直对齐线（在节点区域的左右两侧各显示一条）
            if (verticalPositions != null && movingBounds.HasValue)
            {
                foreach (var x in verticalPositions)
                {
                    double y1 = defaultVTop;
                    double y2 = defaultVBottom;

                    if (verticalExtents != null)
                    {
                        var match = verticalExtents.FirstOrDefault(v => Math.Abs(v.x - x) < 0.5);
                        if (!match.Equals(default((double, double, double))))
                        {
                            y1 = CanvasToScreen(new Point(0, match.y1)).Y;
                            y2 = CanvasToScreen(new Point(0, match.y2)).Y;
                        }
                    }

                    y1 = Math.Max(0, Math.Min(y1, height));
                    y2 = Math.Max(0, Math.Min(y2, height));

                    var mb = movingBounds.Value;
                    // 计算节点区域的左右边界
                    var leftX = CanvasToScreen(new Point(Math.Min(x, mb.Left) - 5, 0)).X;
                    var rightX = CanvasToScreen(new Point(Math.Max(x, mb.Right) + 5, 0)).X;

                    // 左侧对齐线
                    _alignmentLayer.Children.Add(new Line
                    {
                        X1 = leftX,
                        Y1 = y1,
                        X2 = leftX,
                        Y2 = y2,
                        Stroke = lineBrush,
                        StrokeThickness = 1.5,
                        StrokeDashArray = new DoubleCollection { 4, 4 }
                    });

                    // 右侧对齐线
                    _alignmentLayer.Children.Add(new Line
                    {
                        X1 = rightX,
                        Y1 = y1,
                        X2 = rightX,
                        Y2 = y2,
                        Stroke = lineBrush,
                        StrokeThickness = 1.5,
                        StrokeDashArray = new DoubleCollection { 4, 4 }
                    });
                }
            }

            // 🔧 绘制水平对齐线（在节点区域的上下两侧各显示一条）
            if (horizontalPositions != null && movingBounds.HasValue)
            {
                foreach (var y in horizontalPositions)
                {
                    double x1 = defaultHLeft;
                    double x2 = defaultHRight;

                    if (horizontalExtents != null)
                    {
                        var match = horizontalExtents.FirstOrDefault(h => Math.Abs(h.y - y) < 0.5);
                        if (!match.Equals(default((double, double, double))))
                        {
                            x1 = CanvasToScreen(new Point(match.x1, 0)).X;
                            x2 = CanvasToScreen(new Point(match.x2, 0)).X;
                        }
                    }

                    x1 = Math.Max(0, Math.Min(x1, width));
                    x2 = Math.Max(0, Math.Min(x2, width));

                    var mb = movingBounds.Value;
                    // 计算节点区域的上下边界
                    var topY = CanvasToScreen(new Point(0, Math.Min(y, mb.Top) - 5)).Y;
                    var bottomY = CanvasToScreen(new Point(0, Math.Max(y, mb.Bottom) + 5)).Y;

                    // 上方对齐线
                    _alignmentLayer.Children.Add(new Line
                    {
                        X1 = x1,
                        Y1 = topY,
                        X2 = x2,
                        Y2 = topY,
                        Stroke = lineBrush,
                        StrokeThickness = 1.5,
                        StrokeDashArray = new DoubleCollection { 4, 4 }
                    });

                    // 下方对齐线
                    _alignmentLayer.Children.Add(new Line
                    {
                        X1 = x1,
                        Y1 = bottomY,
                        X2 = x2,
                        Y2 = bottomY,
                        Stroke = lineBrush,
                        StrokeThickness = 1.5,
                        StrokeDashArray = new DoubleCollection { 4, 4 }
                    });
                }
            }
        }

        public void HideAlignmentLines()
        {
            _alignmentLayer?.Children.Clear();
            // 🔧 清空保存的对齐线坐标
            _currentAlignmentVerticals = null;
            _currentAlignmentHorizontals = null;
        }

        /// <summary>
        /// 拖动节点时根据当前位置动态计算对齐线
        /// </summary>
        /// <param name="movingItem">正在移动的节点对象</param>
        /// <param name="movingBounds">节点在画布坐标系中的矩形</param>
        internal void UpdateAlignmentGuides(object movingItem, Rect movingBounds)
        {
            if (!EnableAlignment || _alignmentLayer == null)
            {
                HideAlignmentLines();
                return;
            }

            if (ItemsSource == null || _contentCanvas == null)
            {
                HideAlignmentLines();
                return;
            }

            var itemsControl = _contentCanvas.Children.OfType<System.Windows.Controls.ItemsControl>().FirstOrDefault();
            // 以屏幕像素为准的对齐触发范围，检测时将距离转换为屏幕尺度再比较
            var tolerancePx = AlignmentTolerance;
            const double linePadding = 12;

            // 🔧 只保留最近的一条对齐线（两两对齐）
            double? closestVerticalX = null;
            double closestVerticalDistance = double.MaxValue;
            (double y1, double y2)? closestVerticalExtent = null;

            double? closestHorizontalY = null;
            double closestHorizontalDistance = double.MaxValue;
            (double x1, double x2)? closestHorizontalExtent = null;

            // 多选拖动时，忽略所有已选中的节点作为对齐参考，避免自对齐
            HashSet<object> selectedSet = null;
            if (SelectedItems != null && SelectedItems.Count > 0)
            {
                selectedSet = new HashSet<object>(SelectedItems.Cast<object>());
            }

            bool ShouldIgnore(object item)
            {
                if (ReferenceEquals(item, movingItem))
                    return true;

                if (selectedSet != null && selectedSet.Count > 1 && selectedSet.Contains(item))
                    return true;

                return false;
            }

            // 计算移动节点的对齐参考（左/中/右、上/中/下）
            var movingVerticals = new[]
            {
                movingBounds.Left,
                movingBounds.Left + movingBounds.Width / 2,
                movingBounds.Right
            };
            // 水平对齐使用节点顶部作为参考，满足“Y 等于参考节点 Y”需求
            var movingHorizontals = new[]
            {
                movingBounds.Top
            };

            // 🔧 遍历所有参考节点，找到最近的对齐线
            foreach (var item in ItemsSource)
            {
                if (ShouldIgnore(item))
                    continue;

                var dims = GetItemDimensions(item, itemsControl);
                if (!dims.HasValue)
                    continue;

                var (x, y, width, height) = dims.Value;

                var otherVerticals = new[] { x, x + width / 2, x + width };
                // 使用顶部线进行垂直方向比对，便于将 Y 吸附到参考节点的 Y
                var otherHorizontals = new[] { y };

                // 检查垂直对齐（X坐标）
                foreach (var ov in otherVerticals)
                {
                    foreach (var mv in movingVerticals)
                    {
                        // 使用屏幕像素距离判断触发
                        double distance = Math.Abs(ov - mv) * Math.Max(Scale, 0.01);
                        if (distance <= tolerancePx && distance < closestVerticalDistance)
                        {
                            closestVerticalX = ov;
                            closestVerticalDistance = distance;
                            // 🔧 对齐线范围：在拖动节点和参考节点之间
                            var y1 = Math.Min(movingBounds.Top, y);
                            var y2 = Math.Max(movingBounds.Bottom, y + height);
                            closestVerticalExtent = (y1, y2);
                        }
                    }
                }

                // 检查水平对齐（Y坐标）
                foreach (var oh in otherHorizontals)
                {
                    foreach (var mh in movingHorizontals)
                    {
                        // 使用屏幕像素距离判断触发
                        double distance = Math.Abs(oh - mh) * Math.Max(Scale, 0.01);
                        if (distance <= tolerancePx && distance < closestHorizontalDistance)
                        {
                            closestHorizontalY = oh;
                            closestHorizontalDistance = distance;
                            // 🔧 对齐线范围：在拖动节点和参考节点之间
                            var x1 = Math.Min(movingBounds.Left, x);
                            var x2 = Math.Max(movingBounds.Right, x + width);
                            closestHorizontalExtent = (x1, x2);
                        }
                    }
                }
            }

            // 🔧 构建对齐线列表（最多只有一条垂直线和一条水平线）
            var verticals = new List<double>();
            var horizontals = new List<double>();
            var verticalExtents = new List<(double x, double y1, double y2)>();
            var horizontalExtents = new List<(double y, double x1, double x2)>();

            if (closestVerticalX.HasValue && closestVerticalExtent.HasValue)
            {
                verticals.Add(closestVerticalX.Value);
                verticalExtents.Add((closestVerticalX.Value, closestVerticalExtent.Value.y1, closestVerticalExtent.Value.y2));
            }

            if (closestHorizontalY.HasValue && closestHorizontalExtent.HasValue)
            {
                horizontals.Add(closestHorizontalY.Value);
                horizontalExtents.Add((closestHorizontalY.Value, closestHorizontalExtent.Value.x1, closestHorizontalExtent.Value.x2));
            }

            if (verticals.Count == 0 && horizontals.Count == 0)
            {
                HideAlignmentLines();
                // 🔧 清空保存的对齐线坐标
                _currentAlignmentVerticals = null;
                _currentAlignmentHorizontals = null;
                return;
            }

            // 🔧 保存当前的对齐线坐标（用于松开鼠标时的精确对齐）
            _currentAlignmentVerticals = new List<double>(verticals);
            _currentAlignmentHorizontals = new List<double>(horizontals);

            ShowAlignmentLines(verticals, horizontals, movingBounds, verticalExtents, horizontalExtents);
        }

        /// <summary>
        /// 计算节点松开后的自动吸附偏移量（使用拖动时显示的对齐线坐标进行精确对齐）
        /// </summary>
        internal (double dx, double dy)? CalculateAlignmentSnap(object movingItem, Rect movingBounds)
        {
            if (!EnableAlignment)
                return null;

            // 🔧 使用拖动时保存的对齐线坐标进行精确对齐
            if (_currentAlignmentVerticals == null && _currentAlignmentHorizontals == null)
                return null;

            var tolerancePx = Math.Max(AlignmentTolerance, 1.0);
            var toleranceCanvas = tolerancePx / Math.Max(Scale, 0.01);

            double? bestDx = null;
            double? bestDy = null;

            // 计算移动节点的对齐参考点（左/中/右、上/中/下）
            var movingVerticals = new[]
            {
                (pos: movingBounds.Left, type: "Left"),
                (pos: movingBounds.Left + movingBounds.Width / 2, type: "Center"),
                (pos: movingBounds.Right, type: "Right")
            };
            // 水平对齐使用节点顶部作为参考，满足“Y 等于参考节点 Y”需求
            var movingHorizontals = new[]
            {
                (pos: movingBounds.Top, type: "Top")
            };

            // 🔧 垂直对齐线（X坐标对齐）- 找到最近的对齐线
            if (_currentAlignmentVerticals != null && _currentAlignmentVerticals.Count > 0)
            {
                foreach (var alignX in _currentAlignmentVerticals)
                {
                    foreach (var mv in movingVerticals)
                    {
                        double diff = alignX - mv.pos;
                        if (Math.Abs(diff) <= toleranceCanvas &&
                            (bestDx == null || Math.Abs(diff) < Math.Abs(bestDx.Value)))
                        {
                            bestDx = diff;
                        }
                    }
                }
            }

            // 🔧 水平对齐线（Y坐标对齐）- 找到最近的对齐线
            if (_currentAlignmentHorizontals != null && _currentAlignmentHorizontals.Count > 0)
            {
                foreach (var alignY in _currentAlignmentHorizontals)
                {
                    foreach (var mh in movingHorizontals)
                    {
                        double diff = alignY - mh.pos;
                        if (Math.Abs(diff) <= toleranceCanvas &&
                            (bestDy == null || Math.Abs(diff) < Math.Abs(bestDy.Value)))
                        {
                            bestDy = diff;
                        }
                    }
                }
            }

            if (bestDx == null && bestDy == null)
                return null;

            System.Diagnostics.Debug.WriteLine($"[对齐吸附] 偏移: dx={bestDx ?? 0:F2}, dy={bestDy ?? 0:F2}");

            return (bestDx ?? 0, bestDy ?? 0);
        }

        #endregion

        #region 缩略图功能

        /// <summary>
        /// 更新缩略图 - 显示画布上所有内容的缩小版（重构版）
        /// </summary>
        private void UpdateMinimap()
        {
            if (_minimapCanvas == null || !ShowMinimap || _contentCanvas == null || IsMinimapCollapsed) 
                return;

            // 🔧 性能优化：如果正在缩放，跳过小地图更新（缩放结束后统一更新）
            int nodeCount = ItemsSource?.Cast<object>().Count() ?? 0;
            if (nodeCount > PerformanceNodeThreshold && _isZooming)
            {
                return;
            }

            // 拖拽后的一次性抑制：避免拖拽刚结束时定时器/布局触发的重新计算导致跳回
            if (_suppressMinimapUpdateAfterDrag)
            {
                _suppressMinimapUpdateAfterDrag = false;
                return;
            }

            // 拖动视口指示器期间，禁止更新小地图（避免 contentBounds/scale 被重新计算导致位置跳动）
            if (_isDraggingViewportIndicator)
            {
                return;
            }

            // 如果当前不需要重算（内容无变化），直接返回，避免重新计算边界导致指示器跳回
            if (!_minimapNeedsRecalc)
            {
                return;
            }

            _minimapCanvas.Children.Clear();

            // 重新添加视口指示器（因为 Clear() 会将其移除）
            RestoreViewportIndicatorToMinimap();

            // 计算画布内容的边界
            var contentBounds = GetContentBounds();

            // 处理空内容：使用当前视口为参考，确保 _minimapContentBounds/_minimapScale 有值
            if (!MinimapAutoFit || contentBounds.IsEmpty)
            {
                // 使用当前视口区域作为小地图参考
                var viewportWidth = Math.Max(ActualWidth / Math.Max(Scale, 0.0001), 2000);
                var viewportHeight = Math.Max(ActualHeight / Math.Max(Scale, 0.0001), 2000);
                var viewportCenterX = -PanX / Math.Max(Scale, 0.0001) + viewportWidth / 2;
                var viewportCenterY = -PanY / Math.Max(Scale, 0.0001) + viewportHeight / 2;
                
                contentBounds = new Rect(
                    viewportCenterX - viewportWidth,
                    viewportCenterY - viewportHeight,
                    viewportWidth * 2,
                    viewportHeight * 2
                );
            }

            // 若仍为空（极端情况），使用默认大区域
            if (contentBounds.IsEmpty)
            {
                var mainWidth = Math.Max(ActualWidth, 2000);
                var mainHeight = Math.Max(ActualHeight, 2000);
                contentBounds = new Rect(-mainWidth / 2, -mainHeight / 2, mainWidth, mainHeight);
            }

            // 计算缩放比例以适应缩略图
            var minimapWidth = MinimapWidth - 24; // 减去边距
            var minimapHeight = MinimapHeight - 32 - 8; // 减去标题栏和边距
            var scaleX = minimapWidth / Math.Max(contentBounds.Width, 1);
            var scaleY = minimapHeight / Math.Max(contentBounds.Height, 1);
            var minimapScale = Math.Min(scaleX, scaleY);

            // 绘制缩略图内容
            if (ItemsSource != null)
            {
                DrawMinimapContent(contentBounds, minimapScale);
            }

            // 存储缩放信息用于导航
            _minimapScale = minimapScale;
            _minimapContentBounds = contentBounds;

            // 立即更新视口指示器
            UpdateViewportIndicator();

            // 完成一次重算，标记为已处理
            _minimapNeedsRecalc = false;
        }

        /// <summary>
        /// 恢复视口指示器到小地图画布
        /// </summary>
        private void RestoreViewportIndicatorToMinimap()
        {
            if (_viewportIndicator == null || _minimapCanvas == null) return;

            // 确保指示器不在小地图的子元素中
            if (_minimapCanvas.Children.Contains(_viewportIndicator))
            {
                // 已经在了，无需重新添加
                return;
            }

            // 如果指示器还在其他父容器中，先移除
            try
            {
                if (_viewportIndicator.Parent is Panel parent)
                {
                    parent.Children.Remove(_viewportIndicator);
                }
            }
            catch { }

            // 添加到小地图画布（作为最后一个子元素，确保在最上层）
            _minimapCanvas.Children.Add(_viewportIndicator);
            
            // 确保 Z-Index 最高
            Panel.SetZIndex(_viewportIndicator, 1000);
            
            // 确保可以接收鼠标事件
            _viewportIndicator.IsHitTestVisible = true;
        }

        /// <summary>
        /// 绘制小地图内容
        /// </summary>
        private void DrawMinimapContent(Rect contentBounds, double minimapScale)
        {
            var contentBrush = TryFindResource("PrimaryBrush") as Brush ?? Brushes.Blue;
            var itemsControl = _contentCanvas?.Children.OfType<ItemsControl>().FirstOrDefault();

            foreach (var item in ItemsSource)
            {
                var nodeDimensions = GetItemDimensions(item, itemsControl);
                if (!nodeDimensions.HasValue) 
                    continue;

                var (x, y, width, height) = nodeDimensions.Value;

                var minimapRect = new Rectangle
                {
                    Width = Math.Max(width * minimapScale, 2),
                    Height = Math.Max(height * minimapScale, 2),
                    Fill = contentBrush,
                    Opacity = 0.6,
                    IsHitTestVisible = false,
                    RadiusX = 2,
                    RadiusY = 2
                };

                Canvas.SetLeft(minimapRect, (x - contentBounds.Left) * minimapScale);
                Canvas.SetTop(minimapRect, (y - contentBounds.Top) * minimapScale);

                _minimapCanvas.Children.Add(minimapRect);
            }
        }

        /// <summary>
        /// 获取项目的尺寸和位置（统一处理）
        /// 优先使用 UI 容器的实际位置，这样在拖动节点时能获取实时位置
        /// </summary>
        private (double x, double y, double width, double height)? GetItemDimensions(object item, ItemsControl itemsControl)
        {
            double x = 0, y = 0, width = 0, height = 0;
            bool hasDimensions = false;

            // 1. 优先从 UI 容器获取（最准确，包含拖动中的实时位置）
            if (itemsControl != null)
            {
                var container = itemsControl.ItemContainerGenerator.ContainerFromItem(item) as FrameworkElement;
                if (container != null && container.ActualWidth > 0 && container.ActualHeight > 0)
                {
                    // 优先使用 Canvas.Left/Top（拖动时会实时更新这些附加属性）
                    var left = Canvas.GetLeft(container);
                    var top = Canvas.GetTop(container);
                    
                    if (!double.IsNaN(left) && !double.IsNaN(top))
                    {
                        x = left;
                        y = top;
                    }
                    else if (_transformTarget != null)
                    {
                        // 回退：使用 TranslatePoint 获取相对于 transformTarget 的逻辑坐标
                        try
                        {
                            var pos = container.TranslatePoint(new Point(0, 0), _transformTarget);
                            x = pos.X;
                            y = pos.Y;
                        }
                        catch 
                        { 
                            // 如果失败，继续尝试 contentCanvas
                            if (_contentCanvas != null)
                            {
                                try
                                {
                                    var pos = container.TranslatePoint(new Point(0, 0), _contentCanvas);
                                    x = pos.X;
                                    y = pos.Y;
                                }
                                catch { }
                            }
                        }
                    }
                    
                    width = container.ActualWidth;
                    height = container.ActualHeight;
                    hasDimensions = true;
                }
            }

            // 2. 回退到数据模型属性（当容器尚未生成或不可用时）
            if (!hasDimensions)
            {
                if (item is Node node)
                {
                    width = node.Size.IsEmpty ? 220 : node.Size.Width;
                    height = node.Size.IsEmpty ? 40 : node.Size.Height;
                    x = node.Position.X;
                    y = node.Position.Y;
                    hasDimensions = true;
                }
                else if (item is FrameworkElement element)
                {
                    width = element.ActualWidth > 0 ? element.ActualWidth : element.Width;
                    height = element.ActualHeight > 0 ? element.ActualHeight : element.Height;
                    if (double.IsNaN(width)) width = 50;
                    if (double.IsNaN(height)) height = 50;
                    
                    var left = Canvas.GetLeft(element);
                    var top = Canvas.GetTop(element);
                    x = double.IsNaN(left) ? 0 : left;
                    y = double.IsNaN(top) ? 0 : top;
                    hasDimensions = true;
                }
            }

            return hasDimensions ? (x, y, width, height) : ((double, double, double, double)?)null;
        }

        /// <summary>
        /// 更新视口指示器 - 高亮显示当前可见区域
        /// </summary>
        private void UpdateViewportIndicator(bool allowDuringDrag = false)
        {
            // 如果正在拖拽视口指示器，跳过更新（避免拖拽时抖动）
            if (_isDraggingViewportIndicator && !allowDuringDrag)
                return;

            if (_viewportIndicator == null || _minimapCanvas == null || !ShowMinimap || IsMinimapCollapsed)
            {
                if (_viewportIndicator != null)
                    _viewportIndicator.Visibility = Visibility.Collapsed;
                return;
            }

            // 确保画布已布局
            var canvasWidth = _minimapCanvas.ActualWidth;
            var canvasHeight = _minimapCanvas.ActualHeight;

            if (canvasWidth <= 0 || canvasHeight <= 0 || ActualWidth <= 0 || ActualHeight <= 0)
            {
                // 延迟更新，等待布局完成
                Dispatcher.BeginInvoke(new Action(() => UpdateViewportIndicator()), System.Windows.Threading.DispatcherPriority.Loaded);
                return;
            }

            // 如果还没有内容边界，尝试获取
            if (_minimapContentBounds.IsEmpty)
            {
                var bounds = GetContentBounds();
                if (!bounds.IsEmpty)
                {
                    _minimapContentBounds = bounds;
                    // 计算缩放比例
                    var availableWidth = canvasWidth;
                    var availableHeight = canvasHeight;
                    var scaleX = availableWidth / Math.Max(bounds.Width, 1);
                    var scaleY = availableHeight / Math.Max(bounds.Height, 1);
                    _minimapScale = Math.Min(scaleX, scaleY);
                }
                else
                {
                    // 没有内容时，使用默认边界和缩放
                    // 使用主画布的实际尺寸作为参考
                    var mainWidth = Math.Max(ActualWidth, 2000); // 至少2000像素
                    var mainHeight = Math.Max(ActualHeight, 2000);
                    _minimapContentBounds = new Rect(-mainWidth / 2, -mainHeight / 2, mainWidth, mainHeight);

                    // 计算缩放比例，使整个内容区域能显示在缩略图中
                    var availableWidth = canvasWidth;
                    var availableHeight = canvasHeight;
                    var scaleX = availableWidth / mainWidth;
                    var scaleY = availableHeight / mainHeight;
                    _minimapScale = Math.Min(scaleX, scaleY);
                }
            }
            
            // 🔧 如果视口在初始位置（PanX/PanY 接近 0），将视口指示器放在小地图中心
            const double initialTolerance = 1.0; // 容差
            bool isInitialPosition = Math.Abs(PanX) < initialTolerance && Math.Abs(PanY) < initialTolerance;

            if (_minimapScale <= 0 || double.IsNaN(_minimapScale) || double.IsInfinity(_minimapScale))
            {
                _viewportIndicator.Visibility = Visibility.Collapsed;
                return;
            }

            // 🔧 视口指示器使用固定大小（横着的长方形），不随画布缩放变化
            const double fixedIndicatorWidth = 60.0;  // 固定宽度
            const double fixedIndicatorHeight = 40.0; // 固定高度
            
            double minimapCenterX, minimapCenterY;
            
            // 🔧 如果视口在初始位置，将视口指示器放在小地图中心
            if (isInitialPosition)
            {
                minimapCenterX = canvasWidth / 2;
                minimapCenterY = canvasHeight / 2;
            }
            else
            {
                // 计算当前视口在画布坐标系中的位置和大小
                // 视口在画布坐标系中的位置 = (屏幕坐标 - 平移) / 缩放
                // 【关键修复】使用 ScaleTransform 的实际值而不是依赖属性
                var currentScale = _scaleTransform?.ScaleX ?? Scale;
                if (currentScale <= 0 || double.IsNaN(currentScale) || double.IsInfinity(currentScale))
                {
                    currentScale = 1.0; // 回退到默认值
                }
                
                var viewportLeft = -PanX / currentScale;
                var viewportTop = -PanY / currentScale;
                var viewportWidth = ActualWidth / currentScale;
                var viewportHeight = ActualHeight / currentScale;
                
                // 计算视口中心在小地图中的位置
                var contentBounds = _minimapContentBounds;
                var viewportCenterX = viewportLeft + viewportWidth / 2;
                var viewportCenterY = viewportTop + viewportHeight / 2;
                minimapCenterX = (viewportCenterX - contentBounds.Left) * _minimapScale;
                minimapCenterY = (viewportCenterY - contentBounds.Top) * _minimapScale;
            }
            
            // 以中心为基准计算左上角位置
            var minimapWidth = fixedIndicatorWidth;
            var minimapHeight = fixedIndicatorHeight;
            var minimapLeft = minimapCenterX - minimapWidth / 2;
            var minimapTop = minimapCenterY - minimapHeight / 2;

            // 确保指示器在缩略图范围内
            // 限制在画布范围内
            minimapLeft = Math.Max(0, Math.Min(minimapLeft, canvasWidth - minimapWidth));
            minimapTop = Math.Max(0, Math.Min(minimapTop, canvasHeight - minimapHeight));

            // 确保值有效
            if (double.IsNaN(minimapLeft) || double.IsNaN(minimapTop) ||
                double.IsNaN(minimapWidth) || double.IsNaN(minimapHeight) ||
                minimapWidth <= 0 || minimapHeight <= 0)
            {
                _viewportIndicator.Visibility = Visibility.Collapsed;
                return;
            }

            // 设置指示器位置和大小
            Canvas.SetLeft(_viewportIndicator, minimapLeft);
            Canvas.SetTop(_viewportIndicator, minimapTop);
            _viewportIndicator.Width = minimapWidth;
            _viewportIndicator.Height = minimapHeight;

            // 确保指示器可见（最后设置，确保所有属性都已设置）
            _viewportIndicator.Visibility = Visibility.Visible;
        }

        /// <summary>
        /// 获取画布内容的边界
        /// </summary>
        private Rect GetContentBounds()
        {
            if (_contentCanvas == null || ItemsSource == null) return Rect.Empty;

            var minX = double.MaxValue;
            var minY = double.MaxValue;
            var maxX = double.MinValue;
            var maxY = double.MinValue;
            bool hasContent = false;

            foreach (var item in ItemsSource)
            {
                double x = 0, y = 0, width = 0, height = 0;

                if (item is FrameworkElement element)
                {
                    x = Canvas.GetLeft(element);
                    y = Canvas.GetTop(element);
                    if (double.IsNaN(x)) x = 0;
                    if (double.IsNaN(y)) y = 0;
                    width = element.ActualWidth;
                    height = element.ActualHeight;
                }
                else if (item is DependencyObject depObj)
                {
                    var xProp = depObj.GetType().GetProperty("X");
                    var yProp = depObj.GetType().GetProperty("Y");
                    var wProp = depObj.GetType().GetProperty("Width");
                    var hProp = depObj.GetType().GetProperty("Height");

                    if (xProp != null) x = Convert.ToDouble(xProp.GetValue(depObj) ?? 0);
                    if (yProp != null) y = Convert.ToDouble(yProp.GetValue(depObj) ?? 0);
                    if (wProp != null) width = Convert.ToDouble(wProp.GetValue(depObj) ?? 0);
                    if (hProp != null) height = Convert.ToDouble(hProp.GetValue(depObj) ?? 0);
                }

                if (width > 0 && height > 0)
                {
                    minX = Math.Min(minX, x);
                    minY = Math.Min(minY, y);
                    maxX = Math.Max(maxX, x + width);
                    maxY = Math.Max(maxY, y + height);
                    hasContent = true;
                }
            }

            if (!hasContent) return Rect.Empty;

            // 添加一些边距
            var margin = 50;
            return new Rect(
                minX - margin,
                minY - margin,
                maxX - minX + 2 * margin,
                maxY - minY + 2 * margin
            );
        }

        /// <summary>
        /// 从缩略图坐标转换为画布坐标并导航（支持边界约束）
        /// </summary>
        /// <summary>
        /// 导航到小地图上的指定点（该点将被放置在主视口中心）
        /// </summary>
        private void NavigateToMinimapPoint(Point minimapPoint)
        {
            if (_minimapContentBounds.IsEmpty || _minimapScale <= 0) return;

            // 将缩略图坐标转换为画布坐标
            var canvasX = minimapPoint.X / _minimapScale + _minimapContentBounds.Left;
            var canvasY = minimapPoint.Y / _minimapScale + _minimapContentBounds.Top;

            // 计算新的Pan值，使该点位于视口中心
            var newPanX = -canvasX * Scale + ActualWidth / 2;
            var newPanY = -canvasY * Scale + ActualHeight / 2;

            // 应用边界约束（如果启用）
            if (MinimapBoundaryConstraint)
            {
                var viewportWidth = ActualWidth / Scale;
                var viewportHeight = ActualHeight / Scale;

                // 计算允许的平移范围
                var minPanX = -((_minimapContentBounds.Right - viewportWidth) * Scale);
                var maxPanX = -_minimapContentBounds.Left * Scale;
                var minPanY = -((_minimapContentBounds.Bottom - viewportHeight) * Scale);
                var maxPanY = -_minimapContentBounds.Top * Scale;

                // 如果内容小于视口，居中显示
                if (_minimapContentBounds.Width <= viewportWidth)
                {
                    newPanX = -(_minimapContentBounds.Left + _minimapContentBounds.Width / 2) * Scale + ActualWidth / 2;
                }
                else
                {
                    newPanX = Math.Max(minPanX, Math.Min(maxPanX, newPanX));
                }

                if (_minimapContentBounds.Height <= viewportHeight)
                {
                    newPanY = -(_minimapContentBounds.Top + _minimapContentBounds.Height / 2) * Scale + ActualHeight / 2;
                }
                else
                {
                    newPanY = Math.Max(minPanY, Math.Min(maxPanY, newPanY));
                }
            }

            PanX = newPanX;
            PanY = newPanY;
            
            // 🔧 更新框选框位置（点击小地图跳转后确保位置正确）
            UpdateSelectedGroupBox();
        }

        private double _minimapScale = 1.0;
        private Rect _minimapContentBounds = Rect.Empty;

        #endregion

        #region 辅助方法

        /// <summary>
        /// 判断 child 是否为 parent 的后代（包含自己）
        /// </summary>
        private bool IsDescendant(DependencyObject parent, DependencyObject child)
        {
            if (parent == null || child == null) return false;
            if (ReferenceEquals(parent, child)) return true;

            var current = VisualTreeHelper.GetParent(child);
            while (current != null)
            {
                if (ReferenceEquals(current, parent))
                    return true;
                current = VisualTreeHelper.GetParent(current);
            }
            return false;
        }

        #endregion

        #region 视口指示器缩放

        /// <summary>
        /// 视口指示器滚轮 - 缩放主画布
        /// </summary>
        private void OnViewportIndicatorMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (!ShowMinimap || !EnableZoom) return;

            // 在视口指示器上滚轮缩放，以当前视口中心为缩放中心
            var zoomFactor = e.Delta > 0 ? 1.15 : 0.85;
            
            // 使用主画布中心作为缩放中心点
            var centerPoint = new Point(ActualWidth / 2, ActualHeight / 2);
            ZoomToPoint(centerPoint, zoomFactor);
            
            e.Handled = true;
        }

        #endregion

        #region 节点集合事件处理

        /// <summary>
        /// ItemsSource 集合变化时的回调（添加/删除节点时触发）
        /// </summary>
        private void OnItemsCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            // 集合变化需要允许小地图刷新
            _suppressMinimapUpdateAfterDrag = false;
            _minimapNeedsRecalc = true;

            // 为新添加的节点订阅属性变化事件（监听 Position/Size 变化）
            if (e.Action == NotifyCollectionChangedAction.Add && e.NewItems != null)
            {
                foreach (var item in e.NewItems)
                {
                    if (item is System.ComponentModel.INotifyPropertyChanged notifyItem)
                    {
                        notifyItem.PropertyChanged += OnNodePropertyChanged;
                    }
                }
            }

            // 取消订阅移除节点的事件
            if (e.Action == NotifyCollectionChangedAction.Remove && e.OldItems != null)
            {
                foreach (var item in e.OldItems)
                {
                    if (item is System.ComponentModel.INotifyPropertyChanged notifyItem)
                    {
                        notifyItem.PropertyChanged -= OnNodePropertyChanged;
                    }
                }
            }

            // 重置时取消所有订阅
            if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                // 无法获取被移除的项，只能在下次添加时重新订阅
            }

            // 更新节点总数
            UpdateTotalItemsCount();

            // 🔧 性能优化：如果正在批量操作，跳过立即更新（批量结束后会统一更新）
            if (_isBatchUpdating)
            {
                System.Diagnostics.Debug.WriteLine("[节点集合变化] 批量操作中，跳过立即更新");
                return;
            }

            // 延迟更新以等待 UI 容器生成完成（ItemContainerGenerator 是异步的）
            // 使用 Loaded 优先级确保布局完成后再更新
            Dispatcher.BeginInvoke(new Action(() =>
            {
                UpdateMinimap();
                UpdateViewportIndicator();
                RefreshEdges();
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        /// <summary>
        /// 节点属性变化时的回调（Position/Size 变化时触发）
        /// </summary>
        private void OnNodePropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            // 只关心影响布局的属性
            if (e.PropertyName == nameof(Node.Position) || 
                e.PropertyName == nameof(Node.Size) ||
                e.PropertyName == "X" || 
                e.PropertyName == "Y" ||
                e.PropertyName == "Width" || 
                e.PropertyName == "Height")
            {
                // 节点变化需要允许小地图刷新
                _suppressMinimapUpdateAfterDrag = false;
                _minimapNeedsRecalc = true;

                // 🔧 性能优化：超过10个节点时，使用节流和延迟更新
                int nodeCount = ItemsSource?.Cast<object>().Count() ?? 0;
                if (nodeCount > PerformanceNodeThreshold)
                {
                    // 使用节流控制：限制更新频率
                    var now = DateTime.Now;
                    if ((now - _lastNodePropertyUpdateTime).TotalMilliseconds < NodePropertyUpdateThrottleMs)
                    {
                        // 在节流期间，启动或重置延迟更新定时器
                        if (_nodePropertyUpdateTimer == null)
                        {
                            _nodePropertyUpdateTimer = new System.Windows.Threading.DispatcherTimer
                            {
                                Interval = TimeSpan.FromMilliseconds(NodePropertyUpdateThrottleMs)
                            };
                            _nodePropertyUpdateTimer.Tick += (s, args) =>
                            {
                                _nodePropertyUpdateTimer.Stop();
                                PerformNodePropertyUpdate();
                            };
                        }
                        
                        if (!_nodePropertyUpdateTimer.IsEnabled)
                        {
                            _nodePropertyUpdateTimer.Start();
                        }
                        else
                        {
                            // 重置定时器
                            _nodePropertyUpdateTimer.Stop();
                            _nodePropertyUpdateTimer.Start();
                        }
                        return;
                    }
                    
                    _lastNodePropertyUpdateTime = now;
                    
                    // 🔧 拖动过程中跳过小地图和视口指示器更新（性能优化）
                    // 只更新连线（使用增量更新+端点调整，实时跟随）
                    // 批量操作时跳过刷新（粘贴、删除等），但智能拖动时允许刷新
                    if (!_isBatchUpdating || _smartEdgeUpdateEnabled)
                    {
                        RefreshEdges(); // 直接调用，不使用Dispatcher（避免延迟）
                    }
                }
                else
                {
                    // 节点数量少时，使用原有逻辑（不节流）
                    UpdateMinimap();
                    UpdateViewportIndicator();
                    
                    if (!_isBatchUpdating || _smartEdgeUpdateEnabled)
                    {
                        RefreshEdges(); // 直接调用，实时更新连线
                    }
                }
            }
        }

        /// <summary>
        /// 执行节点属性更新（延迟批量更新）
        /// </summary>
        private void PerformNodePropertyUpdate()
        {
            _lastNodePropertyUpdateTime = DateTime.Now;
            
            // 只更新连线，跳过小地图和视口指示器（拖动结束后会统一更新）
            if (!_isBatchUpdating || _smartEdgeUpdateEnabled)
            {
                RefreshEdges();
            }
        }

        // ✅ 连线功能已移至 InfiniteCanvas.Connections.cs

        #endregion

        #region 事件处理

        private void InitializeEventHandlers()
        {
            // 🆕 初始化统一的鼠标交互处理
            InitializeUnifiedMouseInteraction();
            
            // ✅ 新的统一事件处理（只使用 Preview 事件）
            PreviewMouseLeftButtonDown += OnUnifiedMouseDown;
            PreviewMouseMove += OnUnifiedMouseMove;
            PreviewMouseLeftButtonUp += OnUnifiedMouseUp;
            PreviewMouseWheel += OnUnifiedMouseWheel;
            
            // ✅ 连线功能的事件处理（保留，使用 AddHandler 确保能捕获）
            // 注意：连线功能需要能捕获已处理的事件，因为可能在节点上点击
            AddHandler(MouseLeftButtonDownEvent, new MouseButtonEventHandler(OnCanvasMouseLeftButtonDown), true);
            AddHandler(MouseMoveEvent, new MouseEventHandler(OnCanvasMouseMove), true);
            AddHandler(MouseLeftButtonUpEvent, new MouseButtonEventHandler(OnCanvasMouseLeftButtonUp), true);

            // 保留键盘和窗口事件
            KeyDown += OnKeyDown;
            KeyUp += OnKeyUp;
            SizeChanged += OnSizeChanged;
            MouseEnter += OnMouseEnter;  // 鼠标进入时获取焦点
        }

        private void OnMouseEnter(object sender, MouseEventArgs e)
        {
            // 鼠标进入画布时获取焦点，确保能接收鼠标滚轮事件
            if (!IsFocused)
            {
                Focus();
            }
        }

        #region 内容画布拖放事件处理（确保事件能正确冒泡）

        /// <summary>
        /// 内容画布拖拽进入事件
        /// </summary>
        private void OnContentCanvasDragEnter(object sender, DragEventArgs e)
        {
            if (e.Data != null && e.Data.GetFormats().Any(f => f.Contains("ToolItem") || f.Contains("FlowEditor")))
            {
                e.Effects = DragDropEffects.Copy;
            }
        }

        /// <summary>
        /// 内容画布拖拽经过事件
        /// </summary>
        private void OnContentCanvasDragOver(object sender, DragEventArgs e)
        {
            if (e.Data != null && e.Data.GetFormats().Any(f => f.Contains("ToolItem") || f.Contains("FlowEditor")))
            {
                e.Effects = DragDropEffects.Copy;
            }
        }

        #endregion

        // 注意：连线相关的所有方法已移至 InfiniteCanvas.Connections.cs
        // 注意：事件处理方法已在上面定义（1775行），此处删除重复定义
        // 注意：OnMouseWheel 已移除，现在使用 OnUnifiedMouseWheel（在 MouseInteraction.cs 中）

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Space && !_state.IsPanning)
                Cursor = Cursors.Hand;

            // Delete 键删除选中项
            if (e.Key == Key.Delete)
            {
                DeleteSelectedItems();
                e.Handled = true;
            }

            // Ctrl+Z 撤销
            if (e.Key == Key.Z && Keyboard.Modifiers == ModifierKeys.Control)
            {
                if (_undoRedoManager != null && _undoRedoManager.CanUndo)
                {
                    _undoRedoManager.Undo();
                    RefreshEdgesImmediate(); // 刷新连线显示
                    e.Handled = true;
                }
            }

            // Ctrl+Y 或 Ctrl+Shift+Z 重做
            if ((e.Key == Key.Y && Keyboard.Modifiers == ModifierKeys.Control) ||
                (e.Key == Key.Z && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift)))
            {
                if (_undoRedoManager != null && _undoRedoManager.CanRedo)
                {
                    _undoRedoManager.Redo();
                    RefreshEdgesImmediate(); // 刷新连线显示
                    e.Handled = true;
                }
            }

            // Ctrl+0 适应画布
            if (e.Key == Key.D0 && Keyboard.Modifiers == ModifierKeys.Control)
            {
                FitToScreen();
                e.Handled = true;
            }
        }

        private void OnKeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Space && !_state.IsPanning)
                Cursor = Cursors.Arrow;
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            // 重置节流时间，确保尺寸改变时一定会更新网格
            _lastGridUpdateTime = DateTime.MinValue;
            UpdateGrid();
            UpdateViewportIndicator();
        }

        #endregion

        #region 属性变化回调

        private static void OnScaleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var canvas = (InfiniteCanvas)d;
            if (canvas._scaleTransform != null)
            {
                canvas._scaleTransform.ScaleX = (double)e.NewValue;
                canvas._scaleTransform.ScaleY = (double)e.NewValue;
            }

            // 🔧 性能优化：如果正在缩放过程中，跳过所有UI更新（缩放结束后统一更新）
            int nodeCount = canvas.ItemsSource?.Cast<object>().Count() ?? 0;
            if (nodeCount > PerformanceNodeThreshold && canvas._isZooming)
            {
                // 缩放过程中完全跳过更新，等缩放结束后统一更新
                return;
            }

            // 🔧 性能优化：节点数量多时，使用节流更新网格
            if (nodeCount > PerformanceNodeThreshold)
            {
                var now = DateTime.Now;
                if ((now - canvas._lastGridUpdateTime).TotalMilliseconds >= GridUpdateThrottleMs)
                {
                    canvas._lastGridUpdateTime = now;
                    canvas.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        canvas.UpdateGrid();
                        canvas.UpdateViewportIndicator();
                        canvas.UpdateSelectedGroupBox();
                    }), System.Windows.Threading.DispatcherPriority.Render);
                }
            }
            else
            {
                // 节点数量少时，不节流
                canvas._lastGridUpdateTime = DateTime.MinValue;
                canvas.Dispatcher.BeginInvoke(new Action(() =>
                {
                    canvas.UpdateGrid();
                    canvas.UpdateViewportIndicator();
                    canvas.UpdateSelectedGroupBox();
                }), System.Windows.Threading.DispatcherPriority.Render);
            }

            canvas.RaiseViewTransformChanged();
        }

        private static void OnTransformChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var canvas = (InfiniteCanvas)d;

            // 变换已经在 OnPreviewMouseMove 中直接更新，这里只处理其他情况
            // 🔒 如果正在拖动视口指示器，跳过更新（避免排队的异步调用在拖动结束后执行导致跳回）
            if (canvas._translateTransform != null && !canvas._state.IsPanning && !canvas._isDraggingViewportIndicator)
            {
                if (e.Property == PanXProperty)
                    canvas._translateTransform.X = (double)e.NewValue;
                else if (e.Property == PanYProperty)
                    canvas._translateTransform.Y = (double)e.NewValue;

                // 重置节流时间，确保平移改变时一定会更新网格
                canvas._lastGridUpdateTime = DateTime.MinValue;

                // 只在非拖动时更新网格和视口指示器
                canvas.Dispatcher.BeginInvoke(new Action(() =>
                {
                    canvas.UpdateGrid();
                    canvas.UpdateViewportIndicator();
                    canvas.UpdateSelectedGroupBox();
                }), System.Windows.Threading.DispatcherPriority.Render);
            }
            // 如果正在拖动画布或视口指示器，只在拖动结束时更新（在 OnPreviewMouseUp 或 OnViewportIndicatorDragCompleted 中处理）

            canvas.RaiseViewTransformChanged();
        }

        private static void OnGridSettingsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((InfiniteCanvas)d).UpdateGrid();
        }

        private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var canvas = (InfiniteCanvas)d;

            // 取消订阅旧集合和旧项的事件
            if (canvas._itemsCollectionNotify != null)
            {
                canvas._itemsCollectionNotify.CollectionChanged -= canvas.OnItemsCollectionChanged;
                canvas._itemsCollectionNotify = null;
            }

            // 取消订阅旧集合中所有项的属性变化事件
            if (e.OldValue is IEnumerable oldItems)
            {
                foreach (var item in oldItems)
                {
                    if (item is System.ComponentModel.INotifyPropertyChanged notifyItem)
                    {
                        notifyItem.PropertyChanged -= canvas.OnNodePropertyChanged;
                    }
                }
            }

            // 订阅新集合的事件
            canvas._itemsCollectionNotify = e.NewValue as INotifyCollectionChanged;
            if (canvas._itemsCollectionNotify != null)
            {
                canvas._itemsCollectionNotify.CollectionChanged += canvas.OnItemsCollectionChanged;
            }

            // 订阅新集合中所有现有项的属性变化事件
            if (e.NewValue is IEnumerable newItems)
            {
                foreach (var item in newItems)
                {
                    if (item is System.ComponentModel.INotifyPropertyChanged notifyItem)
                    {
                        notifyItem.PropertyChanged += canvas.OnNodePropertyChanged;
                    }
                }
            }

            // 更新节点总数
            canvas.UpdateTotalItemsCount();

            // 当内容变化时更新缩略图
            canvas.UpdateMinimap();
            canvas.RefreshEdges();
            
            // 强制刷新 ItemsControl 的模板（延迟执行，确保 ItemsControl 已加载）
            canvas.Dispatcher.BeginInvoke(new Action(() =>
            {
                canvas.RefreshItemsControlTemplate();
                
                // 强制 ItemsControl 生成所有容器（通过访问每个项目的容器）
                if (canvas._contentCanvas != null)
                {
                    var itemsControl = canvas._contentCanvas.Children.OfType<ItemsControl>().FirstOrDefault();
                    if (itemsControl != null && canvas.ItemsSource != null)
                    {
                        // 强制生成所有容器
                        foreach (var item in canvas.ItemsSource)
                        {
                            var container = itemsControl.ItemContainerGenerator.ContainerFromItem(item);
                            // ContainerFromItem 如果容器不存在会返回 null，但不会自动生成
                            // 我们需要等待容器生成完成
                        }
                        
                        // 延迟验证并修复容器模板
                        canvas.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            canvas.VerifyAndFixContainerTemplates();
                        }), System.Windows.Threading.DispatcherPriority.Loaded);
                    }
                }
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        // ✅ OnEdgeItemsSourceChanged 已移至 InfiniteCanvas.Connections.cs

        private static void OnItemTemplateSelectorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var canvas = (InfiniteCanvas)d;
            
            // 当 ItemTemplateSelector 变更时，确保 ItemsControl 正确更新
            // 通过 TemplateBinding，ItemsControl 会自动获取新的 ItemTemplateSelector
            // 但为了确保更新，我们需要强制刷新 ItemsControl
            
            // 如果设置了 ItemTemplateSelector，清除 ItemTemplate（让 ItemTemplateSelector 生效）
            if (e.NewValue != null && canvas.ItemTemplate != null)
            {
                canvas.ItemTemplate = null;
                System.Diagnostics.Debug.WriteLine("[InfiniteCanvas] 清除 ItemTemplate，让 ItemTemplateSelector 生效");
            }
            
            // 同时更新内部 ItemsControl 的 ItemTemplate
            if (canvas._contentCanvas != null)
            {
                var itemsControl = canvas._contentCanvas.Children.OfType<TemplateSelectorItemsControl>().FirstOrDefault();
                if (itemsControl != null)
                {
                    if (e.NewValue != null)
                    {
                        // 设置了 ItemTemplateSelector，清除 ItemTemplate
                        itemsControl.ItemTemplate = null;
                        System.Diagnostics.Debug.WriteLine("[InfiniteCanvas] 清除内部 ItemsControl 的 ItemTemplate");
                    }
                    else if (canvas.ItemTemplate != null)
                    {
                        // 没有 ItemTemplateSelector，使用 ItemTemplate
                        itemsControl.ItemTemplate = canvas.ItemTemplate;
                        System.Diagnostics.Debug.WriteLine("[InfiniteCanvas] 设置内部 ItemsControl 的 ItemTemplate");
                    }
                }
            }
            
            // 延迟刷新，确保 ItemsControl 已加载
            canvas.Dispatcher.BeginInvoke(new Action(() =>
            {
                canvas.RefreshItemsControlTemplate();
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }
        
        /// <summary>
        /// 刷新 ItemsControl 的模板（强制重新应用 ItemTemplateSelector）
        /// </summary>
        private void RefreshItemsControlTemplate()
        {
            if (_contentCanvas == null)
                return;
                
            var itemsControl = _contentCanvas.Children.OfType<System.Windows.Controls.ItemsControl>().FirstOrDefault();
            if (itemsControl == null)
                return;
            
            // 如果设置了 ItemTemplateSelector，清除 ItemTemplate（让 ItemTemplateSelector 生效）
            if (ItemTemplateSelector != null && ItemTemplate != null)
            {
                ItemTemplate = null;
                System.Diagnostics.Debug.WriteLine("[InfiniteCanvas] 清除 ItemTemplate，让 ItemTemplateSelector 生效");
            }
            
            // 强制刷新 ItemsControl 的 ItemTemplateSelector
            var currentSelector = ItemTemplateSelector;
            if (currentSelector != null)
            {
                // 确保 ItemsControl 的 ItemTemplate 被清除（让 ItemTemplateSelector 生效）
                if (itemsControl.ItemTemplate != null)
                {
                    itemsControl.ItemTemplate = null;
                    System.Diagnostics.Debug.WriteLine("[InfiniteCanvas] 清除 ItemsControl.ItemTemplate，让 ItemTemplateSelector 生效");
                }
                
                // 设置 ItemTemplateSelector（ItemsControl 会自动为每个 ContentPresenter 设置 ContentTemplateSelector）
                itemsControl.ItemTemplateSelector = currentSelector;
                System.Diagnostics.Debug.WriteLine($"[InfiniteCanvas] ItemTemplateSelector 已设置: {currentSelector.GetType().Name}");
                
                // 强制生成所有容器（如果还没有生成）
                if (ItemsSource != null)
                {
                    // 通过访问每个项目的容器来强制生成
                    foreach (var item in ItemsSource)
                    {
                        var container = itemsControl.ItemContainerGenerator.ContainerFromItem(item);
                        // ContainerFromItem 如果容器不存在会返回 null，但不会自动生成
                        // 我们需要等待容器生成完成
                    }
                    
                    // 等待容器生成完成后再验证
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        if (itemsControl.ItemContainerGenerator.Status == System.Windows.Controls.Primitives.GeneratorStatus.ContainersGenerated)
                        {
                            VerifyAndFixContainerTemplates();
                        }
                        else
                        {
                            // 如果容器还没生成，订阅 StatusChanged 事件
                            EventHandler statusChangedHandler = null;
                            statusChangedHandler = (s, args) =>
                            {
                                if (itemsControl.ItemContainerGenerator.Status == System.Windows.Controls.Primitives.GeneratorStatus.ContainersGenerated)
                                {
                                    itemsControl.ItemContainerGenerator.StatusChanged -= statusChangedHandler;
                                    VerifyAndFixContainerTemplates();
                                }
                            };
                            itemsControl.ItemContainerGenerator.StatusChanged += statusChangedHandler;
                        }
                    }), System.Windows.Threading.DispatcherPriority.Loaded);
                }
            }
        }
        
        /// <summary>
        /// 验证并修复容器的模板（确保模板选择器正确应用）
        /// </summary>
        private void VerifyAndFixContainerTemplates()
        {
            if (_contentCanvas == null || ItemTemplateSelector == null)
                return;
                
            var itemsControl = _contentCanvas.Children.OfType<System.Windows.Controls.ItemsControl>().FirstOrDefault();
            if (itemsControl == null)
                return;
            
            if (itemsControl.ItemContainerGenerator.Status != System.Windows.Controls.Primitives.GeneratorStatus.ContainersGenerated)
                return;
            
            foreach (var item in ItemsSource ?? Enumerable.Empty<object>())
            {
                var container = itemsControl.ItemContainerGenerator.ContainerFromItem(item);
                if (container is ContentPresenter contentPresenter)
                {
                    // 检查模板是否正确
                    var expectedTemplate = ItemTemplateSelector.SelectTemplate(item, contentPresenter);
                    var currentTemplate = contentPresenter.ContentTemplate;
                    
                    // 如果模板不匹配，强制重新应用
                    if (expectedTemplate != null && currentTemplate != expectedTemplate)
                    {
                        // 清除当前模板和选择器
                        contentPresenter.ContentTemplate = null;
                        contentPresenter.ContentTemplateSelector = null;
                        
                        // 重新设置选择器，让 WPF 重新选择模板
                        contentPresenter.ContentTemplateSelector = ItemTemplateSelector;
                        
                        System.Diagnostics.Debug.WriteLine($"[InfiniteCanvas] 修复容器模板: {item?.GetType().Name ?? "null"}, NodeType: {(item as Node)?.NodeType ?? "null"}, 期望模板: {expectedTemplate?.GetType().Name ?? "null"}, 当前模板: {currentTemplate?.GetType().Name ?? "null"}");
                    }
                }
            }
        }

        private static void OnMinimapCollapsedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var canvas = (InfiniteCanvas)d;

            // 更新可见性状态
            canvas.UpdateMinimapVisibility();

            // 当展开时更新缩略图
            if (!canvas.IsMinimapCollapsed)
            {
                canvas.UpdateMinimap();
            }
        }

        private static void OnMinimapSettingsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var canvas = (InfiniteCanvas)d;
            canvas.UpdateMinimap();
            canvas.UpdateViewportIndicator();
        }

        /// <summary>
        /// 更新缩略图相关控件的可见性
        /// </summary>
        private void UpdateMinimapVisibility()
        {
            if (!ShowMinimap)
            {
                if (_minimapContainer != null) _minimapContainer.Visibility = Visibility.Collapsed;
                if (_minimapCollapseButton != null) _minimapCollapseButton.Visibility = Visibility.Collapsed;
                if (_minimapExpandButton != null) _minimapExpandButton.Visibility = Visibility.Collapsed;
                return;
            }

            if (IsMinimapCollapsed)
            {
                // 折叠状态：隐藏容器（包含画布），隐藏折叠按钮，显示展开按钮
                if (_minimapContainer != null) _minimapContainer.Visibility = Visibility.Collapsed;
                if (_minimapCollapseButton != null) _minimapCollapseButton.Visibility = Visibility.Collapsed;
                if (_minimapExpandButton != null) _minimapExpandButton.Visibility = Visibility.Visible;
            }
            else
            {
                // 展开状态：显示容器，显示折叠按钮，隐藏展开按钮
                if (_minimapContainer != null) 
                {
                    _minimapContainer.Visibility = Visibility.Visible;
                }
                if (_minimapCollapseButton != null) _minimapCollapseButton.Visibility = Visibility.Visible;
                if (_minimapExpandButton != null) _minimapExpandButton.Visibility = Visibility.Collapsed;
            }
        }

        private void RaiseViewTransformChanged()
        {
            RaiseEvent(new RoutedEventArgs(ViewTransformChangedEvent));
        }

        #endregion

        #region 框选功能

        /// <summary>
        /// 判断是否点击在画布背景上（不是任何子控件）
        /// 使用更精确的命中测试
        /// </summary>
        private bool IsClickOnCanvasBackground(DependencyObject hitElement)
        {
            if (hitElement == null)
                return false;

            // 检查是否点击在 NodeControl 或其他交互控件上
            var current = hitElement;
            while (current != null && current != this)
            {
                // 如果是这些类型的控件，说明不是空白区域
                if (current is NodeControl ||
                    current is System.Windows.Controls.TextBox ||
                    current is System.Windows.Controls.Primitives.ButtonBase)
                {
                    return false;
                }

                // 检查是否是 ContentPresenter（节点的容器）
                if (current is ContentPresenter)
                {
                    return false;
                }

                current = VisualTreeHelper.GetParent(current);
            }

            // 如果遍历到这里都没有找到交互控件，说明点击的是空白区域
            // 包括：InfiniteCanvas 本身、ContentCanvas、GridLayer、AlignmentLayer、
            // 或者是节点容器 ItemsControl（但不是具体的节点 ContentPresenter）
            return true;
        }

        /// <summary>
        /// 开始框选
        /// </summary>
        private void StartBoxSelection(Point startPoint)
        {
            if (!EnableBoxSelection || _selectionBox == null || IsLocked)
                return;

            // 清除之前的选中状态（除非按住 Ctrl 键）
            if (Keyboard.Modifiers != ModifierKeys.Control)
            {
                ClearSelection();
            }

            _isBoxSelecting = true;

            // 使用屏幕坐标系来绘制框选框（因为 SelectionBox 在最外层 Grid 中）
            _selectionStartPoint = startPoint;

            // 设置框选矩形的初始位置
            if (_selectionBox != null)
            {
                Canvas.SetLeft(_selectionBox, startPoint.X);
                Canvas.SetTop(_selectionBox, startPoint.Y);
                _selectionBox.Width = 0;
                _selectionBox.Height = 0;
                _selectionBox.Visibility = Visibility.Visible;
            }

            // 捕获鼠标（确保能接收到 MouseMove 和 MouseUp 事件）
            CaptureMouse();
        }

        /// <summary>
        /// 更新框选区域
        /// </summary>
        private void UpdateBoxSelection(Point currentPoint)
        {
            if (!_isBoxSelecting || _selectionBox == null)
                return;

            // 使用屏幕坐标系来绘制框选框
            var x = Math.Min(_selectionStartPoint.X, currentPoint.X);
            var y = Math.Min(_selectionStartPoint.Y, currentPoint.Y);
            var width = Math.Abs(currentPoint.X - _selectionStartPoint.X);
            var height = Math.Abs(currentPoint.Y - _selectionStartPoint.Y);

            Canvas.SetLeft(_selectionBox, x);
            Canvas.SetTop(_selectionBox, y);
            _selectionBox.Width = width;
            _selectionBox.Height = height;

            // 将屏幕坐标转换为画布坐标来检测节点
            // 使用更稳定的 Rect 构造方式（x, y, width, height）
            var topLeft = ScreenToCanvas(new Point(x, y));
            var bottomRight = ScreenToCanvas(new Point(x + width, y + height));

            // 确保正确的矩形构造（处理缩放后坐标可能反转的情况）
            var canvasX = Math.Min(topLeft.X, bottomRight.X);
            var canvasY = Math.Min(topLeft.Y, bottomRight.Y);
            var canvasWidth = Math.Abs(bottomRight.X - topLeft.X);
            var canvasHeight = Math.Abs(bottomRight.Y - topLeft.Y);

            var canvasRect = new Rect(canvasX, canvasY, canvasWidth, canvasHeight);

            UpdateSelectedItems(canvasRect);
        }

        /// <summary>
        /// 结束框选
        /// </summary>
        private void EndBoxSelection()
        {
            if (!_isBoxSelecting)
                return;

            _isBoxSelecting = false;

            if (_selectionBox != null)
            {
                _selectionBox.Visibility = Visibility.Collapsed;
            }

            // 释放鼠标捕获
            if (IsMouseCaptured)
            {
                ReleaseMouseCapture();
            }

            // 更新选中范围显示
            UpdateSelectedGroupBox();
        }

        /// <summary>
        /// 根据框选区域更新选中项
        /// </summary>
        private void UpdateSelectedItems(Rect selectionRect)
        {
            if (ItemsSource == null)
                return;

            _selectedItems.Clear();

            // 尽量使用 UI 容器（ContentPresenter + NodeControl）计算命中，
            // 保证与实际界面位置完全一致，而不是依赖数据模型中的 Position/Size
            ItemsControl itemsControl = null;
            if (_contentCanvas != null)
            {
                itemsControl = _contentCanvas.Children
                    .OfType<ItemsControl>()
                    .FirstOrDefault();
            }

            foreach (var item in ItemsSource)
            {
                bool isSelected = false;

                if (itemsControl != null)
                {
                    var container = itemsControl.ItemContainerGenerator.ContainerFromItem(item) as ContentPresenter;
                    if (container != null)
                    {
                        // ContentPresenter 在内容画布坐标系中的位置和大小
                        var left = Canvas.GetLeft(container);
                        var top = Canvas.GetTop(container);
                        if (double.IsNaN(left)) left = 0;
                        if (double.IsNaN(top)) top = 0;

                        var itemRect = new Rect(left, top, container.ActualWidth, container.ActualHeight);
                        isSelected = selectionRect.IntersectsWith(itemRect);

                        // 设置 NodeControl 的 IsSelected（视觉高亮）
                        if (VisualTreeHelper.GetChildrenCount(container) > 0)
                        {
                            if (VisualTreeHelper.GetChild(container, 0) is NodeControl nodeControl)
                            {
                                nodeControl.IsSelected = isSelected;
                            }
                        }
                    }
                }
                else
                {
                    // 回退到旧的几何判断逻辑
                    isSelected = IsItemInSelectionRect(item, selectionRect);
                }

                // 更新数据模型中的 IsSelected（供业务逻辑使用）
                if (item is Astra.Core.Nodes.Models.Node nodeModel)
                {
                    nodeModel.IsSelected = isSelected;
                }

                if (isSelected)
                {
                    _selectedItems.Add(item);
                }
            }

            // 更新 SelectedItems 属性
            if (SelectedItems == null)
            {
                SelectedItems = new ObservableCollection<object>();
            }

            if (SelectedItems != null)
            {
                SelectedItems.Clear();
                foreach (var item in _selectedItems)
                {
                    SelectedItems.Add(item);
                }
            }

            UpdateSelectedGroupBox();
        }

        /// <summary>
        /// 判断项是否在框选区域内
        /// </summary>
        private bool IsItemInSelectionRect(object item, Rect selectionRect)
        {
            if (item == null)
                return false;

            // selectionRect 已经是画布坐标系，不需要转换

            // 如果是 FrameworkElement，直接获取位置
            if (item is FrameworkElement element)
            {
                var left = Canvas.GetLeft(element);
                var top = Canvas.GetTop(element);
                if (double.IsNaN(left)) left = 0;
                if (double.IsNaN(top)) top = 0;

                var itemRect = new Rect(left, top, element.ActualWidth, element.ActualHeight);
                return selectionRect.IntersectsWith(itemRect);
            }
            // 如果是 Node 类型，通过 Position 属性获取位置
            else if (item is Astra.Core.Nodes.Models.Node node)
            {
                if (node.Position != null)
                {
                    // 默认节点大小（如果 Node.Size 为空或无效）
                    var width = node.Size.IsEmpty ? 220 : node.Size.Width;
                    var height = node.Size.IsEmpty ? 40 : node.Size.Height;

                    var itemRect = new Rect(
                        node.Position.X,
                        node.Position.Y,
                        width,
                        height
                    );
                    return selectionRect.IntersectsWith(itemRect);
                }
            }
            // 其他情况，尝试通过反射获取位置
            else
            {
                var itemType = item.GetType();
                var xProp = itemType.GetProperty("X");
                var yProp = itemType.GetProperty("Y");
                var wProp = itemType.GetProperty("Width");
                var hProp = itemType.GetProperty("Height");

                if (xProp != null && yProp != null)
                {
                    var x = Convert.ToDouble(xProp.GetValue(item) ?? 0);
                    var y = Convert.ToDouble(yProp.GetValue(item) ?? 0);
                    var w = wProp != null ? Convert.ToDouble(wProp.GetValue(item) ?? 220) : 220;
                    var h = hProp != null ? Convert.ToDouble(hProp.GetValue(item) ?? 40) : 40;

                    var itemRect = new Rect(x, y, w, h);
                    return selectionRect.IntersectsWith(itemRect);
                }
            }

            return false;
        }

        /// <summary>
        /// 清除所有选中项
        /// </summary>
        public void ClearSelection()
        {
            _selectedItems.Clear();

            // 清除所有节点的选中状态（数据 + 视觉）
            ItemsControl itemsControl = null;
            if (_contentCanvas != null)
            {
                itemsControl = _contentCanvas.Children
                    .OfType<ItemsControl>()
                    .FirstOrDefault();
            }

            if (ItemsSource != null)
            {
                foreach (var item in ItemsSource)
                {
                    if (item is Astra.Core.Nodes.Models.Node nodeModel)
                    {
                        nodeModel.IsSelected = false;
                    }

                    if (itemsControl != null)
                    {
                        var container = itemsControl.ItemContainerGenerator.ContainerFromItem(item) as ContentPresenter;
                        if (container != null && VisualTreeHelper.GetChildrenCount(container) > 0)
                        {
                            var child = VisualTreeHelper.GetChild(container, 0);
                            if (child is NodeControl nodeControl)
                            {
                                nodeControl.IsSelected = false;
                            }
                            else if (child is WorkflowReferenceNodeControl workflowNodeControl)
                            {
                                workflowNodeControl.IsSelected = false;
                            }
                        }
                    }
                }
            }

            if (SelectedItems != null)
            {
                SelectedItems.Clear();
            }
            else
            {
                SelectedItems = new ObservableCollection<object>();
            }

            UpdateSelectedGroupBox();
        }

        /// <summary>
        /// 删除选中的项（使用撤销/重做命令）
        /// 这是删除节点的统一入口，所有删除操作都应该调用此方法
        /// </summary>
        public void DeleteSelectedItems()
        {
            if (ItemsSource == null)
                return;

            // 优先使用框选维护的 _selectedItems；如果为空，则回退到 SelectedItems 集合
            List<object> itemsToDelete;
            if (_selectedItems.Count > 0)
            {
                itemsToDelete = new List<object>(_selectedItems);
            }
            else if (SelectedItems != null && SelectedItems.Count > 0)
            {
                itemsToDelete = new List<object>(SelectedItems.Cast<object>());
            }
            else
            {
                return;
            }

            // 🔧 批量删除时使用批量操作（避免多次刷新连线）
            bool useBatchUpdate = itemsToDelete.Count > 1;
            if (useBatchUpdate)
            {
                System.Diagnostics.Debug.WriteLine($"[批量删除] 开始删除 {itemsToDelete.Count} 个节点");
                BeginBatchUpdate();
            }

            try
            {
            // 使用撤销/重做命令删除节点（同时会删除相关连线）
            if (ItemsSource is IList nodeList && EdgeItemsSource is IList edgeList)
            {
                if (_undoRedoManager != null)
                {
                    var command = new DeleteNodeCommand(nodeList, edgeList, itemsToDelete);
                    // 设置命令的 WorkflowTab
                    var workflowTab = FindWorkflowTab();
                    if (workflowTab != null)
                    {
                        command.WorkflowTab = workflowTab;
                    }
                    _undoRedoManager.Execute(command);
                }
                else
                {
                    // 回退：直接删除（不支持撤销）
                    DeleteItemsDirectly(nodeList, edgeList, itemsToDelete);
                }
            }
            else if (ItemsSource is IList list)
            {
                // 只有节点列表，没有连线列表
                if (_undoRedoManager != null)
                {
                    var command = new DeleteNodeCommand(list, null, itemsToDelete);
                    // 设置命令的 WorkflowTab
                    var workflowTab = FindWorkflowTab();
                    if (workflowTab != null)
                    {
                        command.WorkflowTab = workflowTab;
                    }
                    _undoRedoManager.Execute(command);
                }
                else
                {
                    foreach (var item in itemsToDelete)
                    {
                        list.Remove(item);
                    }
                }
            }
            else
            {
                // 回退到旧逻辑（不支持撤销）
                System.Diagnostics.Debug.WriteLine("警告：ItemsSource 不是 IList，无法使用撤销/重做功能");
                var removeMethod = ItemsSource.GetType().GetMethod("Remove");
                if (removeMethod != null)
                {
                    foreach (var item in itemsToDelete)
                    {
                        try
                        {
                            removeMethod.Invoke(ItemsSource, new[] { item });
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"[删除项时发生错误]: {ex.Message}");
                        }
                    }
                    }
                }
            }
            finally
            {
                if (useBatchUpdate)
                {
                    EndBatchUpdate();
                    System.Diagnostics.Debug.WriteLine($"[批量删除] 完成");
                }
            }

            // 清除选中状态
            ClearSelection();
            
            // 刷新连线显示
            RefreshEdgesImmediate();
        }

        /// <summary>
        /// 直接删除节点和相关连线（不使用撤销/重做）
        /// </summary>
        private void DeleteItemsDirectly(IList nodeList, IList edgeList, List<object> itemsToDelete)
        {
            // 先删除相关连线
            if (edgeList != null)
            {
                var removeIds = new HashSet<string>(itemsToDelete.OfType<Node>().Select(n => n.Id));
                if (removeIds.Count > 0)
                {
                    var edgesToDelete = edgeList
                        .Cast<object>()
                        .OfType<Edge>()
                        .Where(e => removeIds.Contains(e.SourceNodeId) || removeIds.Contains(e.TargetNodeId))
                        .Cast<object>()
                        .ToList();
                    foreach (var edge in edgesToDelete)
                    {
                        edgeList.Remove(edge);
                    }
                }
            }

            // 再删除节点
            foreach (var item in itemsToDelete)
            {
                nodeList.Remove(item);
            }

            UpdateSelectedGroupBox();
        }

        /// <summary>
        /// 更新选中范围高亮框（框选完成后保留）
        /// </summary>
        private void UpdateSelectedGroupBox()
        {
            if (_selectedGroupBox == null)
                return;

            // 框选进行中时不显示（只显示橡皮筋框）
            if (_isBoxSelecting)
            {
                _selectedGroupBox.Visibility = Visibility.Collapsed;
                return;
            }

            // 仅在有多个选中节点时显示组框；单个节点选中时不显示，避免挡住节点自身右键菜单
            if (SelectedItems == null || SelectedItems.Count < 2 || _contentCanvas == null)
            {
                _selectedGroupBox.Visibility = Visibility.Collapsed;
                return;
            }

            var itemsControl = _contentCanvas.Children.OfType<System.Windows.Controls.ItemsControl>().FirstOrDefault();
            if (itemsControl == null)
            {
                _selectedGroupBox.Visibility = Visibility.Collapsed;
                return;
            }

            double minX = double.MaxValue, minY = double.MaxValue;
            double maxX = double.MinValue, maxY = double.MinValue;
            int validCount = 0;

            foreach (var item in SelectedItems)
            {
                var dims = GetItemDimensions(item, itemsControl);
                if (!dims.HasValue) continue;
                validCount++;

                var (x, y, w, h) = dims.Value;
                minX = Math.Min(minX, x);
                minY = Math.Min(minY, y);
                maxX = Math.Max(maxX, x + w);
                maxY = Math.Max(maxY, y + h);
            }

            if (validCount == 0 || double.IsInfinity(minX) || double.IsInfinity(minY) ||
                double.IsInfinity(maxX) || double.IsInfinity(maxY))
            {
                _selectedGroupBox.Visibility = Visibility.Collapsed;
                return;
            }

            // 将画布坐标转换为控件坐标：x' = x * Scale + PanX
            var leftLogical = minX;
            var topLogical = minY;
            var rightLogical = maxX;
            var bottomLogical = maxY;

            var left = leftLogical * Scale + PanX;
            var top = topLogical * Scale + PanY;
            var right = rightLogical * Scale + PanX;
            var bottom = bottomLogical * Scale + PanY;

            // 水平方向统一 padding；垂直方向略微向上扩一点，使视觉上上下间距更接近
            const double paddingX = 6;
            const double paddingY = 6;
            const double topBias = 8; // 顶部额外向上偏移，补偿节点视觉重心偏上的效果

            left  -= paddingX;
            right += paddingX;
            top   -= (paddingY + topBias);
            bottom += paddingY;

            var width = Math.Max(0, right - left);
            var height = Math.Max(0, bottom - top);

            Canvas.SetLeft(_selectedGroupBox, left);
            Canvas.SetTop(_selectedGroupBox, top);
            _selectedGroupBox.Width = width;
            _selectedGroupBox.Height = height;
            _selectedGroupBox.Visibility = Visibility.Visible;
        }

        /// <summary>
        /// 判断点是否落在选中组框内（用于组拖动或右键菜单）
        /// </summary>
        internal bool IsPointInSelectedGroupBox(Point pointInControl)
        {
            if (_selectedGroupBox == null || _selectedGroupBox.Visibility != Visibility.Visible)
                return false;

            try
            {
                var topLeft = _selectedGroupBox.TransformToAncestor(this).Transform(new Point(0, 0));
                var rect = new Rect(topLeft, new Size(_selectedGroupBox.Width, _selectedGroupBox.Height));
                return rect.Contains(pointInControl);
            }
            catch
            {
                return false;
            }
        }

        // 注意：FindPathAStar 方法已移至 InfiniteCanvas.Connections.cs

        #endregion
    }

    #region 内部辅助类

    internal class ViewportState
    {
        public bool IsPanning { get; private set; }
        private Point _startPoint;
        private double _startPanX;
        private double _startPanY;

        public void BeginPanning(Point startPoint, double panX, double panY)
        {
            IsPanning = true;
            _startPoint = startPoint;
            _startPanX = panX;
            _startPanY = panY;
        }

        public (double X, double Y) CalculatePanDelta(Point currentPoint)
        {
            return (
                _startPanX + (currentPoint.X - _startPoint.X),
                _startPanY + (currentPoint.Y - _startPoint.Y)
            );
        }

        public void EndPanning()
        {
            IsPanning = false;
        }
    }

    #endregion
}
