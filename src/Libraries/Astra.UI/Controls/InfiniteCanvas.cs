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
    public class InfiniteCanvas : Control
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
                new PropertyMetadata(56.0, OnGridSettingsChanged));

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
                new PropertyMetadata(20.0));

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
        /// 连线数据源
        /// </summary>
        public static readonly DependencyProperty EdgeItemsSourceProperty =
            DependencyProperty.Register(nameof(EdgeItemsSource), typeof(IEnumerable), typeof(InfiniteCanvas),
                new PropertyMetadata(null, OnEdgeItemsSourceChanged));

        public IEnumerable EdgeItemsSource
        {
            get => (IEnumerable)GetValue(EdgeItemsSourceProperty);
            set => SetValue(EdgeItemsSourceProperty, value);
        }

        /// <summary>
        /// 撤销/重做管理器（由 FlowEditor 注入）
        /// </summary>
        public UndoRedoManager UndoRedoManager
        {
            get => _undoRedoManager;
            set => _undoRedoManager = value;
        }

        public static readonly DependencyProperty ItemTemplateProperty =
            DependencyProperty.Register(nameof(ItemTemplate), typeof(DataTemplate), typeof(InfiniteCanvas),
                new PropertyMetadata(null));

        public DataTemplate ItemTemplate
        {
            get => (DataTemplate)GetValue(ItemTemplateProperty);
            set => SetValue(ItemTemplateProperty, value);
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
                new PropertyMetadata(null));

        public IList SelectedItems
        {
            get => (IList)GetValue(SelectedItemsProperty);
            set => SetValue(SelectedItemsProperty, value);
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
        private bool _isDraggingViewportIndicator;  // 是否正在拖拽视口指示器
        private Point _viewportIndicatorDragStart;  // 视口指示器拖拽起始点（小地图坐标）
        private FrameworkElement _transformTarget; // 专门用于承载缩放/平移变换的视觉元素
        private Canvas _edgeLayer;                // 连线层（在节点下方）
        private Canvas _connectionPreviewLayer;   // 临时连线层
        private Polyline _connectionPreviewLine;  // 临时连线（正交路径）
        private bool _isConnecting;
        private Node _connectionSourceNode;
        private FrameworkElement _connectionSourcePortElement;  // 保存源端口元素，用于获取端口ID
        private Point _connectionStartPoint;
        private INotifyCollectionChanged _edgeCollectionNotify;
        private INotifyCollectionChanged _itemsCollectionNotify;
        private UndoRedoManager _undoRedoManager;
        private FrameworkElement _hoveredPort;  // 当前悬停的端口
        private const double PortSnapDistance = 30.0;  // 端口吸附距离（像素）

        // 框选相关字段
        private bool _isBoxSelecting;
        private Point _selectionStartPoint;
        private Rectangle _selectionBox;
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

        #endregion

        #region 模板应用

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            System.Diagnostics.Debug.WriteLine("=== [InfiniteCanvas] OnApplyTemplate 开始 ===");

            _contentCanvas = GetTemplateChild(PART_ContentCanvas) as Canvas;
            _gridLayer = GetTemplateChild(PART_GridLayer) as Canvas;
            _alignmentLayer = GetTemplateChild(PART_AlignmentLayer) as Canvas;
            _minimapContainer = GetTemplateChild(PART_MinimapContainer) as Border;
            _minimapCanvas = GetTemplateChild(PART_MinimapCanvas) as Canvas;
            _viewportIndicator = GetTemplateChild(PART_ViewportIndicator) as System.Windows.Controls.Primitives.Thumb;
            _minimapCollapseButton = GetTemplateChild(PART_MinimapCollapseButton) as Button;
            _minimapExpandButton = GetTemplateChild(PART_MinimapExpandButton) as Button;
            _minimapFitButton = GetTemplateChild(PART_MinimapFitButton) as Button;

            System.Diagnostics.Debug.WriteLine($"[InfiniteCanvas] 模板控件获取结果:");
            System.Diagnostics.Debug.WriteLine($"  - _contentCanvas: {_contentCanvas != null}");
            System.Diagnostics.Debug.WriteLine($"  - _minimapContainer: {_minimapContainer != null}");
            System.Diagnostics.Debug.WriteLine($"  - _minimapCanvas: {_minimapCanvas != null}");
            System.Diagnostics.Debug.WriteLine($"  - _viewportIndicator: {_viewportIndicator != null}");
            System.Diagnostics.Debug.WriteLine($"  - ShowMinimap: {ShowMinimap}");
            System.Diagnostics.Debug.WriteLine($"  - IsMinimapCollapsed: {IsMinimapCollapsed}");

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

            // 启用拖放功能
            AllowDrop = true;
            IsHitTestVisible = true;

            // 订阅拖放事件（Preview 事件优先）
            PreviewDragEnter += OnInfiniteCanvasDragEnter;
            PreviewDragOver += OnInfiniteCanvasDragOver;
            PreviewDragLeave += OnInfiniteCanvasDragLeave;
            PreviewDrop += OnInfiniteCanvasDrop;

            DragEnter += OnInfiniteCanvasDragEnter;
            DragOver += OnInfiniteCanvasDragOver;
            DragLeave += OnInfiniteCanvasDragLeave;
            Drop += OnInfiniteCanvasDrop;

            if (_contentCanvas != null)
            {
                // 确保内容画布启用拖放
                _contentCanvas.AllowDrop = true;

                EnsureEdgeLayer();

                // 锁定真正承载缩放/平移的目标（只对内容做变换，不缩放命中区域）
                ResolveTransformTarget();

                // 订阅拖放事件（Preview 和普通事件）
                _contentCanvas.PreviewDragOver += OnContentCanvasDragOver;
                _contentCanvas.PreviewDrop += OnContentCanvasDrop;
                _contentCanvas.PreviewDragEnter += OnContentCanvasDragEnter;
                _contentCanvas.PreviewDragLeave += OnContentCanvasDragLeave;

                _contentCanvas.DragOver += OnContentCanvasDragOver;
                _contentCanvas.Drop += OnContentCanvasDrop;
                _contentCanvas.DragEnter += OnContentCanvasDragEnter;
                _contentCanvas.DragLeave += OnContentCanvasDragLeave;

                InitializeTransforms();
                InitializeEventHandlers();
                InitializeMinimapUpdateTimer();
            }

            if (_minimapCanvas != null && _viewportIndicator != null)
            {
                System.Diagnostics.Debug.WriteLine("[InfiniteCanvas] 准备初始化小地图...");
                InitializeMinimap();
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[InfiniteCanvas] ⚠️ 无法初始化小地图: _minimapCanvas={_minimapCanvas != null}, _viewportIndicator={_viewportIndicator != null}");
            }

            InitializeMinimapButtons();

            System.Diagnostics.Debug.WriteLine("=== [InfiniteCanvas] OnApplyTemplate 完成 ===");

            // 延迟更新网格和缩略图，等待布局完成后再绘制
            Dispatcher.BeginInvoke(new Action(() =>
            {
                UpdateGrid();
                UpdateMinimap();
                UpdateViewportIndicator();
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

            System.Diagnostics.Debug.WriteLine("[小地图] 初始化小地图和视口指示器");

            // 确保视口指示器的基本属性
            _viewportIndicator.IsHitTestVisible = true;
            _viewportIndicator.Focusable = true;
            _viewportIndicator.Cursor = Cursors.Hand;
            Panel.SetZIndex(_viewportIndicator, 1000);
            
            // 诊断：输出视口指示器的属性
            System.Diagnostics.Debug.WriteLine($"[视口指示器] 属性检查:");
            System.Diagnostics.Debug.WriteLine($"  - IsHitTestVisible: {_viewportIndicator.IsHitTestVisible}");
            System.Diagnostics.Debug.WriteLine($"  - Focusable: {_viewportIndicator.Focusable}");
            System.Diagnostics.Debug.WriteLine($"  - IsEnabled: {_viewportIndicator.IsEnabled}");
            System.Diagnostics.Debug.WriteLine($"  - Visibility: {_viewportIndicator.Visibility}");
          
            System.Diagnostics.Debug.WriteLine($"  - Opacity: {_viewportIndicator.Opacity}");
            System.Diagnostics.Debug.WriteLine($"  - Width x Height: {_viewportIndicator.Width} x {_viewportIndicator.Height}");
            System.Diagnostics.Debug.WriteLine($"  - Parent: {_viewportIndicator.Parent?.GetType().Name ?? "null"}");

            // 为小地图容器添加诊断事件
            if (_minimapContainer != null)
            {
                _minimapContainer.PreviewMouseDown += (s, e) => {
                    System.Diagnostics.Debug.WriteLine($"🟦 [小地图容器] PreviewMouseDown - Source: {e.OriginalSource?.GetType().Name}");
                };
                _minimapContainer.MouseDown += (s, e) => {
                    System.Diagnostics.Debug.WriteLine($"🟦 [小地图容器] MouseDown - Source: {e.OriginalSource?.GetType().Name}");
                };
            }

            // 为缩略图画布添加鼠标事件处理（点击空白区域快速跳转）
            _minimapCanvas.MouseEnter += (s, e) => System.Diagnostics.Debug.WriteLine("✨ [小地图] 鼠标进入小地图区域");
            _minimapCanvas.MouseLeave += (s, e) => System.Diagnostics.Debug.WriteLine("⬅️ [小地图] 鼠标离开小地图区域");
            
            // 🔧 使用 AddHandler 强制捕获小地图点击事件，在 Preview 阶段就处理（优先于视口指示器）
            _minimapCanvas.AddHandler(UIElement.PreviewMouseLeftButtonDownEvent, 
                new MouseButtonEventHandler(OnMinimapMouseDown), handledEventsToo: true);
            _minimapCanvas.MouseLeftButtonUp += OnMinimapMouseUp;
            _minimapCanvas.MouseMove += OnMinimapMouseMove;
            
            // MouseLeave 没有 Preview 版本，使用普通事件
            _minimapCanvas.MouseLeave += OnMinimapMouseLeave;

            // 🔒 冗余保护：即使事件被提前标记为 Handled 也要捕获
            _minimapCanvas.AddHandler(UIElement.PreviewMouseLeftButtonDownEvent, new MouseButtonEventHandler((s, e) =>
            {
                System.Diagnostics.Debug.WriteLine($"📍 [小地图画布-AddHandler] PreviewMouseLeftButtonDown - Source: {e.OriginalSource?.GetType().Name}, Handled={e.Handled}");
            }), true);
            _minimapCanvas.AddHandler(UIElement.MouseLeftButtonDownEvent, new MouseButtonEventHandler((s, e) =>
            {
                System.Diagnostics.Debug.WriteLine($"📍 [小地图画布-AddHandler] MouseLeftButtonDown - Source: {e.OriginalSource?.GetType().Name}, Handled={e.Handled}");
            }), true);
            
            // 视口指示器进入/离开事件
            _viewportIndicator.MouseEnter += (s, e) => System.Diagnostics.Debug.WriteLine("🎯 [视口指示器-Thumb] 鼠标进入视口指示器");
            _viewportIndicator.MouseLeave += (s, e) => System.Diagnostics.Debug.WriteLine("⬅️ [视口指示器-Thumb] 鼠标离开视口指示器");
            
            // 使用 Thumb 的专用拖拽事件（保留作为备用）
            _viewportIndicator.DragStarted += OnViewportIndicatorDragStarted;
            _viewportIndicator.DragDelta += OnViewportIndicatorDragDelta;
            _viewportIndicator.DragCompleted += OnViewportIndicatorDragCompleted;
            
            // 🔧 手动实现拖拽：使用 AddHandler 强制捕获所有事件（即使已被标记 Handled）
            _viewportIndicator.AddHandler(UIElement.PreviewMouseLeftButtonDownEvent, 
                new MouseButtonEventHandler(OnViewportIndicatorMouseLeftButtonDown), handledEventsToo: true);
            _viewportIndicator.AddHandler(UIElement.PreviewMouseLeftButtonUpEvent, 
                new MouseButtonEventHandler(OnViewportIndicatorMouseLeftButtonUp), handledEventsToo: true);
            _viewportIndicator.AddHandler(UIElement.PreviewMouseMoveEvent, 
                new MouseEventHandler(OnViewportIndicatorMouseMove), handledEventsToo: true);
            
            // 滚轮缩放
            _viewportIndicator.MouseWheel += OnViewportIndicatorMouseWheel;

            // 测试：添加任意鼠标按下事件
            _viewportIndicator.PreviewMouseDown += (s, e) => {
                System.Diagnostics.Debug.WriteLine($"🔍 [视口指示器-Thumb] PreviewMouseDown - 按钮: {e.ChangedButton}, 状态: {e.ButtonState}");
            };
            _viewportIndicator.MouseDown += (s, e) => {
                System.Diagnostics.Debug.WriteLine($"🔍 [视口指示器-Thumb] MouseDown - 按钮: {e.ChangedButton}, 状态: {e.ButtonState}");
            };

            // 🔒 冗余保护：捕获被标记 Handled 的鼠标事件
            _viewportIndicator.AddHandler(UIElement.PreviewMouseLeftButtonDownEvent, new MouseButtonEventHandler((s, e) =>
            {
                System.Diagnostics.Debug.WriteLine($"🔒 [视口指示器-Thumb] AddHandler PreviewMouseLeftButtonDown - Source: {e.OriginalSource?.GetType().Name}, Handled={e.Handled}");
            }), true);
            _viewportIndicator.AddHandler(UIElement.MouseLeftButtonDownEvent, new MouseButtonEventHandler((s, e) =>
            {
                System.Diagnostics.Debug.WriteLine($"🔒 [视口指示器-Thumb] AddHandler MouseLeftButtonDown - Source: {e.OriginalSource?.GetType().Name}, Handled={e.Handled}");
            }), true);

            System.Diagnostics.Debug.WriteLine("[小地图] 视口指示器事件已绑定（Preview + 普通事件）");

            // 🔴 调试测试：创建一个简单的测试矩形
            var testRect = new Rectangle
            {
                Width = 50,
                Height = 50,
                Fill = Brushes.Red,
                Opacity = 0.5,
                IsHitTestVisible = true,
                Cursor = Cursors.Hand
            };
            Canvas.SetLeft(testRect, 10);
            Canvas.SetTop(testRect, 10);
            Canvas.SetZIndex(testRect, 2000);
            
            testRect.PreviewMouseDown += (s, e) => {
                System.Diagnostics.Debug.WriteLine("🔴 [测试矩形] PreviewMouseDown - 成功！");
            };
            testRect.MouseDown += (s, e) => {
                System.Diagnostics.Debug.WriteLine("🔴 [测试矩形] MouseDown - 成功！");
            };
            
            _minimapCanvas.Children.Add(testRect);
            System.Diagnostics.Debug.WriteLine("🔴 [测试] 红色测试矩形已添加到小地图左上角");
            System.Diagnostics.Debug.WriteLine($"🔴 [测试] 小地图画布子元素数量: {_minimapCanvas.Children.Count}");
            System.Diagnostics.Debug.WriteLine($"🔴 [测试] 小地图画布属性:");
            System.Diagnostics.Debug.WriteLine($"  - ActualWidth x ActualHeight: {_minimapCanvas.ActualWidth} x {_minimapCanvas.ActualHeight}");
            System.Diagnostics.Debug.WriteLine($"  - IsHitTestVisible: {_minimapCanvas.IsHitTestVisible}");
            System.Diagnostics.Debug.WriteLine($"  - IsEnabled: {_minimapCanvas.IsEnabled}");
            System.Diagnostics.Debug.WriteLine($"  - Visibility: {_minimapCanvas.Visibility}");
            
            // 检查小地图容器
            if (_minimapContainer != null)
            {
                System.Diagnostics.Debug.WriteLine($"🔴 [测试] 小地图容器属性:");
                System.Diagnostics.Debug.WriteLine($"  - ActualWidth x ActualHeight: {_minimapContainer.ActualWidth} x {_minimapContainer.ActualHeight}");
                System.Diagnostics.Debug.WriteLine($"  - IsHitTestVisible: {_minimapContainer.IsHitTestVisible}");
                System.Diagnostics.Debug.WriteLine($"  - IsEnabled: {_minimapContainer.IsEnabled}");
                System.Diagnostics.Debug.WriteLine($"  - Visibility: {_minimapContainer.Visibility}");
            }

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

        /// <summary>
        /// 创建连线层和临时连线层（插入到内容画布最前，保证在节点下方）
        /// </summary>
        private void EnsureEdgeLayer()
        {
            if (_contentCanvas == null)
                return;

            if (_edgeLayer == null)
            {
                _edgeLayer = new Canvas
                {
                    IsHitTestVisible = false
                };
                _contentCanvas.Children.Insert(0, _edgeLayer);
            }

            if (_connectionPreviewLayer == null)
            {
                _connectionPreviewLayer = new Canvas
                {
                    IsHitTestVisible = false
                };
                // 放在连线层上方，仍在节点下方
                _contentCanvas.Children.Insert(1, _connectionPreviewLayer);
            }
        }

        #region 坐标转换

        public Point ScreenToCanvas(Point screenPoint)
        {
            return new Point(
                (screenPoint.X - PanX) / Scale,
                (screenPoint.Y - PanY) / Scale
            );
        }

        public Point CanvasToScreen(Point canvasPoint)
        {
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
            Scale = 1.0;
            PanX = 0;
            PanY = 0;
        }

        public void ZoomToPoint(Point screenPoint, double zoomFactor)
        {
            if (!EnableZoom) return;

            var canvasBefore = ScreenToCanvas(screenPoint);
            Scale *= zoomFactor;
            var canvasAfter = ScreenToCanvas(screenPoint);

            PanX += (canvasAfter.X - canvasBefore.X) * Scale;
            PanY += (canvasAfter.Y - canvasBefore.Y) * Scale;
        }

        public void Pan(double deltaX, double deltaY)
        {
            if (!EnablePanning) return;

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

            var spacing = GridSpacing * Scale;

            if (spacing < 5) return;

            // 动态获取网格画刷
            var gridBrush = GridBrush ?? TryFindResource("BorderBrush") as Brush ?? Brushes.LightGray;

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
        public void ShowAlignmentLines(IEnumerable<double> verticalPositions, IEnumerable<double> horizontalPositions)
        {
            if (_alignmentLayer == null || !EnableAlignment) return;

            _alignmentLayer.Children.Clear();

            // ✅ 动态获取对齐线画刷
            var lineBrush = AlignmentLineBrush ?? TryFindResource("SuccessBrush") as Brush ?? Brushes.Green;
            var width = ActualWidth;
            var height = ActualHeight;

            // 绘制垂直对齐线
            if (verticalPositions != null)
            {
                foreach (var x in verticalPositions)
                {
                    var screenX = CanvasToScreen(new Point(x, 0)).X;
                    _alignmentLayer.Children.Add(new Line
                    {
                        X1 = screenX,
                        Y1 = 0,
                        X2 = screenX,
                        Y2 = height,
                        Stroke = lineBrush,
                        StrokeThickness = 1.5,
                        StrokeDashArray = new DoubleCollection { 4, 4 }
                    });
                }
            }

            // 绘制水平对齐线
            if (horizontalPositions != null)
            {
                foreach (var y in horizontalPositions)
                {
                    var screenY = CanvasToScreen(new Point(0, y)).Y;
                    _alignmentLayer.Children.Add(new Line
                    {
                        X1 = 0,
                        Y1 = screenY,
                        X2 = width,
                        Y2 = screenY,
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

            // 拖拽后的一次性抑制：避免拖拽刚结束时定时器/布局触发的重新计算导致跳回
            if (_suppressMinimapUpdateAfterDrag)
            {
                System.Diagnostics.Debug.WriteLine("⏸️ [UpdateMinimap] 拖拽结束后抑制一次更新");
                _suppressMinimapUpdateAfterDrag = false;
                return;
            }

            // 🔒 拖动视口指示器期间，禁止更新小地图（避免 contentBounds/scale 被重新计算导致位置跳动）
            if (_isDraggingViewportIndicator)
            {
                System.Diagnostics.Debug.WriteLine("⏸️ [UpdateMinimap] 拖动期间跳过更新");
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

            if (_minimapScale <= 0 || double.IsNaN(_minimapScale) || double.IsInfinity(_minimapScale))
            {
                _viewportIndicator.Visibility = Visibility.Collapsed;
                return;
            }

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

            // 转换为缩略图坐标系
            var contentBounds = _minimapContentBounds;
            var minimapLeft = (viewportLeft - contentBounds.Left) * _minimapScale;
            var minimapTop = (viewportTop - contentBounds.Top) * _minimapScale;
            var minimapWidth = viewportWidth * _minimapScale;
            var minimapHeight = viewportHeight * _minimapScale;

            // 确保指示器在缩略图范围内
            // 限制在画布范围内
            minimapLeft = Math.Max(0, Math.Min(minimapLeft, canvasWidth));
            minimapTop = Math.Max(0, Math.Min(minimapTop, canvasHeight));
            minimapWidth = Math.Max(8, Math.Min(minimapWidth, canvasWidth - minimapLeft));
            minimapHeight = Math.Max(8, Math.Min(minimapHeight, canvasHeight - minimapTop));

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

            System.Diagnostics.Debug.WriteLine($"🔄 [反向计算] Pan: ({PanX:F2}, {PanY:F2}) → 画布视口: ({viewportLeft:F2}, {viewportTop:F2}) → 小地图位置: ({minimapLeft:F2}, {minimapTop:F2})");

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
        }

        private double _minimapScale = 1.0;
        private Rect _minimapContentBounds = Rect.Empty;

        #endregion

        #region 缩略图事件处理

        private void OnMinimapMouseDown(object sender, MouseButtonEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"🗺️🗺️🗺️ [小地图点击] MouseDown 事件被触发！");
            System.Diagnostics.Debug.WriteLine($"🗺️ OriginalSource: {e.OriginalSource?.GetType().Name}, Source: {e.Source?.GetType().Name}");
            System.Diagnostics.Debug.WriteLine($"🗺️ _viewportIndicator 类型: {_viewportIndicator?.GetType().Name}");
            System.Diagnostics.Debug.WriteLine($"🗺️ 是否点击指示器: {e.OriginalSource == _viewportIndicator}");

            if (!ShowMinimap || _minimapCanvas == null)
            {
                System.Diagnostics.Debug.WriteLine($"🗺️ ❌ 条件检查失败：ShowMinimap={ShowMinimap}, _minimapCanvas={_minimapCanvas != null}");
                return;
            }

            // 如果点击在视口指示器或其子元素上，不处理（让 Thumb 的拖拽事件处理）
            var isClickOnIndicator = IsDescendant(_viewportIndicator, e.OriginalSource as DependencyObject);
            System.Diagnostics.Debug.WriteLine($"🗺️ IsDescendant 判断结果: {isClickOnIndicator}");
            
            if (isClickOnIndicator)
            {
                System.Diagnostics.Debug.WriteLine("🗺️ ⏭️ 检测到点击视口指示器（或子元素），跳过处理，让 Thumb 处理拖拽");
                return; // 不设置 Handled，让 Thumb 控件能接收到事件
            }

            System.Diagnostics.Debug.WriteLine("🗺️✅ 点击空白区域，执行快速跳转！");

            // 点击小地图空白区域，快速跳转
            var point = e.GetPosition(_minimapCanvas);
            System.Diagnostics.Debug.WriteLine($"🗺️ 点击位置: ({point.X:F2}, {point.Y:F2})");
            
            NavigateToMinimapPoint(point);
            _isNavigatingMinimap = true;
            _minimapCanvas.CaptureMouse();
            e.Handled = true;
            
            System.Diagnostics.Debug.WriteLine("🗺️✅ 快速跳转完成！");
        }

        private void OnMinimapMouseMove(object sender, MouseEventArgs e)
        {
            if (!ShowMinimap || _minimapCanvas == null) return;

            // 如果正在拖拽视口指示器，不处理小地图的拖动
            if (_isDraggingViewportIndicator)
            {
                return;
            }

            // 小地图拖动导航
            if (_isNavigatingMinimap && e.LeftButton == MouseButtonState.Pressed)
            {
                var point = e.GetPosition(_minimapCanvas);
                NavigateToMinimapPoint(point);
            }
        }

        private void OnMinimapMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_minimapCanvas != null && _isNavigatingMinimap)
            {
                _isNavigatingMinimap = false;
                _minimapCanvas.ReleaseMouseCapture();
            }
        }

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

        private void OnMinimapMouseLeave(object sender, MouseEventArgs e)
        {
            if (_minimapCanvas != null && _isNavigatingMinimap)
            {
                _isNavigatingMinimap = false;
                _minimapCanvas.ReleaseMouseCapture();
            }
        }

        #endregion

        #region 视口指示器拖拽与缩放（使用 Thumb 控件）

        /// <summary>
        /// 视口指示器拖拽开始（Thumb.DragStarted）
        /// </summary>
        private void OnViewportIndicatorDragStarted(object sender, System.Windows.Controls.Primitives.DragStartedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"✅ [视口指示器-Thumb] DragStarted - 位置: ({e.HorizontalOffset:F2}, {e.VerticalOffset:F2})");
            
            if (!ShowMinimap || _minimapCanvas == null || _viewportIndicator == null)
                return;

            // 拖拽开始，允许后续正常更新
            _suppressMinimapUpdateAfterDrag = false;
            _minimapNeedsRecalc = false; // 拖拽期间不需要重算

            // 确保小地图内容边界和缩放已初始化
            if (_minimapContentBounds.IsEmpty || _minimapScale <= 0)
            {
                UpdateViewportIndicator(allowDuringDrag: true);
            }

            _isDraggingViewportIndicator = true;
            _viewportIndicator.Cursor = Cursors.SizeAll;
            
            // 记录当前视口指示器的位置作为拖拽起始点
            var left = Canvas.GetLeft(_viewportIndicator);
            var top = Canvas.GetTop(_viewportIndicator);
            _viewportIndicatorDragStart = new Point(
                double.IsNaN(left) ? 0 : left,
                double.IsNaN(top) ? 0 : top
            );
        }

        /// <summary>
        /// 视口指示器拖拽移动（Thumb.DragDelta）
        /// </summary>
        private void OnViewportIndicatorDragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"✅ [视口指示器-Thumb] DragDelta - 偏移: ({e.HorizontalChange:F2}, {e.VerticalChange:F2})");

            if (!ShowMinimap || _minimapCanvas == null || _viewportIndicator == null)
                return;

            // 确保小地图内容边界和缩放已初始化
            if (_minimapContentBounds.IsEmpty || _minimapScale <= 0)
            {
                UpdateViewportIndicator(allowDuringDrag: true);
                if (_minimapContentBounds.IsEmpty || _minimapScale <= 0)
                    return; // 仍然无效，放弃本次更新
            }

            // 计算视口指示器的当前位置
            var currentLeft = Canvas.GetLeft(_viewportIndicator);
            var currentTop = Canvas.GetTop(_viewportIndicator);
            if (double.IsNaN(currentLeft)) currentLeft = 0;
            if (double.IsNaN(currentTop)) currentTop = 0;

            // 计算新位置
            var newLeft = currentLeft + e.HorizontalChange;
            var newTop = currentTop + e.VerticalChange;

            // 边界限制
            var canvasWidth = _minimapCanvas.ActualWidth;
            var canvasHeight = _minimapCanvas.ActualHeight;
            var indicatorWidth = _viewportIndicator.Width;
            var indicatorHeight = _viewportIndicator.Height;

            if (MinimapBoundaryConstraint)
            {
                // 严格边界约束
                newLeft = Math.Max(0, Math.Min(newLeft, canvasWidth - indicatorWidth));
                newTop = Math.Max(0, Math.Min(newTop, canvasHeight - indicatorHeight));
            }
            else
            {
                // 无限画布模式
                var minVisible = 20.0;
                newLeft = Math.Max(-indicatorWidth + minVisible, Math.Min(newLeft, canvasWidth - minVisible));
                newTop = Math.Max(-indicatorHeight + minVisible, Math.Min(newTop, canvasHeight - minVisible));
            }

            // 更新指示器视觉位置
            Canvas.SetLeft(_viewportIndicator, newLeft);
            Canvas.SetTop(_viewportIndicator, newTop);

            // 【关键修复】直接从小地图坐标计算主画布的 PanX/PanY
            // 将视口指示器的左上角转换为画布坐标系中的视口左上角
            var viewportLeftInCanvas = newLeft / _minimapScale + _minimapContentBounds.Left;
            var viewportTopInCanvas = newTop / _minimapScale + _minimapContentBounds.Top;

            // 读取当前的 Scale，确保使用 ScaleTransform 的实际值而不是依赖属性
            var currentScale = _scaleTransform?.ScaleX ?? Scale;
            if (currentScale <= 0 || double.IsNaN(currentScale) || double.IsInfinity(currentScale))
            {
                currentScale = 1.0; // 回退到默认值
            }

            System.Diagnostics.Debug.WriteLine($"🔍 [Scale检查] Scale属性: {Scale:F4} | ScaleTransform.ScaleX: {_scaleTransform?.ScaleX:F4} | 使用值: {currentScale:F4}");

            // 根据视口左上角反推 PanX/PanY
            // 因为：viewportLeft = -PanX / Scale，所以：PanX = -viewportLeft * Scale
            var newPanX = -viewportLeftInCanvas * currentScale;
            var newPanY = -viewportTopInCanvas * currentScale;

            System.Diagnostics.Debug.WriteLine($"🔄 [拖拽计算] 小地图位置: ({newLeft:F2}, {newTop:F2}) → 画布视口: ({viewportLeftInCanvas:F2}, {viewportTopInCanvas:F2}) → Pan: ({newPanX:F2}, {newPanY:F2})");

            // 手动更新变换（因为 OnTransformChanged 在拖动期间被跳过）
            if (_translateTransform != null)
            {
                _translateTransform.X = newPanX;
                _translateTransform.Y = newPanY;
            }

            // 同时更新依赖属性（用于绑定和其他逻辑）
            PanX = newPanX;
            PanY = newPanY;
            
            // 拖动过程中不调用 UpdateViewportIndicator，避免反复覆盖导致位置跳动
        }

        /// <summary>
        /// 视口指示器鼠标释放 - 结束拖拽
        /// </summary>
        /// <summary>
        /// 视口指示器拖拽完成（Thumb.DragCompleted）
        /// </summary>
        private void OnViewportIndicatorDragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"✅ [视口指示器-Thumb] DragCompleted - 已取消: {e.Canceled}");

            if (!ShowMinimap || _viewportIndicator == null)
                return;

            // 抑制拖拽结束后的第一轮小地图重算，防止跳回
            _suppressMinimapUpdateAfterDrag = true;
            // 拖拽完成后如无内容变化，不重算小地图
            _minimapNeedsRecalc = false;

            // 在最终同步 Pan 之前保持拖拽状态为 true，避免依赖属性回调插队
            // （OnTransformChanged 在 _isDraggingViewportIndicator 为 true 时会跳过异步更新）
            _isDraggingViewportIndicator = true;

            // 确保小地图边界与缩放有效
            if (_minimapContentBounds.IsEmpty || _minimapScale <= 0)
            {
                UpdateViewportIndicator(allowDuringDrag: true);
            }

            // 读取指示器当前位置（拖拽结束时的最终位置）
            var finalLeft = Canvas.GetLeft(_viewportIndicator);
            var finalTop = Canvas.GetTop(_viewportIndicator);
            if (double.IsNaN(finalLeft)) finalLeft = 0;
            if (double.IsNaN(finalTop)) finalTop = 0;

            // 使用实际的 ScaleTransform 值计算最终 Pan
            var currentScale = _scaleTransform?.ScaleX ?? Scale;
            if (currentScale <= 0 || double.IsNaN(currentScale) || double.IsInfinity(currentScale))
            {
                currentScale = 1.0;
            }

            // 反推最终 PanX/PanY
            var viewportLeftInCanvas = finalLeft / _minimapScale + _minimapContentBounds.Left;
            var viewportTopInCanvas = finalTop / _minimapScale + _minimapContentBounds.Top;
            var finalPanX = -viewportLeftInCanvas * currentScale;
            var finalPanY = -viewportTopInCanvas * currentScale;

            System.Diagnostics.Debug.WriteLine(
                $"🏁 [拖拽完成] 指示器: ({finalLeft:F2}, {finalTop:F2}) → 画布视口: ({viewportLeftInCanvas:F2}, {viewportTopInCanvas:F2}) → Pan: ({finalPanX:F2}, {finalPanY:F2})");

            // 同步到变换与依赖属性
            if (_translateTransform != null)
            {
                _translateTransform.X = finalPanX;
                _translateTransform.Y = finalPanY;
            }
            PanX = finalPanX;
            PanY = finalPanY;

            _isDraggingViewportIndicator = false;
            _viewportIndicator.Cursor = Cursors.Hand;
            
            // 重置节流时间，确保拖动结束后一定会更新网格
            _lastGridUpdateTime = DateTime.MinValue;
            
            // 拖拽结束后，更新网格和视口指示器（不需要更新小地图，因为平移不改变节点位置）
            UpdateGrid();
            UpdateViewportIndicator();
        }

        /// <summary>
        /// 🔧 手动实现：视口指示器鼠标左键按下 - 开始拖拽
        /// </summary>
        private void OnViewportIndicatorMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"🔧🔧🔧 [视口指示器-手动拖拽] PreviewMouseLeftButtonDown 被触发！");
            System.Diagnostics.Debug.WriteLine($"🔧 Sender: {sender?.GetType().Name}, OriginalSource: {e.OriginalSource?.GetType().Name}");
            
            if (!ShowMinimap || _minimapCanvas == null || _viewportIndicator == null)
            {
                System.Diagnostics.Debug.WriteLine($"🔧 条件检查失败：ShowMinimap={ShowMinimap}, _minimapCanvas={_minimapCanvas != null}, _viewportIndicator={_viewportIndicator != null}");
                return;
            }

            System.Diagnostics.Debug.WriteLine($"🔧 准备开始拖拽...");

            // 拖拽开始，允许后续正常更新
            _suppressMinimapUpdateAfterDrag = false;
            _minimapNeedsRecalc = false;

            // 确保小地图内容边界和缩放已初始化
            if (_minimapContentBounds.IsEmpty || _minimapScale <= 0)
            {
                UpdateViewportIndicator(allowDuringDrag: true);
            }

            _isDraggingViewportIndicator = true;
            _viewportIndicator.Cursor = Cursors.SizeAll;
            
            // 记录鼠标在视口指示器上的相对位置
            _viewportIndicatorDragStart = e.GetPosition(_viewportIndicator);
            
            // 捕获鼠标，确保后续鼠标移动都能被捕获
            _viewportIndicator.CaptureMouse();
            
            // 🔥 重要：标记事件已处理，防止传播到 Canvas 的 MouseLeftButtonDown
            e.Handled = true;
            
            System.Diagnostics.Debug.WriteLine($"🔧✅ [视口指示器-手动拖拽] 开始拖拽成功！起始点: ({_viewportIndicatorDragStart.X:F2}, {_viewportIndicatorDragStart.Y:F2})");
        }

        /// <summary>
        /// 🔧 手动实现：视口指示器鼠标移动 - 拖拽中
        /// </summary>
        private void OnViewportIndicatorMouseMove(object sender, MouseEventArgs e)
        {
            if (!_isDraggingViewportIndicator || !ShowMinimap || _minimapCanvas == null || _viewportIndicator == null)
                return;

            // 确保小地图内容边界和缩放已初始化
            if (_minimapContentBounds.IsEmpty || _minimapScale <= 0)
            {
                UpdateViewportIndicator(allowDuringDrag: true);
                if (_minimapContentBounds.IsEmpty || _minimapScale <= 0)
                    return;
            }

            // 获取鼠标在小地图画布上的位置
            var currentMousePos = e.GetPosition(_minimapCanvas);
            
            // 计算视口指示器的新位置（鼠标位置 - 拖拽起始偏移）
            var newLeft = currentMousePos.X - _viewportIndicatorDragStart.X;
            var newTop = currentMousePos.Y - _viewportIndicatorDragStart.Y;

            // 边界限制
            var canvasWidth = _minimapCanvas.ActualWidth;
            var canvasHeight = _minimapCanvas.ActualHeight;
            var indicatorWidth = _viewportIndicator.Width;
            var indicatorHeight = _viewportIndicator.Height;

            if (MinimapBoundaryConstraint)
            {
                // 严格边界约束
                newLeft = Math.Max(0, Math.Min(newLeft, canvasWidth - indicatorWidth));
                newTop = Math.Max(0, Math.Min(newTop, canvasHeight - indicatorHeight));
            }
            else
            {
                // 无限画布模式
                var minVisible = 20.0;
                newLeft = Math.Max(-indicatorWidth + minVisible, Math.Min(newLeft, canvasWidth - minVisible));
                newTop = Math.Max(-indicatorHeight + minVisible, Math.Min(newTop, canvasHeight - minVisible));
            }

            // 更新指示器视觉位置
            Canvas.SetLeft(_viewportIndicator, newLeft);
            Canvas.SetTop(_viewportIndicator, newTop);

            // 将视口指示器的左上角转换为画布坐标系中的视口左上角
            var viewportLeftInCanvas = newLeft / _minimapScale + _minimapContentBounds.Left;
            var viewportTopInCanvas = newTop / _minimapScale + _minimapContentBounds.Top;

            // 读取当前的 Scale
            var currentScale = _scaleTransform?.ScaleX ?? Scale;
            if (currentScale <= 0 || double.IsNaN(currentScale) || double.IsInfinity(currentScale))
            {
                currentScale = 1.0;
            }

            // 根据视口左上角反推 PanX/PanY
            var newPanX = -viewportLeftInCanvas * currentScale;
            var newPanY = -viewportTopInCanvas * currentScale;

            System.Diagnostics.Debug.WriteLine($"🔧 [拖拽计算-手动] 小地图位置: ({newLeft:F2}, {newTop:F2}) → 画布视口: ({viewportLeftInCanvas:F2}, {viewportTopInCanvas:F2}) → Pan: ({newPanX:F2}, {newPanY:F2})");

            // 手动更新变换
            if (_translateTransform != null)
            {
                _translateTransform.X = newPanX;
                _translateTransform.Y = newPanY;
            }

            // 同时更新依赖属性
            PanX = newPanX;
            PanY = newPanY;
        }

        /// <summary>
        /// 🔧 手动实现：视口指示器鼠标左键释放 - 结束拖拽
        /// </summary>
        private void OnViewportIndicatorMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isDraggingViewportIndicator)
                return;

            System.Diagnostics.Debug.WriteLine($"🔧 [视口指示器-手动] MouseLeftButtonUp - 拖拽结束");

            // 抑制拖拽结束后的第一轮小地图重算
            _suppressMinimapUpdateAfterDrag = true;
            _minimapNeedsRecalc = false;

            // 确保小地图边界与缩放有效
            if (_minimapContentBounds.IsEmpty || _minimapScale <= 0)
            {
                UpdateViewportIndicator(allowDuringDrag: true);
            }

            // 读取指示器当前位置
            var finalLeft = Canvas.GetLeft(_viewportIndicator);
            var finalTop = Canvas.GetTop(_viewportIndicator);
            if (double.IsNaN(finalLeft)) finalLeft = 0;
            if (double.IsNaN(finalTop)) finalTop = 0;

            // 使用实际的 ScaleTransform 值计算最终 Pan
            var currentScale = _scaleTransform?.ScaleX ?? Scale;
            if (currentScale <= 0 || double.IsNaN(currentScale) || double.IsInfinity(currentScale))
            {
                currentScale = 1.0;
            }

            // 反推最终 PanX/PanY
            var viewportLeftInCanvas = finalLeft / _minimapScale + _minimapContentBounds.Left;
            var viewportTopInCanvas = finalTop / _minimapScale + _minimapContentBounds.Top;
            var finalPanX = -viewportLeftInCanvas * currentScale;
            var finalPanY = -viewportTopInCanvas * currentScale;

            System.Diagnostics.Debug.WriteLine($"🏁 [拖拽完成-手动] 指示器: ({finalLeft:F2}, {finalTop:F2}) → Pan: ({finalPanX:F2}, {finalPanY:F2})");

            // 同步到变换与依赖属性
            if (_translateTransform != null)
            {
                _translateTransform.X = finalPanX;
                _translateTransform.Y = finalPanY;
            }
            PanX = finalPanX;
            PanY = finalPanY;

            _isDraggingViewportIndicator = false;
            _viewportIndicator.Cursor = Cursors.Hand;
            
            // 释放鼠标捕获
            _viewportIndicator.ReleaseMouseCapture();
            
            // 重置节流时间，确保拖动结束后一定会更新网格
            _lastGridUpdateTime = DateTime.MinValue;
            
            // 拖拽结束后，更新网格和视口指示器
            UpdateGrid();
            UpdateViewportIndicator();
            
            e.Handled = true;
        }

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

        #region 连线绘制与交互

        private void OnEdgeCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            RefreshEdges();
        }

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

                // 延迟更新以避免频繁刷新（使用 Dispatcher 合并多个属性变化）
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    UpdateMinimap();
                    UpdateViewportIndicator();
                    RefreshEdges();
                }), System.Windows.Threading.DispatcherPriority.Background);
            }
        }

        /// <summary>
        /// 刷新连线层
        /// </summary>
        public void RefreshEdges()
        {
            if (_edgeLayer == null)
            {
                System.Diagnostics.Debug.WriteLine("[连线刷新] 连线层为空");
                return;
            }

            _edgeLayer.Children.Clear();

            if (EdgeItemsSource == null || ItemsSource == null)
            {
                System.Diagnostics.Debug.WriteLine($"[连线刷新] EdgeItemsSource: {EdgeItemsSource != null}, ItemsSource: {ItemsSource != null}");
                return;
            }

            System.Diagnostics.Debug.WriteLine($"[连线刷新] 开始刷新，连线数量: {EdgeItemsSource.Cast<object>().Count()}");

            var nodes = ItemsSource.OfType<Node>().ToDictionary(n => n.Id, n => n);
            var primaryBrush = TryFindResource("PrimaryBrush") as Brush ?? Brushes.SteelBlue;
            var selectedBrush = TryFindResource("InfoBrush") as Brush ?? Brushes.DeepSkyBlue;

            // 预先计算所有节点的边界，用于避障
            var nodeBounds = new Dictionary<string, Rect>();
            foreach (var node in nodes.Values)
            {
                nodeBounds[node.Id] = GetNodeBounds(node);
            }

            foreach (var edgeObj in EdgeItemsSource)
            {
                if (edgeObj is not Edge edge)
                    continue;

                if (string.IsNullOrWhiteSpace(edge.SourceNodeId) || string.IsNullOrWhiteSpace(edge.TargetNodeId))
                    continue;

                if (!nodes.TryGetValue(edge.SourceNodeId, out var source) ||
                    !nodes.TryGetValue(edge.TargetNodeId, out var target))
                {
                    continue;
                }

                var points = new PointCollection();

                // 优先使用端口ID查找，如果没有ID则使用坐标作为提示
                var startHint = edge.Points != null && edge.Points.Count > 0
                    ? new Point(edge.Points.First().X, edge.Points.First().Y)
                    : (Point?)null;
                var endHint = edge.Points != null && edge.Points.Count > 0
                    ? new Point(edge.Points.Last().X, edge.Points.Last().Y)
                    : (Point?)null;

                // 使用端口ID查找端口，如果没有ID则回退到hint查找
                var startPort = GetPortPoint(source, edge.SourcePortId, startHint) ?? GetNodeCenter(source);
                var endPort = GetPortPoint(target, edge.TargetPortId, endHint) ?? GetNodeCenter(target);

                // 准备障碍物列表（排除源节点和目标节点）
                // 确保只使用有效的边界
                var obstacles = new List<Rect>();
                foreach (var kvp in nodeBounds)
                {
                    // 排除源和目标节点，且必须是有效的矩形
                    if (kvp.Key != source.Id && kvp.Key != target.Id && !kvp.Value.IsEmpty && kvp.Value.Width > 1 && kvp.Value.Height > 1)
                    {
                        obstacles.Add(kvp.Value);
                    }
                }

                // 调试障碍物信息
                if (obstacles.Count == 0 && nodeBounds.Count > 2)
                {
                    // 仅用于调试，实际可删除
                }
                else
                {
                    // System.Diagnostics.Debug.WriteLine($"[连线刷新] 准备路由 - 源: {source.Id}, 目标: {target.Id}, 障碍物数量: {obstacles.Count}");
                }

                var routed = BuildOrthogonalRoute(startPort, source, endPort, target, obstacles);
                points = new PointCollection(routed);

                // 覆盖 Edge.Points 为最新路径，便于序列化/后续刷新
                edge.Points = routed.Select(p => new Point2D(p.X, p.Y)).ToList();

                var polyline = new Polyline
                {
                    Stroke = edge.IsSelected ? selectedBrush : primaryBrush,
                    StrokeThickness = edge.IsSelected ? 3 : 2,
                    StrokeLineJoin = PenLineJoin.Round,
                    StrokeStartLineCap = PenLineCap.Round,
                    StrokeEndLineCap = PenLineCap.Round,
                    Points = points,
                    Opacity = 0.9,
                    Tag = edge,
                    IsHitTestVisible = false
                };

                // 箭头
                var arrow = BuildArrow(points, edge.IsSelected ? selectedBrush : primaryBrush);

                System.Diagnostics.Debug.WriteLine($"[连线刷新] 添加连线 - 点数: {points.Count}, 起点: ({points[0].X:F2}, {points[0].Y:F2}), 终点: ({points[points.Count - 1].X:F2}, {points[points.Count - 1].Y:F2})");

                _edgeLayer.Children.Add(polyline);
                if (arrow != null)
                {
                    _edgeLayer.Children.Add(arrow);
                }
            }

            System.Diagnostics.Debug.WriteLine($"[连线刷新] 完成刷新，绘制了 {_edgeLayer.Children.Count} 条连线");
        }

        private void OnCanvasMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // 备用手势：Shift + 左键且点在端口上开始连线
            if (Keyboard.Modifiers == ModifierKeys.Shift)
            {
                var port = FindPortFromHit(e.OriginalSource as DependencyObject);
                var nodeControl = FindParentNodeControl(port ?? e.OriginalSource as DependencyObject);
                if (port != null && nodeControl?.DataContext is Node node)
                {
                    BeginConnection(node, port);
                    e.Handled = true;
                }
            }
        }

        private void OnCanvasMouseMove(object sender, MouseEventArgs e)
        {
            if (!_isConnecting || _connectionPreviewLine == null)
                return;

            // 获取逻辑坐标（统一的未缩放/未平移坐标系）
            var canvasPoint = GetLogicalMousePoint(e);

            // 检测附近端口并吸附
            var nearbyPort = FindNearbyPort(canvasPoint);
            if (nearbyPort != null && nearbyPort != _hoveredPort)
            {
                // 切换到新端口，更新预览终点
                _hoveredPort = nearbyPort;
                var portCenter = GetPortCenter(nearbyPort);
                if (!double.IsNaN(portCenter.X) && !double.IsNaN(portCenter.Y))
                {
                    UpdateConnectionPreview(portCenter);
                }
            }
            else if (nearbyPort == null && _hoveredPort != null)
            {
                // 离开端口区域，使用鼠标位置
                _hoveredPort = null;
                UpdateConnectionPreview(canvasPoint);
            }
            else if (nearbyPort == null)
            {
                // 没有靠近端口，使用鼠标位置
                UpdateConnectionPreview(canvasPoint);
            }
        }

        private void OnCanvasMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isConnecting)
                return;

            // 优先使用吸附的端口，如果没有则尝试从命中点查找
            var targetPort = _hoveredPort ?? FindPortFromHit(e.OriginalSource as DependencyObject);

            // 如果还是没有找到端口，尝试在鼠标位置附近查找
            if (targetPort == null)
            {
                var canvasPoint = GetLogicalMousePoint(e);
                System.Diagnostics.Debug.WriteLine($"[连线] 鼠标位置（逻辑坐标）: ({canvasPoint.X:F2}, {canvasPoint.Y:F2}), Scale={Scale:F2}, Pan=({PanX:F2},{PanY:F2})");
                targetPort = FindNearbyPort(canvasPoint);
            }

            var targetControl = FindParentNodeControl(targetPort ?? e.OriginalSource as DependencyObject);
            var targetNode = targetControl?.DataContext as Node;

            System.Diagnostics.Debug.WriteLine($"[连线] 释放鼠标 - 目标端口: {targetPort != null}, 目标节点: {targetNode?.Name ?? "null"}, 源节点: {_connectionSourceNode?.Name ?? "null"}");

            // 检查是否连接到了同一节点
            bool isSameNode = targetNode != null && _connectionSourceNode != null &&
                             (ReferenceEquals(targetNode, _connectionSourceNode) || targetNode.Id == _connectionSourceNode.Id);

            // 允许替换现有连线，所以不再在此处阻止（TryCreateEdge 会处理替换）
            // bool hasExistingEdge = targetNode != null && _connectionSourceNode != null && 
            //                       HasEdgeBetween(_connectionSourceNode.Id, targetNode.Id);

            if (targetPort != null &&
                targetNode != null &&
                _connectionSourceNode != null &&
                !isSameNode)
            {
                var endPoint = GetPortCenter(targetPort);
                System.Diagnostics.Debug.WriteLine($"[连线] 端点坐标 - 起点: ({_connectionStartPoint.X:F2}, {_connectionStartPoint.Y:F2}), 终点: ({endPoint.X:F2}, {endPoint.Y:F2})");

                if (!double.IsNaN(endPoint.X) && !double.IsNaN(endPoint.Y))
                {
                    TryCreateEdge(_connectionSourceNode, targetNode, _connectionStartPoint, endPoint);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("[连线] 警告：端点坐标无效");
                }
            }
            else
            {
                if (isSameNode)
                {
                    System.Diagnostics.Debug.WriteLine($"[连线] 无法创建连线：不能连接到同一节点（{_connectionSourceNode?.Name ?? "未知"}）");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[连线] 无法创建连线 - 目标端口: {targetPort != null}, 目标节点: {targetNode?.Name ?? "null"}, 源节点: {_connectionSourceNode?.Name ?? "null"}");
                }
            }

            StopConnectionPreview();
            _isConnecting = false;
            _connectionSourceNode = null;
            _hoveredPort = null;

            if (IsMouseCaptured)
            {
                ReleaseMouseCapture();
            }
        }

        /// <summary>
        /// 从外部（节点端口）发起连线（必须点在端口上）
        /// </summary>
        public void BeginConnection(Node sourceNode, FrameworkElement sourcePortElement)
        {
            if (sourceNode == null || sourcePortElement == null)
                return;

            var start = GetPortCenter(sourcePortElement);
            if (double.IsNaN(start.X) || double.IsNaN(start.Y))
                return;

            // 确保源端口有稳定的 PortId（若未设置则自动生成）
            if (sourcePortElement is PortControl pc && string.IsNullOrWhiteSpace(pc.PortId))
            {
                pc.PortId = Guid.NewGuid().ToString("N");
                System.Diagnostics.Debug.WriteLine($"[连线] 为源端口自动生成 PortId: {pc.PortId}");
            }

            _isConnecting = true;
            _connectionSourceNode = sourceNode;
            _connectionSourcePortElement = sourcePortElement;  // 保存源端口元素
            _connectionStartPoint = start;
            StartConnectionPreview(_connectionStartPoint);
            CaptureMouse();
        }

        private void StartConnectionPreview(Point start)
        {
            if (_connectionPreviewLayer == null)
                return;

            _connectionPreviewLayer.Children.Clear();
            _connectionPreviewLine = new Polyline
            {
                Stroke = TryFindResource("InfoBrush") as Brush ?? Brushes.DeepSkyBlue,
                StrokeThickness = 2,
                StrokeDashArray = new DoubleCollection { 4, 4 },
                StrokeLineJoin = PenLineJoin.Round,
                Opacity = 0.9,
                Points = new PointCollection { start }
            };
            _connectionPreviewLayer.Children.Add(_connectionPreviewLine);
        }

        private void UpdateConnectionPreview(Point end)
        {
            if (_connectionPreviewLine == null || _connectionSourceNode == null)
                return;

            List<Point> route;

            // 如果鼠标悬停在目标端口上，使用完整的正交路由
            if (_hoveredPort != null)
            {
                var targetControl = FindParentNodeControl(_hoveredPort);
                if (targetControl?.DataContext is Node hoveredNode)
                {
                    var endPort = GetPortCenter(_hoveredPort);
                    route = BuildOrthogonalRoute(_connectionStartPoint, _connectionSourceNode, endPort, hoveredNode);
                }
                else
                {
                    // 悬停端口但找不到节点，使用简化路径
                    route = BuildSimpleOrthogonalPath(_connectionStartPoint, end, _connectionSourceNode);
                }
            }
            else
            {
                // 没有悬停端口，使用简化的L形路径到鼠标位置
                route = BuildSimpleOrthogonalPath(_connectionStartPoint, end, _connectionSourceNode);
            }

            // 更新 Polyline 的点集合
            _connectionPreviewLine.Points = new PointCollection(route);
        }

        /// <summary>
        /// 构建简单的正交路径（用于预览，无目标节点）
        /// </summary>
        private List<Point> BuildSimpleOrthogonalPath(Point start, Point end, Node sourceNode)
        {
            const double margin = 18.0;
            var sourceBounds = GetNodeBounds(sourceNode);

            // 判断起始端口在节点的哪一边
            var sourceSide = GetPortSideByDistance(start, sourceBounds);

            // 计算源外扩点
            var sourceOut = GetExpansionAlongSide(start, sourceBounds, margin, sourceSide);

            // 简单的L形路径：起点 -> 外扩点 -> 转折点 -> 终点
            var route = new List<Point> { start, sourceOut };

            // 根据源端口方向选择转折方式
            if (sourceSide == PortSide.Top || sourceSide == PortSide.Bottom)
            {
                // 垂直方向的端口，先垂直后水平
                route.Add(new Point(sourceOut.X, end.Y));
            }
            else
            {
                // 水平方向的端口，先水平后垂直
                route.Add(new Point(end.X, sourceOut.Y));
            }

            route.Add(end);

            // 去除重复点
            for (int i = route.Count - 2; i >= 0; i--)
            {
                if (IsSamePoint(route[i], route[i + 1]))
                    route.RemoveAt(i + 1);
            }

            return route;
        }

        private void StopConnectionPreview()
        {
            _connectionPreviewLayer?.Children.Clear();
            _connectionPreviewLine = null;
            _hoveredPort = null;
        }

        private void TryCreateEdge(Node source, Node target, Point startPoint, Point endPoint)
        {
            System.Diagnostics.Debug.WriteLine($"[连线] TryCreateEdge - EdgeItemsSource: {EdgeItemsSource != null}, 类型: {EdgeItemsSource?.GetType().Name ?? "null"}");

            // 如果 EdgeItemsSource 为 null，尝试从父级 FlowEditor 获取或自动创建
            if (EdgeItemsSource == null)
            {
                var flowEditor = FindParentFlowEditor(this);
                if (flowEditor != null)
                {
                    System.Diagnostics.Debug.WriteLine("[连线] 从 FlowEditor 获取 EdgeItemsSource");
                    EdgeItemsSource = flowEditor.EdgeItemsSource;

                    // 如果 FlowEditor 的也是 null，自动创建一个新的集合
                    if (flowEditor.EdgeItemsSource == null)
                    {
                        System.Diagnostics.Debug.WriteLine("[连线] FlowEditor 的 EdgeItemsSource 也为 null，自动创建新的集合");
                        var edges = new System.Collections.ObjectModel.ObservableCollection<Edge>();
                        flowEditor.EdgeItemsSource = edges;
                        EdgeItemsSource = edges;
                    }
                }
                else
                {
                    // 如果找不到 FlowEditor，直接创建一个本地集合
                    System.Diagnostics.Debug.WriteLine("[连线] 未找到 FlowEditor，创建本地 EdgeItemsSource 集合");
                    EdgeItemsSource = new System.Collections.ObjectModel.ObservableCollection<Edge>();
                }
            }

            if (EdgeItemsSource is not System.Collections.IList list)
            {
                System.Diagnostics.Debug.WriteLine($"[连线] 错误：EdgeItemsSource 不是 IList 类型，类型: {EdgeItemsSource?.GetType().Name ?? "null"}");
                return;
            }

            // 获取源端口和目标端口的ID
            string sourcePortId = null;
            string targetPortId = null;

            if (_connectionSourcePortElement is PortControl sourcePort)
            {
                sourcePortId = sourcePort.PortId;
                System.Diagnostics.Debug.WriteLine($"[连线] 源端口ID: {sourcePortId ?? "null"}");
            }

            if (_hoveredPort is PortControl targetPort)
            {
                // 确保目标端口有稳定的 PortId（若未设置则自动生成）
                if (string.IsNullOrWhiteSpace(targetPort.PortId))
                {
                    targetPort.PortId = Guid.NewGuid().ToString("N");
                    System.Diagnostics.Debug.WriteLine($"[连线] 为目标端口自动生成 PortId: {targetPort.PortId}");
                }

                targetPortId = targetPort.PortId;
                System.Diagnostics.Debug.WriteLine($"[连线] 目标端口ID: {targetPortId ?? "null"}");
            }

            var edge = new Edge
            {
                SourceNodeId = source.Id,
                TargetNodeId = target.Id,
                SourcePortId = sourcePortId,  // 保存源端口ID
                TargetPortId = targetPortId,  // 保存目标端口ID
                Points = new List<Point2D>
                {
                    new Point2D(startPoint.X, startPoint.Y),
                    new Point2D(endPoint.X, endPoint.Y)
                }
            };

            System.Diagnostics.Debug.WriteLine($"[连线] 创建连线 - 源节点ID: {source.Id}, 目标节点ID: {target.Id}, 源端口: {sourcePortId ?? "无"}, 目标端口: {targetPortId ?? "无"}, 点数: {edge.Points.Count}");

            // 查找已存在的连线（相同源和目标节点，无论方向）
            // 用户要求：如果两个节点之间已经有一条连线了，若再在两条节点上画一条连线，则需要删除之前的连线
            var existingEdges = new List<object>();
            foreach (var item in list)
            {
                if (item is Edge e)
                {
                    // 检查 A->B 或 B->A
                    if ((e.SourceNodeId == source.Id && e.TargetNodeId == target.Id) ||
                        (e.SourceNodeId == target.Id && e.TargetNodeId == source.Id))
                    {
                        existingEdges.Add(e);
                    }
                }
            }

            if (_undoRedoManager != null)
            {
                var commands = new List<IUndoableCommand>();

                // 1. 如果有旧连线，先删除
                if (existingEdges.Count > 0)
                {
                    System.Diagnostics.Debug.WriteLine($"[连线] 发现 {existingEdges.Count} 条现有连线，准备替换");
                    commands.Add(new DeleteEdgeCommand(list, existingEdges));
                }

                // 2. 添加新连线
                commands.Add(new CreateEdgeCommand(list, edge));

                System.Diagnostics.Debug.WriteLine("[连线] 使用组合命令（删除旧连线+创建新连线）");
                _undoRedoManager.Do(new CompositeCommand(commands));
            }
            else
            {
                // 不使用 UndoRedoManager，直接操作
                foreach (var oldEdge in existingEdges)
                {
                    list.Remove(oldEdge);
                }
                System.Diagnostics.Debug.WriteLine("[连线] 直接添加到集合");
                list.Add(edge);
            }

            System.Diagnostics.Debug.WriteLine($"[连线] 连线集合数量: {list.Count}");
            RefreshEdges();
        }

        private bool HasEdgeBetween(string sourceId, string targetId)
        {
            if (EdgeItemsSource == null)
                return false;

            foreach (var edgeObj in EdgeItemsSource)
            {
                if (edgeObj is not Edge edge) continue;

                if (edge.SourceNodeId == sourceId && edge.TargetNodeId == targetId)
                    return true;

                // 视为双向唯一
                if (edge.SourceNodeId == targetId && edge.TargetNodeId == sourceId)
                    return true;
            }
            return false;
        }

        private Point GetNodeCenter(Node node)
        {
            var width = node.Size.IsEmpty ? 220 : node.Size.Width;
            var height = node.Size.IsEmpty ? 40 : node.Size.Height;

            return new Point(
                node.Position.X + width / 2,
                node.Position.Y + height / 2);
        }

        private Point? GetPortPoint(Node node, string portId = null, Point? hint = null)
        {
            if (_contentCanvas == null || node == null)
                return null;

            var itemsControl = _contentCanvas.Children.OfType<ItemsControl>().FirstOrDefault();
            if (itemsControl == null)
                return null;

            var container = itemsControl.ItemContainerGenerator.ContainerFromItem(node) as ContentPresenter;
            if (container == null)
                return null;

            var ports = FindPortsInContainer(container).ToList();
            if (ports.Count == 0)
                return null;

            FrameworkElement port = null;

            // 优先使用端口ID查找
            if (!string.IsNullOrEmpty(portId))
            {
                port = ports.OfType<PortControl>()
                    .FirstOrDefault(p => p.PortId == portId);

                if (port != null)
                {
                    System.Diagnostics.Debug.WriteLine($"[端口查找] 通过ID找到端口: {portId} 在节点 {node.Name}");
                    return GetPortCenter(port);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[端口查找] 未找到ID为 {portId} 的端口在节点 {node.Name}，使用hint或默认端口");
                }
            }

            // 如果没有找到指定ID的端口，使用 hint
            if (hint.HasValue && ports.Count > 0)
            {
                port = ports
                    .OrderBy(p =>
                    {
                        var center = GetPortCenter(p);
                        return (center.X - hint.Value.X) * (center.X - hint.Value.X) +
                               (center.Y - hint.Value.Y) * (center.Y - hint.Value.Y);
                    })
                    .FirstOrDefault();
            }
            else
            {
                port = ports.FirstOrDefault();
            }

            if (port == null)
                return null;

            return GetPortCenter(port);
        }

        private FrameworkElement FindPortFromHit(DependencyObject source)
        {
            var current = source;
            while (current != null)
            {
                if (current is PortControl pc)
                    return pc;
                current = VisualTreeHelper.GetParent(current);
            }
            return null;
        }

        private Point GetPortCenter(FrameworkElement portElement)
        {
            if (portElement == null)
                return new Point(double.NaN, double.NaN);

            // 优先使用基于节点位置的计算（GetPortCenterByNodePosition）
            // 原因：在快速拖动时，Canvas.SetLeft/Top 虽然已更新，但 WPF 的视觉树可能尚未完成 Render Pass，
            // 导致直接 TranslatePoint 到画布根节点会返回上一帧的旧坐标，从而产生连线滞后。
            // 而 node.Position 是实时更新的，相对偏移也是稳定的，因此计算结果更实时。
            var pointByPos = GetPortCenterByNodePosition(portElement);
            if (!double.IsNaN(pointByPos.X) && !double.IsNaN(pointByPos.Y))
            {
                return pointByPos;
            }

            // 获取端口中心在端口内的相对位置
            var portCenter = new Point(portElement.ActualWidth / 2, portElement.ActualHeight / 2);

            // 回退：直接转换到 transformTarget (逻辑坐标系)
            if (_transformTarget != null)
            {
                try
                {
                    return portElement.TranslatePoint(portCenter, _transformTarget);
                }
                catch { }
            }

            // 回退：直接转换到内容画布坐标
            if (_contentCanvas != null)
            {
                try
                {
                    return portElement.TranslatePoint(portCenter, _contentCanvas);
                }
                catch { }
            }

            return new Point(double.NaN, double.NaN);
        }

        /// <summary>
        /// 通过节点位置计算端口中心（备用方法）
        /// </summary>
        private Point GetPortCenterByNodePosition(FrameworkElement portElement)
        {
            // 找到端口所属的节点
            var nodeControl = FindParentNodeControl(portElement);
            if (nodeControl == null || nodeControl.DataContext is not Node node)
                return new Point(double.NaN, double.NaN);

            // 获取节点在画布上的位置
            var itemsControl = _contentCanvas?.Children.OfType<ItemsControl>().FirstOrDefault();
            if (itemsControl == null)
                return new Point(double.NaN, double.NaN);

            var container = itemsControl.ItemContainerGenerator.ContainerFromItem(node) as ContentPresenter;
            if (container == null)
                return new Point(double.NaN, double.NaN);

            var nodeX = Canvas.GetLeft(container);
            var nodeY = Canvas.GetTop(container);
            if (double.IsNaN(nodeX)) nodeX = node.Position.X;
            if (double.IsNaN(nodeY)) nodeY = node.Position.Y;

            // 获取端口相对于节点的位置
            var portCenter = new Point(portElement.ActualWidth / 2, portElement.ActualHeight / 2);
            var portInNode = portElement.TranslatePoint(portCenter, nodeControl);

            // 计算端口在画布上的绝对位置
            return new Point(nodeX + portInNode.X, nodeY + portInNode.Y);
        }

        /// <summary>
        /// 查找指定画布坐标附近最近的端口
        /// </summary>
        private FrameworkElement FindNearbyPort(Point canvasPoint)
        {
            if (ItemsSource == null || _contentCanvas == null)
                return null;

            FrameworkElement closestPort = null;
            // 吸附距离（画布坐标），考虑缩放后仍然保持合理的吸附范围；放大系数便于诊断
            double minDistance = (PortSnapDistance * 3) / Math.Max(Scale, 0.1);

            System.Diagnostics.Debug.WriteLine($"[端口查找] 查找附近端口，鼠标位置: ({canvasPoint.X:F2}, {canvasPoint.Y:F2}), 吸附距离: {minDistance:F2}, 缩放: {Scale:F2}");

            // 遍历所有节点，查找它们的端口
            var itemsControl = _contentCanvas.Children.OfType<ItemsControl>().FirstOrDefault();
            if (itemsControl == null)
                return null;

            int portCount = 0;
            foreach (var node in ItemsSource.OfType<Node>())
            {
                // 跳过源节点（不能连接到自己）
                if (_connectionSourceNode != null && node.Id == _connectionSourceNode.Id)
                    continue;

                var container = itemsControl.ItemContainerGenerator.ContainerFromItem(node) as ContentPresenter;
                if (container == null)
                    continue;

                // 查找节点内的所有端口
                var ports = FindPortsInContainer(container);
                foreach (var port in ports)
                {
                    var portCenter = GetPortCenter(port);
                    if (double.IsNaN(portCenter.X) || double.IsNaN(portCenter.Y))
                        continue;

                    var distance = Math.Sqrt(
                        Math.Pow(canvasPoint.X - portCenter.X, 2) +
                        Math.Pow(canvasPoint.Y - portCenter.Y, 2));

                    portCount++;
                    if (portCount <= 5)  // 只输出前5个端口的信息，避免日志太多
                    {
                        System.Diagnostics.Debug.WriteLine($"[端口查找] 节点: {node.Name}, 端口中心: ({portCenter.X:F2}, {portCenter.Y:F2}), 距离: {distance:F2}");
                    }

                    if (distance < minDistance)
                    {
                        minDistance = distance;
                        closestPort = port;
                    }
                }
            }

            if (closestPort != null)
            {
                System.Diagnostics.Debug.WriteLine($"[端口查找] 找到最近端口，最终距离: {minDistance:F2}");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[端口查找] 未找到符合距离的端口，检查了 {portCount} 个端口");
            }

            return closestPort;
        }

        /// <summary>
        /// 在容器中查找所有端口控件
        /// </summary>
        private List<FrameworkElement> FindPortsInContainer(DependencyObject container)
        {
            var ports = new List<FrameworkElement>();
            if (container == null)
                return ports;

            FindPortsRecursive(container, ports);
            return ports;
        }

        /// <summary>
        /// 递归查找端口控件
        /// </summary>
        private void FindPortsRecursive(DependencyObject element, List<FrameworkElement> ports)
        {
            if (element == null)
                return;

            if (element is PortControl portControl)
            {
                ports.Add(portControl);
            }

            int childCount = VisualTreeHelper.GetChildrenCount(element);
            for (int i = 0; i < childCount; i++)
            {
                var child = VisualTreeHelper.GetChild(element, i);
                FindPortsRecursive(child, ports);
            }
        }

        private NodeControl FindParentNodeControl(DependencyObject element)
        {
            var current = element;
            while (current != null)
            {
                if (current is NodeControl nc)
                    return nc;
                current = VisualTreeHelper.GetParent(current);
            }
            return null;
        }

        /// <summary>
        /// 查找父级 FlowEditor 控件
        /// </summary>
        private FlowEditor FindParentFlowEditor(DependencyObject element)
        {
            var current = element;
            while (current != null)
            {
                if (current is FlowEditor flowEditor)
                    return flowEditor;
                current = VisualTreeHelper.GetParent(current);
            }
            return null;
        }

        /// <summary>
        /// 构建正交线路径：端口 -> 源外扩点 -> L 形折线 -> 目标外扩点 -> 端口
        /// 改进版：基于端口实际位置和节点相对位置选择最优路径，并支持避障
        /// </summary>
        private List<Point> BuildOrthogonalRoute(Point startPort, Node source, Point endPort, Node target, List<Rect> obstacles = null)
        {
            const double margin = 18.0; // 外扩距离

            var sourceBounds = GetNodeBounds(source);
            var targetBounds = GetNodeBounds(target);

            // 判断端口在节点的哪一边（基于端口到各边的距离）
            var sourceSide = GetPortSideByDistance(startPort, sourceBounds);
            var targetSide = GetPortSideByDistance(endPort, targetBounds);

            // 计算外扩点
            var sourceOut = GetExpansionAlongSide(startPort, sourceBounds, margin, sourceSide);
            var targetOut = GetExpansionAlongSide(endPort, targetBounds, margin, targetSide);

            // 构建路径：简化为 StartPort -> SourceOut -> (L/Z) -> TargetOut -> EndPort
            var route = new List<Point> { startPort, sourceOut };

            if (!IsSamePoint(sourceOut, targetOut))
            {
                if (IsSameValue(sourceOut.X, targetOut.X) || IsSameValue(sourceOut.Y, targetOut.Y))
                {
                    // 共线直接连
                }
                else
                {
                    // 简单 L：先水平后垂直（若需要 Z 由碰撞/A* 处理）
                    var mid = new Point(targetOut.X, sourceOut.Y);
                    route.Add(mid);
                }
            }

            route.Add(targetOut);
            route.Add(endPort);

            // 去除连续重复点
            for (int i = route.Count - 2; i >= 0; i--)
            {
                if (IsSamePoint(route[i], route[i + 1]))
                    route.RemoveAt(i + 1);
            }

            // 检查障碍物碰撞
            if (obstacles != null && obstacles.Count > 0)
            {
                // 除了中间的线段，也要检查起点和终点的连接段（如果起点终点外扩点被挡住了）
                // 尤其是当两个节点靠得很近时，外扩点可能落在另一个节点的边界内
                // 但通常外扩点是为了离开节点，所以主要检查中间路径

                // 额外检查：如果源节点和目标节点非常近，直接连线可能会穿过它们自己
                // 这种情况在 BuildOrthogonalRoute 的初始阶段已经通过 margin 规避了一部分
                // 但如果两个 Top 端口相连，且Y轴错开不远，可能会穿过其中一个节点

                bool collision = false;
                for (int i = 0; i < route.Count - 1; i++)
                {
                    var p1 = route[i];
                    var p2 = route[i + 1];

                    foreach (var obs in obstacles)
                    {
                        // 稍微缩小障碍物矩形以允许贴边（避免误判）
                        const double obstacleMargin = 8.0;
                        var checkRect = new Rect(obs.X - obstacleMargin, obs.Y - obstacleMargin,
                                                 Math.Max(1, obs.Width + obstacleMargin * 2),
                                                 Math.Max(1, obs.Height + obstacleMargin * 2));

                        // 记录碰撞检测信息
                        bool intersected = IntersectsRect(p1, p2, checkRect);
                        if (intersected)
                        {
                            System.Diagnostics.Debug.WriteLine($"[碰撞检测] 发生碰撞！线段: ({p1.X:F2},{p1.Y:F2})->({p2.X:F2},{p2.Y:F2}), 障碍物: {checkRect}");
                            collision = true;
                            break;
                        }
                    }
                    if (collision) break;
                }

                // 还要检查是否穿过了源节点或目标节点本身（除了第一段和最后一段）
                // 因为标准的 L 形路由可能会产生穿过源/目标节点的情况（特别是 Top 到 Top）
                if (!collision)
                {
                    // 检查除第一段外的所有段是否穿过源节点
                    for (int i = 1; i < route.Count - 1; i++)
                    {
                        if (IntersectsRect(route[i], route[i + 1], sourceBounds))
                        {
                            System.Diagnostics.Debug.WriteLine($"[碰撞检测] 连线穿过源节点！");
                            collision = true;
                            break;
                        }
                    }

                    // 检查除最后一段外的所有段是否穿过目标节点
                    if (!collision)
                    {
                        for (int i = 0; i < route.Count - 2; i++)
                        {
                            if (IntersectsRect(route[i], route[i + 1], targetBounds))
                            {
                                System.Diagnostics.Debug.WriteLine($"[碰撞检测] 连线穿过目标节点！");
                                collision = true;
                                break;
                            }
                        }
                    }
                }

                if (collision)
                {
                    System.Diagnostics.Debug.WriteLine($"[连线] 检测到与节点碰撞，切换到 A* 寻路");
                    // 使用A*算法重新规划路径
                    // 注意：这里我们需要把源节点和目标节点也当作障碍物（除了它们各自的端口区域）
                    // 但简单的做法是把它们加入障碍物列表，A* 会处理起点终点在障碍物内的情况（通常 A* 会寻找最近的无障碍点）
                    // 为了让 A* 能走出源节点，我们需要确保起点和终点本身被视为"可通行"

                    var allObstacles = new List<Rect>(obstacles);
                    allObstacles.Add(sourceBounds);
                    allObstacles.Add(targetBounds);

                    // 调整 A* 参数以偏好特定类型的路径
                    return FindPathAStar(startPort, endPort, allObstacles);
                }
            }
            // 即使没有外部障碍物，也要检查是否穿过源/目标节点自己
            else
            {
                bool selfCollision = false;
                // 检查除第一段外的所有段是否穿过源节点
                for (int i = 1; i < route.Count - 1; i++)
                {
                    if (IntersectsRect(route[i], route[i + 1], sourceBounds))
                    {
                        selfCollision = true; break;
                    }
                }
                // 检查除最后一段外的所有段是否穿过目标节点
                if (!selfCollision)
                {
                    for (int i = 0; i < route.Count - 2; i++)
                    {
                        if (IntersectsRect(route[i], route[i + 1], targetBounds))
                        {
                            selfCollision = true; break;
                        }
                    }
                }

                if (selfCollision)
                {
                    System.Diagnostics.Debug.WriteLine($"[连线] 连线穿过源/目标节点，切换到 A* 寻路");
                    var selfObstacles = new List<Rect> { sourceBounds, targetBounds };
                    return FindPathAStar(startPort, endPort, selfObstacles);
                }
            }

            return route;
        }

        private bool IntersectsRect(Point p1, Point p2, Rect rect)
        {
            double minX = Math.Min(p1.X, p2.X);
            double maxX = Math.Max(p1.X, p2.X);
            double minY = Math.Min(p1.Y, p2.Y);
            double maxY = Math.Max(p1.Y, p2.Y);

            if (maxX < rect.Left || minX > rect.Right || maxY < rect.Top || minY > rect.Bottom)
                return false;

            if (rect.Contains(p1) || rect.Contains(p2))
                return true;

            // 对于水平线，确保 X 轴和 Y 轴范围都有交集（包含边界）
            if (Math.Abs(p1.Y - p2.Y) < 0.1)
                return p1.Y >= rect.Top && p1.Y <= rect.Bottom &&
                       maxX >= rect.Left && minX <= rect.Right;

            // 对于垂直线，确保 Y 轴和 X 轴范围都有交集（包含边界）
            if (Math.Abs(p1.X - p2.X) < 0.1)
                return p1.X >= rect.Left && p1.X <= rect.Right &&
                       maxY >= rect.Top && minY <= rect.Bottom;

            return true;
        }

        /// <summary>
        /// 获取节点边界矩形（使用实际视觉尺寸）
        /// </summary>
        private Rect GetNodeBounds(Node node)
        {
            // 优先使用节点容器的实际视觉尺寸，直接转换到 transformTarget (逻辑坐标系)
            if (_transformTarget != null && _contentCanvas != null)
            {
                var itemsControl = _contentCanvas.Children.OfType<ItemsControl>().FirstOrDefault();
                if (itemsControl != null)
                {
                    var container = itemsControl.ItemContainerGenerator.ContainerFromItem(node) as ContentPresenter;
                    // 如果容器已经生成，且有实际尺寸
                    if (container != null && container.ActualWidth > 0 && container.ActualHeight > 0)
                    {
                        try
                        {
                            // 确保获取的是相对于 _transformTarget 的坐标（逻辑坐标）
                            var pos = container.TranslatePoint(new Point(0, 0), _transformTarget);
                            return new Rect(
                                pos.X,
                                pos.Y,
                                container.ActualWidth,
                                container.ActualHeight
                            );
                        }
                        catch { }
                    }
                    // 如果容器未生成（可能刚添加），尝试在 ItemsControl 中查找
                    else if (container == null)
                    {
                        // 强制更新布局可能导致死循环，所以这里尝试查找视觉树
                        // 但通常 ItemContainerGenerator 在 OnItemsChanged 后应该能获取到 container
                        // 如果获取不到，回退到数据绑定的位置
                    }
                }
            }

            // 回退：使用容器的 Canvas.Left/Top 或 Node.Position
            double x = node.Position.X;
            double y = node.Position.Y;
            if (_contentCanvas != null)
            {
                var itemsControl = _contentCanvas.Children.OfType<ItemsControl>().FirstOrDefault();
                if (itemsControl != null)
                {
                    var container = itemsControl.ItemContainerGenerator.ContainerFromItem(node) as ContentPresenter;
                    if (container != null)
                    {
                        var cx = Canvas.GetLeft(container);
                        var cy = Canvas.GetTop(container);
                        if (!double.IsNaN(cx)) x = cx;
                        if (!double.IsNaN(cy)) y = cy;
                    }
                }
            }

            var width = node.Size.IsEmpty ? 220 : node.Size.Width;
            var height = node.Size.IsEmpty ? 40 : node.Size.Height;

            return new Rect(
                x,
                y,
                width,
                height
            );
        }

        private enum PortSide
        {
            Top,
            Bottom,
            Left,
            Right
        }

        private PortSide GetPortSide(Point port, Rect bounds)
        {
            var cx = bounds.Left + bounds.Width / 2;
            var cy = bounds.Top + bounds.Height / 2;
            var dx = port.X - cx;
            var dy = port.Y - cy;
            if (Math.Abs(dx) >= Math.Abs(dy))
                return dx >= 0 ? PortSide.Right : PortSide.Left;
            return dy >= 0 ? PortSide.Bottom : PortSide.Top;
        }

        /// <summary>
        /// 基于端口相对节点中心的方向判断端口在哪一边
        /// 使用归一化方向判断，确保端口方向准确
        /// </summary>
        private PortSide GetPortSideByDistance(Point port, Rect bounds)
        {
            // 计算节点中心
            var cx = bounds.Left + bounds.Width / 2;
            var cy = bounds.Top + bounds.Height / 2;

            // 计算端口相对于中心的偏移
            var dx = port.X - cx;
            var dy = port.Y - cy;

            // 归一化到节点尺寸，避免宽高比影响判断
            var normalizedDx = dx / (bounds.Width / 2 + 0.0001); // 避免除零
            var normalizedDy = dy / (bounds.Height / 2 + 0.0001);

            // 基于归一化距离判断主方向
            if (Math.Abs(normalizedDx) > Math.Abs(normalizedDy))
            {
                // 水平方向占主导
                return normalizedDx > 0 ? PortSide.Right : PortSide.Left;
            }
            else
            {
                // 垂直方向占主导
                return normalizedDy > 0 ? PortSide.Bottom : PortSide.Top;
            }
        }

        private Point GetExpansionAlongSide(Point port, Rect bounds, double margin, PortSide side)
        {
            switch (side)
            {
                case PortSide.Top:
                    // 上侧端口：外扩点在端口正上方，X保持不变，Y向上外扩
                    return new Point(port.X, bounds.Top - margin);

                case PortSide.Bottom:
                    // 下侧端口：外扩点在端口正下方（或节点底部外），X保持不变，Y向下外扩
                    return new Point(port.X, bounds.Bottom + margin);

                case PortSide.Left:
                    // 左侧端口：外扩点在端口正左方，Y保持不变，X向左外扩
                    return new Point(bounds.Left - margin, port.Y);

                case PortSide.Right:
                default:
                    // 右侧端口：外扩点在端口正右方，Y保持不变，X向右外扩
                    return new Point(bounds.Right + margin, port.Y);
            }
        }

        private bool IsSameValue(double a, double b) => Math.Abs(a - b) < 0.01;

        private double Manhattan(Point a, Point b) => Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);

        private bool IsSamePoint(Point a, Point b) => Math.Abs(a.X - b.X) < 0.01 && Math.Abs(a.Y - b.Y) < 0.01;

        /// <summary>
        /// 为折线终点构建箭头
        /// </summary>
        private Path BuildArrow(PointCollection points, Brush stroke)
        {
            if (points == null || points.Count < 2)
                return null;

            var end = points[^1];
            var prev = points[^2];

            var dx = end.X - prev.X;
            var dy = end.Y - prev.Y;
            var len = Math.Sqrt(dx * dx + dy * dy);
            if (len < 0.0001) return null;

            dx /= len;
            dy /= len;

            const double size = 10.0;
            var back = new Point(end.X - dx * size, end.Y - dy * size);
            var perpX = -dy;
            var perpY = dx;

            var p1 = end;
            var p2 = new Point(back.X + perpX * (size / 2), back.Y + perpY * (size / 2));
            var p3 = new Point(back.X - perpX * (size / 2), back.Y - perpY * (size / 2));

            var geo = new StreamGeometry();
            using (var ctx = geo.Open())
            {
                ctx.BeginFigure(p1, true, true);
                ctx.LineTo(p2, true, false);
                ctx.LineTo(p3, true, false);
            }
            geo.Freeze();

            return new Path
            {
                Data = geo,
                Fill = stroke,
                Stroke = stroke,
                StrokeThickness = 1,
                IsHitTestVisible = false
            };
        }

        #endregion

        #region 事件处理

        private void InitializeEventHandlers()
        {
            // 使用 AddHandler 并启用 handledEventsToo，确保即使父级控件（如 ScrollViewer）
            // 已经处理了鼠标滚轮事件，InfiniteCanvas 仍然可以收到事件用于缩放
            AddHandler(MouseWheelEvent, new MouseWheelEventHandler(OnMouseWheel), true);
            AddHandler(MouseLeftButtonDownEvent, new MouseButtonEventHandler(OnCanvasMouseLeftButtonDown), true);
            AddHandler(MouseLeftButtonUpEvent, new MouseButtonEventHandler(OnCanvasMouseLeftButtonUp), true);
            AddHandler(MouseMoveEvent, new MouseEventHandler(OnCanvasMouseMove), true);

            PreviewMouseDown += OnPreviewMouseDown;
            PreviewMouseMove += OnPreviewMouseMove;
            PreviewMouseUp += OnPreviewMouseUp;
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

        /// <summary>
        /// 内容画布拖拽离开事件
        /// </summary>
        private void OnContentCanvasDragLeave(object sender, DragEventArgs e)
        {
            // 可以添加视觉反馈清除逻辑
        }

        /// <summary>
        /// 内容画布放置事件
        /// </summary>
        private void OnContentCanvasDrop(object sender, DragEventArgs e)
        {
            // 不处理，让事件冒泡到 FlowEditor
        }

        #endregion

        #region InfiniteCanvas 本身的拖放事件处理（备用）

        /// <summary>
        /// InfiniteCanvas 拖拽进入事件
        /// </summary>
        private void OnInfiniteCanvasDragEnter(object sender, DragEventArgs e)
        {
            if (e.Data != null && e.Data.GetFormats().Any(f => f.Contains("ToolItem") || f.Contains("FlowEditor")))
            {
                e.Effects = DragDropEffects.Copy;
            }
        }

        /// <summary>
        /// InfiniteCanvas 拖拽经过事件
        /// </summary>
        private void OnInfiniteCanvasDragOver(object sender, DragEventArgs e)
        {
            if (e.Data != null && e.Data.GetFormats().Any(f => f.Contains("ToolItem") || f.Contains("FlowEditor")))
            {
                e.Effects = DragDropEffects.Copy;
            }
        }

        /// <summary>
        /// InfiniteCanvas 拖拽离开事件
        /// </summary>
        private void OnInfiniteCanvasDragLeave(object sender, DragEventArgs e)
        {
            // 可以添加视觉反馈清除逻辑
        }

        /// <summary>
        /// InfiniteCanvas 放置事件
        /// </summary>
        private void OnInfiniteCanvasDrop(object sender, DragEventArgs e)
        {
            // 不处理，让事件继续冒泡到 FlowEditor
        }

        #endregion

        private void OnMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (!EnableZoom) return;

            // 直接使用滚轮缩放，不需要按任何修饰键
            // 如果按下了修饰键，则不缩放（避免与其他操作冲突）
            if (Keyboard.Modifiers == ModifierKeys.None)
            {
                // 稍大一些的缩放因子，使缩放更灵敏
                var zoomFactor = e.Delta > 0 ? 1.15 : 0.85;
                ZoomToPoint(e.GetPosition(this), zoomFactor);
                e.Handled = true;
            }
        }

        private void OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            // 优先级1: Ctrl+左键 - 平移画布
            if (e.ChangedButton == MouseButton.Left &&
                Keyboard.Modifiers == PanModifierKey)
            {
                _state.BeginPanning(e.GetPosition(this), PanX, PanY);
                CaptureMouse();
                Cursor = Cursors.Hand;
                e.Handled = true;
                return;
            }

            // 优先级2: 左键点击空白区域 - 框选
            // 重要：先检查是否点击在空白区域，如果不是，让事件继续传递给子控件（如 NodeControl）
            if (e.ChangedButton == MouseButton.Left &&
                Keyboard.Modifiers == ModifierKeys.None &&
                EnableBoxSelection)
            {
                // 使用更可靠的空白区域判断
                var hitElement = e.OriginalSource as DependencyObject;
                if (IsClickOnCanvasBackground(hitElement))
                {
                    // 点击在画布背景上，开始框选
                    StartBoxSelection(e.GetPosition(this));
                    e.Handled = true;
                    return;
                }
                // 否则不处理，让事件传递给子控件（NodeControl）
            }
        }

        private void OnPreviewMouseMove(object sender, MouseEventArgs e)
        {
            // 优先处理平移
            if (_state.IsPanning && IsMouseCaptured)
            {
                var currentPos = e.GetPosition(this);
                var delta = _state.CalculatePanDelta(currentPos);

                // 直接更新变换，不触发依赖属性回调（性能优化）
                if (_translateTransform != null)
                {
                    _translateTransform.X = delta.X;
                    _translateTransform.Y = delta.Y;
                }

                // 同时更新依赖属性（用于绑定和其他逻辑）
                PanX = delta.X;
                PanY = delta.Y;

                e.Handled = true;
                return;
            }

            // 处理框选
            if (_isBoxSelecting && IsMouseCaptured)
            {
                UpdateBoxSelection(e.GetPosition(this));
                e.Handled = true;
                return;
            }
        }

        private void OnPreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_state.IsPanning)
            {
                _state.EndPanning();
                ReleaseMouseCapture();
                Cursor = Cursors.Arrow;

                // 重置节流时间，确保拖动结束后一定会更新网格
                _lastGridUpdateTime = DateTime.MinValue;

                // 拖动结束后更新网格和视口指示器
                UpdateGrid();
                UpdateViewportIndicator();

                e.Handled = true;
                return;
            }

            if (_isBoxSelecting)
            {
                EndBoxSelection();
                e.Handled = true;
                return;
            }
        }

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

            // Ctrl+0 适应画布
            if (e.Key == Key.D0 && Keyboard.Modifiers == ModifierKeys.Control)
            {
                FitToScreen();
                e.Handled = true;
            }

            // F 键适应画布（常用快捷键）
            if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.None)
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

            // 重置节流时间，确保缩放改变时一定会更新网格
            canvas._lastGridUpdateTime = DateTime.MinValue;

            // 使用 Render 优先级，在渲染时更新
            canvas.Dispatcher.BeginInvoke(new Action(() =>
            {
                canvas.UpdateGrid();
                canvas.UpdateViewportIndicator();
            }), System.Windows.Threading.DispatcherPriority.Render);

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

            // 当内容变化时更新缩略图
            canvas.UpdateMinimap();
            canvas.RefreshEdges();
        }

        private static void OnEdgeItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var canvas = (InfiniteCanvas)d;
            if (canvas._edgeCollectionNotify != null)
            {
                canvas._edgeCollectionNotify.CollectionChanged -= canvas.OnEdgeCollectionChanged;
                canvas._edgeCollectionNotify = null;
            }

            canvas._edgeCollectionNotify = e.NewValue as INotifyCollectionChanged;
            if (canvas._edgeCollectionNotify != null)
            {
                canvas._edgeCollectionNotify.CollectionChanged += canvas.OnEdgeCollectionChanged;
            }

            canvas.RefreshEdges();
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
            System.Diagnostics.Debug.WriteLine($"[小地图可见性] ShowMinimap={ShowMinimap}, IsMinimapCollapsed={IsMinimapCollapsed}");

            if (!ShowMinimap)
            {
                System.Diagnostics.Debug.WriteLine("[小地图可见性] ShowMinimap=false，隐藏所有小地图控件");
                if (_minimapContainer != null) _minimapContainer.Visibility = Visibility.Collapsed;
                if (_minimapCollapseButton != null) _minimapCollapseButton.Visibility = Visibility.Collapsed;
                if (_minimapExpandButton != null) _minimapExpandButton.Visibility = Visibility.Collapsed;
                return;
            }

            if (IsMinimapCollapsed)
            {
                System.Diagnostics.Debug.WriteLine("[小地图可见性] 折叠状态");
                // 折叠状态：隐藏容器（包含画布），隐藏折叠按钮，显示展开按钮
                if (_minimapContainer != null) _minimapContainer.Visibility = Visibility.Collapsed;
                if (_minimapCollapseButton != null) _minimapCollapseButton.Visibility = Visibility.Collapsed;
                if (_minimapExpandButton != null) _minimapExpandButton.Visibility = Visibility.Visible;
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("[小地图可见性] 展开状态 - 小地图应该可见");
                // 展开状态：显示容器，显示折叠按钮，隐藏展开按钮
                if (_minimapContainer != null) 
                {
                    _minimapContainer.Visibility = Visibility.Visible;
                    System.Diagnostics.Debug.WriteLine($"[小地图可见性] _minimapContainer 设置为 Visible");
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
            if (!EnableBoxSelection || _selectionBox == null)
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
                            if (VisualTreeHelper.GetChild(container, 0) is NodeControl nodeControl)
                            {
                                nodeControl.IsSelected = false;
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
        }

        /// <summary>
        /// 删除选中的项（仅从 ItemsSource 中移除）
        /// </summary>
        public void DeleteSelectedItems()
        {
            if (_selectedItems.Count == 0 || ItemsSource == null)
                return;

            var itemsToDelete = new List<object>(_selectedItems);

            if (ItemsSource is IList list)
            {
                foreach (var item in itemsToDelete)
                {
                    list.Remove(item);
                }
            }
            else
            {
                // 尝试通过反射调用 Remove 方法
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

            ClearSelection();
        }

        /// <summary>
        /// 使用A*算法寻找避开障碍物的正交路径
        /// </summary>
        private List<Point> FindPathAStar(Point start, Point end, List<Rect> obstacles)
        {
            // 网格大小
            double gridSize = 20.0;

            // 启发式函数：曼哈顿距离
            double Heuristic(Point a, Point b) => Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);

            // 将点对齐到网格
            Point Snap(Point p) => new Point(Math.Round(p.X / gridSize) * gridSize, Math.Round(p.Y / gridSize) * gridSize);

            var startNode = Snap(start);
            var targetNode = Snap(end);

            var openSet = new PriorityQueue<Point, double>();
            openSet.Enqueue(startNode, 0);

            var cameFrom = new Dictionary<Point, Point>();
            var gScore = new Dictionary<Point, double>();
            gScore[startNode] = 0;

            // 记录方向以惩罚转弯
            var cameFromDir = new Dictionary<Point, Vector>();
            cameFromDir[startNode] = new Vector(0, 0);

            // 扩充障碍物区域，给予一点缓冲
            var expandedObstacles = obstacles.Select(r => new Rect(r.X - 10, r.Y - 10, r.Width + 20, r.Height + 20)).ToList();

            // 搜索边界 (限制搜索范围以提高性能)
            double minX = Math.Min(startNode.X, targetNode.X);
            double maxX = Math.Max(startNode.X, targetNode.X);
            double minY = Math.Min(startNode.Y, targetNode.Y);
            double maxY = Math.Max(startNode.Y, targetNode.Y);

            // 确保边界包含起点和终点
            minX = Math.Min(minX, Math.Min(start.X, end.X));
            maxX = Math.Max(maxX, Math.Max(start.X, end.X));
            minY = Math.Min(minY, Math.Min(start.Y, end.Y));
            maxY = Math.Max(maxY, Math.Max(start.Y, end.Y));

            // 如果有障碍物，扩展边界以允许绕路
            if (obstacles.Count > 0)
            {
                foreach (var obs in expandedObstacles)
                {
                    // 简单的包含检查优化
                    if (obs.Right < minX - 100 || obs.Left > maxX + 100 || obs.Bottom < minY - 100 || obs.Top > maxY + 100)
                        continue;

                    minX = Math.Min(minX, obs.Left);
                    maxX = Math.Max(maxX, obs.Right);
                    minY = Math.Min(minY, obs.Top);
                    maxY = Math.Max(maxY, obs.Bottom);
                }
            }

            // 增加额外的搜索边距
            double margin = 100;
            var searchBounds = new Rect(minX - margin, minY - margin, (maxX - minX) + margin * 2, (maxY - minY) + margin * 2);

            // 仅允许正交方向
            var directions = new[] { new Vector(gridSize, 0), new Vector(-gridSize, 0), new Vector(0, gridSize), new Vector(0, -gridSize) };

            int maxSteps = 3000; // 防止无限循环
            int steps = 0;

            while (openSet.Count > 0 && steps++ < maxSteps)
            {
                var current = openSet.Dequeue();

                if (Math.Abs(current.X - targetNode.X) < 1.0 && Math.Abs(current.Y - targetNode.Y) < 1.0)
                {
                    // 重建路径
                    var path = new List<Point>();
                    var curr = current;
                    while (cameFrom.ContainsKey(curr))
                    {
                        path.Add(curr);
                        curr = cameFrom[curr];
                    }
                    path.Add(start);
                    path.Reverse();
                    path.Add(end);

                    // 简化路径（去除共线点），保证正交性
                    // 由于网格搜索天生是正交的，但简化时要小心不要引入斜线（正常简化共线点不会引入斜线）
                    if (path.Count > 2)
                    {
                        for (int i = path.Count - 2; i > 0; i--)
                        {
                            var p1 = path[i - 1];
                            var p2 = path[i];
                            var p3 = path[i + 1];

                            // 检查三点共线
                            // 注意：由于是网格点，浮点误差很小，可以直接判断坐标是否相等
                            bool colinearX = Math.Abs(p1.X - p2.X) < 0.1 && Math.Abs(p2.X - p3.X) < 0.1;
                            bool colinearY = Math.Abs(p1.Y - p2.Y) < 0.1 && Math.Abs(p2.Y - p3.Y) < 0.1;

                            if (colinearX || colinearY)
                            {
                                path.RemoveAt(i);
                            }
                        }
                    }

                    // 再次确保所有线段都是正交的（A* 网格搜索本身应该保证这一点，但为了保险起见）
                    // 如果存在非正交线段（例如起点/终点吸附到网格时引入的），需要插入中间点
                    for (int i = 0; i < path.Count - 1; i++)
                    {
                        var p1 = path[i];
                        var p2 = path[i + 1];
                        if (Math.Abs(p1.X - p2.X) > 0.1 && Math.Abs(p1.Y - p2.Y) > 0.1)
                        {
                            // 发现斜线，插入中间拐点
                            // 优先水平移动
                            var mid = new Point(p2.X, p1.Y);
                            path.Insert(i + 1, mid);
                            i++; // 跳过新插入的点
                        }
                    }

                    return path;
                }

                foreach (var dir in directions)
                {
                    var neighbor = new Point(current.X + dir.X, current.Y + dir.Y);

                    // 检查边界
                    if (!searchBounds.Contains(neighbor)) continue;

                    // 检查障碍物
                    bool collision = false;
                    foreach (var obs in expandedObstacles)
                    {
                        if (obs.Contains(neighbor))
                        {
                            collision = true;
                            break;
                        }
                    }
                    if (collision) continue;

                    // 计算代价
                    double tentativeGScore = gScore[current] + gridSize;

                    // 转弯惩罚
                    if (cameFromDir.TryGetValue(current, out var prevDir))
                    {
                        if (prevDir.Length > 0 && (prevDir.X != dir.X || prevDir.Y != dir.Y))
                        {
                            tentativeGScore += gridSize * 0.5; // 转弯代价
                        }
                    }

                    if (!gScore.ContainsKey(neighbor) || tentativeGScore < gScore[neighbor])
                    {
                        cameFrom[neighbor] = current;
                        cameFromDir[neighbor] = dir;
                        gScore[neighbor] = tentativeGScore;
                        double fScore = tentativeGScore + Heuristic(neighbor, targetNode);
                        openSet.Enqueue(neighbor, fScore);
                    }
                }
            }

            // 如果找不到路径，回退到正交折线连接
            // A* 失败回退逻辑优化：保证回退路径也是正交的
            var fallbackPath = new List<Point> { start };

            // 简单的中间点折线逻辑（与 BuildSimpleOrthogonalPath 类似）
            var midX = (start.X + end.X) / 2;
            fallbackPath.Add(new Point(midX, start.Y));
            fallbackPath.Add(new Point(midX, end.Y));

            fallbackPath.Add(end);
            return fallbackPath;
        }

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
