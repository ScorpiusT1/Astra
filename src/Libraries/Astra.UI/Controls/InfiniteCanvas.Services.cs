using System;
using Astra.UI.Services.ViewTransform;

namespace Astra.UI.Controls
{
    /// <summary>
    /// InfiniteCanvas 服务层集成部分
    /// </summary>
    public partial class InfiniteCanvas
    {
        #region 服务字段
        
        private IViewportTransformService _transformService;
        
        #endregion
        
        #region 服务初始化
        
        /// <summary>
        /// 初始化视图变换服务
        /// </summary>
        private void InitializeTransformService()
        {
            // 创建服务实例
            _transformService = new ViewportTransformService
            {
                MinScale = this.MinScale,
                MaxScale = this.MaxScale,
                Scale = this.Scale,
                PanX = this.PanX,
                PanY = this.PanY
            };
            
            // 订阅服务的变化事件，同步到 UI 层
            _transformService.TransformChanged += OnTransformServiceChanged;
            
            System.Diagnostics.Debug.WriteLine("✅ [服务层] 视图变换服务已初始化");
        }
        
        /// <summary>
        /// 服务层变换改变时的回调
        /// </summary>
        private void OnTransformServiceChanged(object sender, EventArgs e)
        {
            // 服务层状态改变时，同步到 WPF 的 Transform 对象
            if (_scaleTransform != null)
            {
                _scaleTransform.ScaleX = _transformService.Scale;
                _scaleTransform.ScaleY = _transformService.Scale;
            }
            
            if (_translateTransform != null)
            {
                _translateTransform.X = _transformService.PanX;
                _translateTransform.Y = _transformService.PanY;
            }
            
            // 同步回依赖属性（供绑定使用）
            if (Math.Abs(Scale - _transformService.Scale) > 0.0001)
            {
                // 暂时禁用回调，避免循环更新
                _isUpdatingFromService = true;
                Scale = _transformService.Scale;
                _isUpdatingFromService = false;
            }
            
            if (Math.Abs(PanX - _transformService.PanX) > 0.0001)
            {
                _isUpdatingFromService = true;
                PanX = _transformService.PanX;
                _isUpdatingFromService = false;
            }
            
            if (Math.Abs(PanY - _transformService.PanY) > 0.0001)
            {
                _isUpdatingFromService = true;
                PanY = _transformService.PanY;
                _isUpdatingFromService = false;
            }
            
            // 延迟更新视觉（网格、小地图等）
            ScheduleVisualUpdate();
        }
        
        private bool _isUpdatingFromService = false;
        
        /// <summary>
        /// 延迟更新视觉（防止频繁刷新）
        /// </summary>
        private void ScheduleVisualUpdate()
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                _lastGridUpdateTime = DateTime.MinValue;
                UpdateGrid();
                UpdateViewportIndicator();
            }), System.Windows.Threading.DispatcherPriority.Render);
        }
        
        #endregion
    }
}

