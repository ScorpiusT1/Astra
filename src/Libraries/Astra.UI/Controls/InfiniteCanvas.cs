using System;
using System.Collections;
using System.Collections.Generic;
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
    public class InfiniteCanvas : Control
    {
        private const string PART_ContentCanvas = "PART_ContentCanvas";
        private const string PART_GridLayer = "PART_GridLayer";
        private const string PART_AlignmentLayer = "PART_AlignmentLayer";
        private const string PART_MinimapContainer = "PART_MinimapContainer";

        static InfiniteCanvas()
        {
            DefaultStyleKeyProperty.OverrideMetadata(
                typeof(InfiniteCanvas),
                new FrameworkPropertyMetadata(typeof(InfiniteCanvas)));
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
                new PropertyMetadata(0.2));

        public double MinScale
        {
            get => (double)GetValue(MinScaleProperty);
            set => SetValue(MinScaleProperty, value);
        }

        public static readonly DependencyProperty MaxScaleProperty =
            DependencyProperty.Register(nameof(MaxScale), typeof(double), typeof(InfiniteCanvas),
                new PropertyMetadata(4.0));

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

        // ============ 数据源 ============

        public static readonly DependencyProperty ItemsSourceProperty =
            DependencyProperty.Register(nameof(ItemsSource), typeof(IEnumerable), typeof(InfiniteCanvas),
                new PropertyMetadata(null));

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

        #endregion

        #region 模板应用

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            _contentCanvas = GetTemplateChild(PART_ContentCanvas) as Canvas;
            _gridLayer = GetTemplateChild(PART_GridLayer) as Canvas;
            _alignmentLayer = GetTemplateChild(PART_AlignmentLayer) as Canvas;

            if (_contentCanvas != null)
            {
                InitializeTransforms();
                InitializeEventHandlers();
            }

            UpdateGrid();
        }

        private void InitializeTransforms()
        {
            var transformGroup = new TransformGroup();
            _scaleTransform = new ScaleTransform(Scale, Scale);
            _translateTransform = new TranslateTransform(PanX, PanY);

            transformGroup.Children.Add(_scaleTransform);
            transformGroup.Children.Add(_translateTransform);

            _contentCanvas.RenderTransform = transformGroup;
            _contentCanvas.RenderTransformOrigin = new Point(0, 0);
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

            _gridLayer.Children.Clear();

            var width = ActualWidth;
            var height = ActualHeight;
            var spacing = GridSpacing * Scale;

            if (spacing < 5) return;

            // ✅ 动态获取网格画刷（如果未设置，使用 DynamicResource 默认值）
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

        #region 事件处理

        private void InitializeEventHandlers()
        {
            MouseWheel += OnMouseWheel;
            PreviewMouseDown += OnPreviewMouseDown;
            PreviewMouseMove += OnPreviewMouseMove;
            PreviewMouseUp += OnPreviewMouseUp;
            KeyDown += OnKeyDown;
            KeyUp += OnKeyUp;
            SizeChanged += OnSizeChanged;
        }

        private void OnMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (!EnableZoom) return;

            var zoomFactor = e.Delta > 0 ? 1.1 : 0.9;
            ZoomToPoint(e.GetPosition(this), zoomFactor);
            e.Handled = true;
        }

        private void OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left &&
                Keyboard.Modifiers == PanModifierKey)
            {
                _state.BeginPanning(e.GetPosition(this), PanX, PanY);
                CaptureMouse();
                Cursor = Cursors.Hand;
                e.Handled = true;
            }
        }

        private void OnPreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (_state.IsPanning)
            {
                var currentPos = e.GetPosition(this);
                var delta = _state.CalculatePanDelta(currentPos);

                PanX = delta.X;
                PanY = delta.Y;
            }
        }

        private void OnPreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_state.IsPanning)
            {
                _state.EndPanning();
                ReleaseMouseCapture();
                Cursor = Cursors.Arrow;
            }
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Space && !_state.IsPanning)
                Cursor = Cursors.Hand;
        }

        private void OnKeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Space && !_state.IsPanning)
                Cursor = Cursors.Arrow;
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateGrid();
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
            canvas.UpdateGrid();
            canvas.RaiseViewTransformChanged();
        }

        private static void OnTransformChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var canvas = (InfiniteCanvas)d;
            if (canvas._translateTransform != null)
            {
                if (e.Property == PanXProperty)
                    canvas._translateTransform.X = (double)e.NewValue;
                else if (e.Property == PanYProperty)
                    canvas._translateTransform.Y = (double)e.NewValue;
            }
            canvas.UpdateGrid();
            canvas.RaiseViewTransformChanged();
        }

        private static void OnGridSettingsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((InfiniteCanvas)d).UpdateGrid();
        }

        private void RaiseViewTransformChanged()
        {
            RaiseEvent(new RoutedEventArgs(ViewTransformChangedEvent));
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
