using Astra.Core.Nodes.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Astra.UI.Styles.Controls
{
    /// <summary>
    /// MinimapControl：只负责缩略图区域的绘制与视口指示器显示。
    /// 外观样式由外部容器决定，本控件只提供数据依赖属性与最小绘制逻辑。
    /// </summary>
    public partial class MinimapControl : UserControl
    {
        public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
            name: "ItemsSource", propertyType: typeof(ObservableCollection<Node>), ownerType: typeof(MinimapControl),
            new PropertyMetadata(null, OnDataChanged));
        public ObservableCollection<Node> ItemsSource
        {
            get => (ObservableCollection<Node>)GetValue(ItemsSourceProperty);
            set => SetValue(ItemsSourceProperty, value);
        }

        public static readonly DependencyProperty MinimapBoundsProperty = DependencyProperty.Register(
            name: "MinimapBounds", propertyType: typeof(Rect), ownerType: typeof(MinimapControl),
            new PropertyMetadata(new Rect(-200, -200, 1200, 800), OnDataChanged));
        public Rect MinimapBounds
        {
            get => (Rect)GetValue(MinimapBoundsProperty);
            set => SetValue(MinimapBoundsProperty, value);
        }

        public static readonly DependencyProperty PanProperty = DependencyProperty.Register(
            name: "Pan", propertyType: typeof(Point), ownerType: typeof(MinimapControl),
            new PropertyMetadata(new Point(0, 0), OnDataChanged));
        public Point Pan
        {
            get => (Point)GetValue(PanProperty);
            set => SetValue(PanProperty, value);
        }

        public static readonly DependencyProperty ScaleProperty = DependencyProperty.Register(
            name: "Scale", propertyType: typeof(double), ownerType: typeof(MinimapControl),
            new PropertyMetadata(1.0, OnDataChanged));
        public double Scale
        {
            get => (double)GetValue(ScaleProperty);
            set => SetValue(ScaleProperty, value);
        }

        public static readonly DependencyProperty ViewportSizeProperty = DependencyProperty.Register(
            name: "ViewportSize", propertyType: typeof(Size), ownerType: typeof(MinimapControl),
            new PropertyMetadata(new Size(1, 1), OnDataChanged));
        public Size ViewportSize
        {
            get => (Size)GetValue(ViewportSizeProperty);
            set => SetValue(ViewportSizeProperty, value);
        }

        /// <summary>
        /// 视口拖动事件：外层可订阅后据此设置 Pan（将左上角对齐到拖动位置）。
        /// 参数为缩略图坐标（MinimapBounds 系）左上角。
        /// </summary>
        public event EventHandler<Point>? ViewportDrag;

        /// <summary>
        /// 用户点击缩略图（非视口）请求将视口中心移动到该点。
        /// 参数为缩略图坐标（MinimapBounds 系）。
        /// </summary>
        public event EventHandler<Point>? MinimapClick;

        public MinimapControl()
        {
            InitializeComponent();
            Loaded += (_, __) => Redraw();
            SizeChanged += (_, __) => Redraw();
            ViewportIndicator.MouseLeftButtonDown += OnViewportMouseDown;
            MinimapCanvas.MouseLeftButtonDown += OnMinimapMouseDown;
        }

        private static void OnDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            (d as MinimapControl)?.Redraw();
        }

        private void Redraw()
        {
            if (MinimapCanvas == null) return;
            MinimapCanvas.Children.Clear();
            if (MinimapBounds.Width <= 0 || MinimapBounds.Height <= 0) return;

            double scaleX = MinimapCanvas.ActualWidth / MinimapBounds.Width;
            double scaleY = MinimapCanvas.ActualHeight / MinimapBounds.Height;

            // 网格(可选)省略，由外层按需合成；这里只画元素和视口
            var selectionStrokeBrush = TryFindResource("MinimapSelectionStrokeBrush") as Brush
                                        ?? TryFindResource("ToolboxInfoBrush") as Brush
                                        ?? TryFindResource("BrushAccent") as Brush
                                        ?? Brushes.DodgerBlue;
           

            // 视口指示器：由 Pan/Scale/ViewportSize 驱动
            double viewLeft = -Pan.X / Scale;
            double viewTop = -Pan.Y / Scale;
            double viewWidth = Math.Max(1, ViewportSize.Width) / Scale;
            double viewHeight = Math.Max(1, ViewportSize.Height) / Scale;
            Canvas.SetLeft(ViewportIndicator, (viewLeft - MinimapBounds.X) * scaleX);
            Canvas.SetTop(ViewportIndicator, (viewTop - MinimapBounds.Y) * scaleY);
            ViewportIndicator.Width = Math.Max(8, viewWidth * scaleX);
            ViewportIndicator.Height = Math.Max(8, viewHeight * scaleY);
        }

        private bool _dragging;
        private Point _dragOffset;
        private void OnViewportMouseDown(object sender, MouseButtonEventArgs e)
        {
            _dragging = true;
            var mouse = e.GetPosition(MinimapCanvas);
            var vpPos = new Point(Canvas.GetLeft(ViewportIndicator), Canvas.GetTop(ViewportIndicator));
            _dragOffset = new Point(mouse.X - vpPos.X, mouse.Y - vpPos.Y);
            ViewportIndicator.CaptureMouse();
            ViewportIndicator.MouseMove += OnViewportMouseMove;
            ViewportIndicator.MouseLeftButtonUp += OnViewportMouseUp;
        }

        private void OnViewportMouseMove(object sender, MouseEventArgs e)
        {
            if (!_dragging) return;
            var mouse = e.GetPosition(MinimapCanvas);
            double newLeft = mouse.X - _dragOffset.X;
            double newTop = mouse.Y - _dragOffset.Y;
            Canvas.SetLeft(ViewportIndicator, newLeft);
            Canvas.SetTop(ViewportIndicator, newTop);

            // 映射回 MinimapBounds 坐标（左上角）并通知外层
            double scaleX = MinimapBounds.Width / Math.Max(1, MinimapCanvas.ActualWidth);
            double scaleY = MinimapBounds.Height / Math.Max(1, MinimapCanvas.ActualHeight);
            var canvasX = MinimapBounds.X + newLeft * scaleX;
            var canvasY = MinimapBounds.Y + newTop * scaleY;
            ViewportDrag?.Invoke(this, new Point(canvasX, canvasY));
        }

        private void OnViewportMouseUp(object sender, MouseButtonEventArgs e)
        {
            _dragging = false;
            ViewportIndicator.ReleaseMouseCapture();
            ViewportIndicator.MouseMove -= OnViewportMouseMove;
            ViewportIndicator.MouseLeftButtonUp -= OnViewportMouseUp;
        }

        private void OnMinimapMouseDown(object sender, MouseButtonEventArgs e)
        {
            // 将用户点击点映射到 MinimapBounds 坐标并通知外层
            var pos = e.GetPosition(MinimapCanvas);
            double scaleX = MinimapBounds.Width / Math.Max(1, MinimapCanvas.ActualWidth);
            double scaleY = MinimapBounds.Height / Math.Max(1, MinimapCanvas.ActualHeight);
            var canvasX = MinimapBounds.X + pos.X * scaleX;
            var canvasY = MinimapBounds.Y + pos.Y * scaleY;
            MinimapClick?.Invoke(this, new Point(canvasX, canvasY));
        }
    }
}
