using System;
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
    [TemplatePart(Name = PART_ViewportIndicator, Type = typeof(Rectangle))]
    [TemplatePart(Name = PART_MinimapCollapseButton, Type = typeof(Button))]
    [TemplatePart(Name = PART_MinimapExpandButton, Type = typeof(Button))]
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
                new PropertyMetadata(true));

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

        // ============ 数据源 ============

        public static readonly DependencyProperty ItemsSourceProperty =
            DependencyProperty.Register(nameof(ItemsSource), typeof(IEnumerable), typeof(InfiniteCanvas),
                new PropertyMetadata(null, OnItemsSourceChanged));

        public IEnumerable ItemsSource
        {
            get => (IEnumerable)GetValue(ItemsSourceProperty);
            set => SetValue(ItemsSourceProperty, value);
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
        private Rectangle _viewportIndicator;
        private Button _minimapCollapseButton;
        private Button _minimapExpandButton;
        private bool _isNavigatingMinimap;
        private FrameworkElement _transformTarget; // 专门用于承载缩放/平移变换的视觉元素
        
        // 框选相关字段
        private bool _isBoxSelecting;
        private Point _selectionStartPoint;
        private Rectangle _selectionBox;
        private List<object> _selectedItems = new List<object>();
        
        // 性能优化：节流控制
        private DateTime _lastGridUpdateTime = DateTime.MinValue;
        private const int GridUpdateThrottleMs = 16; // 约60fps

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
            _viewportIndicator = GetTemplateChild(PART_ViewportIndicator) as Rectangle;
            _minimapCollapseButton = GetTemplateChild(PART_MinimapCollapseButton) as Button;
            _minimapExpandButton = GetTemplateChild(PART_MinimapExpandButton) as Button;
            
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

            var transformGroup = new TransformGroup();
            _scaleTransform = new ScaleTransform(Scale, Scale);
            _translateTransform = new TranslateTransform(PanX, PanY);

            transformGroup.Children.Add(_scaleTransform);
            transformGroup.Children.Add(_translateTransform);

            _transformTarget.RenderTransform = transformGroup;
            _transformTarget.RenderTransformOrigin = new Point(0, 0);
        }

        private void InitializeMinimap()
        {
            if (_minimapCanvas == null || _viewportIndicator == null) return;

            // 为缩略图画布添加鼠标事件处理
            _minimapCanvas.MouseLeftButtonDown += OnMinimapMouseDown;
            _minimapCanvas.MouseLeftButtonUp += OnMinimapMouseUp;
            _minimapCanvas.MouseMove += OnMinimapMouseMove;
            _minimapCanvas.MouseLeave += OnMinimapMouseLeave;
            
            // 监听布局更新，确保视口指示器正确显示
            _minimapCanvas.LayoutUpdated += (s, e) => UpdateViewportIndicator();
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
        }

        #endregion

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
        /// 更新缩略图 - 显示画布上所有内容的缩小版
        /// </summary>
        private void UpdateMinimap()
        {
            if (_minimapCanvas == null || !ShowMinimap || _contentCanvas == null || IsMinimapCollapsed) return;

            _minimapCanvas.Children.Clear();

            // 计算画布内容的边界
            var contentBounds = GetContentBounds();

            if (contentBounds.IsEmpty)
            {
                UpdateViewportIndicator();
                return;
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
                var contentBrush = TryFindResource("PrimaryBrush") as Brush ?? Brushes.Blue;
                foreach (var item in ItemsSource)
                {
                    if (item is FrameworkElement element)
                    {
                        var x = Canvas.GetLeft(element);
                        var y = Canvas.GetTop(element);
                        if (double.IsNaN(x)) x = 0;
                        if (double.IsNaN(y)) y = 0;

                        var minimapRect = new Rectangle
                        {
                            Width = Math.Max(element.ActualWidth * minimapScale, 2),
                            Height = Math.Max(element.ActualHeight * minimapScale, 2),
                            Fill = contentBrush,
                            Opacity = 0.6
                        };

                        Canvas.SetLeft(minimapRect, (x - contentBounds.Left) * minimapScale);
                        Canvas.SetTop(minimapRect, (y - contentBounds.Top) * minimapScale);

                        _minimapCanvas.Children.Add(minimapRect);
                    }
                    else if (item is DependencyObject depObj)
                    {
                        // 尝试从绑定中获取位置
                        var xProp = depObj.GetType().GetProperty("X");
                        var yProp = depObj.GetType().GetProperty("Y");
                        if (xProp != null && yProp != null)
                        {
                            var x = Convert.ToDouble(xProp.GetValue(depObj) ?? 0);
                            var y = Convert.ToDouble(yProp.GetValue(depObj) ?? 0);

                            var minimapRect = new Rectangle
                            {
                                Width = 4,
                                Height = 4,
                                Fill = contentBrush,
                                Opacity = 0.6
                            };

                            Canvas.SetLeft(minimapRect, (x - contentBounds.Left) * minimapScale);
                            Canvas.SetTop(minimapRect, (y - contentBounds.Top) * minimapScale);

                            _minimapCanvas.Children.Add(minimapRect);
                        }
                    }
                }
            }

            // 存储缩放信息用于导航
            _minimapScale = minimapScale;
            _minimapContentBounds = contentBounds;

            // 立即更新视口指示器
            UpdateViewportIndicator();
        }

        /// <summary>
        /// 更新视口指示器 - 高亮显示当前可见区域
        /// </summary>
        private void UpdateViewportIndicator()
        {
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
            var viewportLeft = -PanX / Scale;
            var viewportTop = -PanY / Scale;
            var viewportWidth = ActualWidth / Scale;
            var viewportHeight = ActualHeight / Scale;

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
        /// 从缩略图坐标转换为画布坐标并导航
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

            PanX = newPanX;
            PanY = newPanY;
        }

        private double _minimapScale = 1.0;
        private Rect _minimapContentBounds = Rect.Empty;

        #endregion

        #region 缩略图事件处理

        private void OnMinimapMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (!ShowMinimap || _minimapCanvas == null) return;

            var point = e.GetPosition(_minimapCanvas);
            NavigateToMinimapPoint(point);
            _isNavigatingMinimap = true;
            _minimapCanvas.CaptureMouse();
            e.Handled = true;
        }

        private void OnMinimapMouseMove(object sender, MouseEventArgs e)
        {
            if (!ShowMinimap || _minimapCanvas == null || !_isNavigatingMinimap) return;

            if (e.LeftButton == MouseButtonState.Pressed)
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

        private void OnMinimapMouseLeave(object sender, MouseEventArgs e)
        {
            if (_minimapCanvas != null && _isNavigatingMinimap)
            {
                _isNavigatingMinimap = false;
                _minimapCanvas.ReleaseMouseCapture();
            }
        }

        #endregion

        #region 事件处理

        private void InitializeEventHandlers()
        {
            // 使用 AddHandler 并启用 handledEventsToo，确保即使父级控件（如 ScrollViewer）
            // 已经处理了鼠标滚轮事件，InfiniteCanvas 仍然可以收到事件用于缩放
            AddHandler(MouseWheelEvent, new MouseWheelEventHandler(OnMouseWheel), true);

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
            if (canvas._translateTransform != null && !canvas._state.IsPanning)
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
            // 如果正在拖动，只在拖动结束时更新（在 OnPreviewMouseUp 中处理）
            
            canvas.RaiseViewTransformChanged();
        }

        private static void OnGridSettingsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((InfiniteCanvas)d).UpdateGrid();
        }

        private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var canvas = (InfiniteCanvas)d;
            // 当内容变化时更新缩略图
            canvas.UpdateMinimap();
        }

        private static void OnMinimapCollapsedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var canvas = (InfiniteCanvas)d;
            // 当展开时更新缩略图
            if (!canvas.IsMinimapCollapsed)
            {
                canvas.UpdateMinimap();
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
