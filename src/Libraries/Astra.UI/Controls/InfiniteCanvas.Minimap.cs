using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Astra.UI.Controls
{
    /// <summary>
    /// InfiniteCanvas 小地图交互部分（重构版）
    /// </summary>
    public partial class InfiniteCanvas
    {
        #region 小地图交互状态
        
        private bool _isMinimapDragging;        // 是否正在拖动视口指示器
        private Point _minimapDragStartPoint;   // 拖动起始点（相对于视口指示器）
        
        #endregion
        
        #region 小地图鼠标事件处理（简化版）
        
        /// <summary>
        /// 小地图鼠标按下 - 支持拖动指示器和快速跳转
        /// </summary>
        private void OnMinimapMouseDownSimplified(object sender, MouseButtonEventArgs e)
        {
            if (_minimapCanvas == null || !ShowMinimap)
                return;
            
            var clickPoint = e.GetPosition(_minimapCanvas);
            
            // 判断是否点击在视口指示器上
            var hitElement = e.OriginalSource as DependencyObject;
            
            // 方法1：检查是否为后代
            var isClickOnIndicator = IsDescendantOrSelf(_viewportIndicator, hitElement);
            
            // 方法2：检查鼠标位置是否在指示器的边界内
            var isInBounds = false;
            if (_viewportIndicator != null)
            {
                var indicatorBounds = new Rect(
                    Canvas.GetLeft(_viewportIndicator),
                    Canvas.GetTop(_viewportIndicator),
                    _viewportIndicator.ActualWidth,
                    _viewportIndicator.ActualHeight);
                
                if (!double.IsNaN(indicatorBounds.Left) && !double.IsNaN(indicatorBounds.Top))
                {
                    isInBounds = indicatorBounds.Contains(clickPoint);
                }
            }
            
            // 优先使用位置检测（更可靠）
            if (!isClickOnIndicator && isInBounds)
            {
                isClickOnIndicator = true;
            }
            
            if (isClickOnIndicator)
            {
                // 直接开始拖动视口指示器
                _isMinimapDragging = true;
                _isDraggingViewportIndicator = true;
                _minimapDragStartPoint = e.GetPosition(_minimapCanvas);
                _minimapCanvas.CaptureMouse();
                _viewportIndicator.Cursor = Cursors.SizeAll;
                e.Handled = true;
            }
            else
            {
                // 点击空白区域，快速跳转
                NavigateToMinimapPoint(clickPoint);
                e.Handled = true;
            }
        }
        
        /// <summary>
        /// 小地图鼠标移动 - 拖动视口指示器
        /// </summary>
        private void OnMinimapMouseMoveSimplified(object sender, MouseEventArgs e)
        {
            if (!_isMinimapDragging || _minimapCanvas == null || _viewportIndicator == null)
                return;
            
            // 获取鼠标在小地图画布上的当前位置
            var currentMousePos = e.GetPosition(_minimapCanvas);
            
            // 计算鼠标移动的增量
            var deltaX = currentMousePos.X - _minimapDragStartPoint.X;
            var deltaY = currentMousePos.Y - _minimapDragStartPoint.Y;
            
            // 获取视口指示器的当前位置
            var currentLeft = Canvas.GetLeft(_viewportIndicator);
            var currentTop = Canvas.GetTop(_viewportIndicator);
            
            // 计算视口指示器的新位置
            var newLeft = currentLeft + deltaX;
            var newTop = currentTop + deltaY;
            
            // 更新起始点为当前位置（用于下一次移动计算）
            _minimapDragStartPoint = currentMousePos;
            
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
                // 无限画布模式：至少保留一部分可见
                var minVisible = 20.0;
                newLeft = Math.Max(-indicatorWidth + minVisible, Math.Min(newLeft, canvasWidth - minVisible));
                newTop = Math.Max(-indicatorHeight + minVisible, Math.Min(newTop, canvasHeight - minVisible));
            }
            
            // 更新指示器位置
            Canvas.SetLeft(_viewportIndicator, newLeft);
            Canvas.SetTop(_viewportIndicator, newTop);
            
            // 🚀 实时同步到主画布（直接更新 Transform，最快响应）
            if (_minimapContentBounds.IsEmpty || _minimapScale <= 0)
            {
                UpdateViewportIndicator(allowDuringDrag: true);
                if (_minimapContentBounds.IsEmpty || _minimapScale <= 0)
                    return;
            }
            
            // 计算主画布的 Pan 值
            var viewportLeftInCanvas = newLeft / _minimapScale + _minimapContentBounds.Left;
            var viewportTopInCanvas = newTop / _minimapScale + _minimapContentBounds.Top;
            
            var currentScale = _scaleTransform?.ScaleX ?? Scale;
            if (currentScale <= 0 || double.IsNaN(currentScale) || double.IsInfinity(currentScale))
            {
                currentScale = 1.0;
            }
            
            var newPanX = -viewportLeftInCanvas * currentScale;
            var newPanY = -viewportTopInCanvas * currentScale;
            
            // 直接更新 Transform（不触发服务层，确保实时性）
            if (_translateTransform != null)
            {
                _translateTransform.X = newPanX;
                _translateTransform.Y = newPanY;
            }
        }
        
        /// <summary>
        /// 小地图鼠标释放 - 结束拖动
        /// </summary>
        private void OnMinimapMouseUpSimplified(object sender, MouseButtonEventArgs e)
        {
            if (!_isMinimapDragging)
                return;
            
            // 读取指示器最终位置
            var finalLeft = Canvas.GetLeft(_viewportIndicator);
            var finalTop = Canvas.GetTop(_viewportIndicator);
            if (double.IsNaN(finalLeft)) finalLeft = 0;
            if (double.IsNaN(finalTop)) finalTop = 0;
            
            // 计算最终 Pan 值
            var currentScale = _scaleTransform?.ScaleX ?? Scale;
            if (currentScale <= 0 || double.IsNaN(currentScale) || double.IsInfinity(currentScale))
            {
                currentScale = 1.0;
            }
            
            var viewportLeftInCanvas = finalLeft / _minimapScale + _minimapContentBounds.Left;
            var viewportTopInCanvas = finalTop / _minimapScale + _minimapContentBounds.Top;
            var finalPanX = -viewportLeftInCanvas * currentScale;
            var finalPanY = -viewportTopInCanvas * currentScale;
            
            // 🔄 拖动结束，同步到服务层和依赖属性
            if (_transformService != null)
            {
                _transformService.PanX = finalPanX;
                _transformService.PanY = finalPanY;
            }
            else
            {
                PanX = finalPanX;
                PanY = finalPanY;
            }
            
            _isMinimapDragging = false;
            _isDraggingViewportIndicator = false; // ✅ 同步旧字段，允许 UpdateViewportIndicator 恢复工作
            _minimapCanvas.ReleaseMouseCapture();
            _viewportIndicator.Cursor = Cursors.Hand;
            
            // 重置节流时间
            _lastGridUpdateTime = DateTime.MinValue;
            UpdateGrid();
            UpdateViewportIndicator();
        }
        
        /// <summary>
        /// 判断元素是否为指定父元素或其后代
        /// </summary>
        private bool IsDescendantOrSelf(DependencyObject parent, DependencyObject element)
        {
            if (parent == null || element == null)
                return false;
            
            // 检查是否为同一元素
            if (ReferenceEquals(parent, element))
                return true;
            
            // 检查是否为后代
            return IsDescendant(parent, element);
        }
        
        #endregion
    }
}

