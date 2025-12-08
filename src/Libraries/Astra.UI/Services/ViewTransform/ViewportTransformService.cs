using System;
using System.Windows;

namespace Astra.UI.Services.ViewTransform
{
    /// <summary>
    /// 视口变换服务实现类
    /// </summary>
    public class ViewportTransformService : IViewportTransformService
    {
        #region 私有字段
        
        private double _scale = 1.0;
        private double _panX = 0.0;
        private double _panY = 0.0;
        
        #endregion
        
        #region 属性实现
        
        public double MinScale { get; set; } = 0.4;
        public double MaxScale { get; set; } = 2.0;
        
        public double Scale
        {
            get => _scale;
            set
            {
                // 限制在 MinScale 和 MaxScale 之间
                var newValue = Math.Max(MinScale, Math.Min(MaxScale, value));
                
                // 只有真正改变时才触发事件（避免精度问题导致的频繁触发）
                if (Math.Abs(_scale - newValue) > 0.0001)
                {
                    _scale = newValue;
                    OnTransformChanged();
                }
            }
        }
        
        public double PanX
        {
            get => _panX;
            set
            {
                if (Math.Abs(_panX - value) > 0.0001)
                {
                    _panX = value;
                    OnTransformChanged();
                }
            }
        }
        
        public double PanY
        {
            get => _panY;
            set
            {
                if (Math.Abs(_panY - value) > 0.0001)
                {
                    _panY = value;
                    OnTransformChanged();
                }
            }
        }
        
        #endregion
        
        #region 事件
        
        public event EventHandler TransformChanged;
        
        protected virtual void OnTransformChanged()
        {
            TransformChanged?.Invoke(this, EventArgs.Empty);
        }
        
        #endregion
        
        #region 坐标转换实现
        
        public Point ScreenToCanvas(Point screenPoint)
        {
            // 公式：canvasPoint = (screenPoint - pan) / scale
            return new Point(
                (screenPoint.X - PanX) / Scale,
                (screenPoint.Y - PanY) / Scale
            );
        }
        
        public Point CanvasToScreen(Point canvasPoint)
        {
            // 公式：screenPoint = canvasPoint * scale + pan
            return new Point(
                canvasPoint.X * Scale + PanX,
                canvasPoint.Y * Scale + PanY
            );
        }
        
        #endregion
        
        #region 变换操作实现
        
        public void ZoomToPoint(Point screenPoint, double zoomFactor)
        {
            // 1. 记录缩放前该点对应的画布坐标
            var canvasBefore = ScreenToCanvas(screenPoint);
            
            // 2. 应用缩放
            Scale *= zoomFactor;
            
            // 3. 计算缩放后该点对应的画布坐标
            var canvasAfter = ScreenToCanvas(screenPoint);
            
            // 4. 调整平移量，使该点在画布坐标系中的位置保持不变
            PanX += (canvasAfter.X - canvasBefore.X) * Scale;
            PanY += (canvasAfter.Y - canvasBefore.Y) * Scale;
        }
        
        public void Pan(double deltaX, double deltaY)
        {
            PanX += deltaX;
            PanY += deltaY;
        }
        
        public void ResetView()
        {
            Scale = 1.0;
            PanX = 0;
            PanY = 0;
        }
        
        public void FitToScreen(Rect contentBounds, double viewportWidth, double viewportHeight)
        {
            // 空内容或无效视口，重置到默认状态
            if (contentBounds.IsEmpty || viewportWidth <= 0 || viewportHeight <= 0)
            {
                ResetView();
                return;
            }
            
            // 添加 10% 边距
            const double marginPercent = 0.1;
            var marginX = contentBounds.Width * marginPercent;
            var marginY = contentBounds.Height * marginPercent;
            
            var paddedBounds = new Rect(
                contentBounds.X - marginX,
                contentBounds.Y - marginY,
                contentBounds.Width + marginX * 2,
                contentBounds.Height + marginY * 2
            );
            
            // 计算缩放比例（取较小值以确保全部可见）
            var scaleX = viewportWidth / paddedBounds.Width;
            var scaleY = viewportHeight / paddedBounds.Height;
            Scale = Math.Min(scaleX, scaleY);
            
            // 计算居中位置
            var centerX = paddedBounds.Left + paddedBounds.Width / 2;
            var centerY = paddedBounds.Top + paddedBounds.Height / 2;
            
            PanX = viewportWidth / 2 - centerX * Scale;
            PanY = viewportHeight / 2 - centerY * Scale;
        }
        
        #endregion
    }
}

