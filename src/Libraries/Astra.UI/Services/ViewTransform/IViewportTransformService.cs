using System;
using System.Windows;

namespace Astra.UI.Services.ViewTransform
{
    /// <summary>
    /// 视口变换服务接口
    /// 职责：统一管理画布的缩放和平移变换
    /// </summary>
    public interface IViewportTransformService
    {
        #region 属性
        
        /// <summary>
        /// 当前缩放比例（1.0 = 100%）
        /// </summary>
        double Scale { get; set; }
        
        /// <summary>
        /// 水平平移偏移量（像素）
        /// </summary>
        double PanX { get; set; }
        
        /// <summary>
        /// 垂直平移偏移量（像素）
        /// </summary>
        double PanY { get; set; }
        
        /// <summary>
        /// 最小缩放比例
        /// </summary>
        double MinScale { get; set; }
        
        /// <summary>
        /// 最大缩放比例
        /// </summary>
        double MaxScale { get; set; }
        
        #endregion
        
        #region 坐标转换
        
        /// <summary>
        /// 屏幕坐标转换为画布逻辑坐标
        /// </summary>
        Point ScreenToCanvas(Point screenPoint);
        
        /// <summary>
        /// 画布逻辑坐标转换为屏幕坐标
        /// </summary>
        Point CanvasToScreen(Point canvasPoint);
        
        #endregion
        
        #region 变换操作
        
        /// <summary>
        /// 以指定屏幕点为中心进行缩放
        /// </summary>
        /// <param name="screenPoint">缩放中心点（屏幕坐标）</param>
        /// <param name="zoomFactor">缩放因子（>1 放大，<1 缩小）</param>
        void ZoomToPoint(Point screenPoint, double zoomFactor);
        
        /// <summary>
        /// 平移画布
        /// </summary>
        void Pan(double deltaX, double deltaY);
        
        /// <summary>
        /// 重置视图到默认状态（缩放=1.0，位置=0,0）
        /// </summary>
        void ResetView();
        
        /// <summary>
        /// 自动适应画布以显示所有内容
        /// </summary>
        /// <param name="contentBounds">内容边界</param>
        /// <param name="viewportWidth">视口宽度</param>
        /// <param name="viewportHeight">视口高度</param>
        void FitToScreen(Rect contentBounds, double viewportWidth, double viewportHeight);
        
        #endregion
        
        #region 事件
        
        /// <summary>
        /// 变换发生改变时触发（Scale/PanX/PanY 任一改变）
        /// </summary>
        event EventHandler TransformChanged;
        
        #endregion
    }
}

