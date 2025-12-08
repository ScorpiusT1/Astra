using System;
using System.Windows;
using System.Windows.Input;
using Astra.UI.Interaction;

namespace Astra.UI.Controls
{
    /// <summary>
    /// InfiniteCanvas 鼠标交互部分（重构版）
    /// </summary>
    public partial class InfiniteCanvas
    {
        #region 私有字段
        
        private MouseCaptureManager _mouseCaptureManager;
        
        // 交互状态
        private enum InteractionMode
        {
            None,           // 空闲
            Panning,        // 平移画布
            BoxSelecting,   // 框选
            Connecting,     // 连线中（保留原有逻辑）
            MinimapNavigating  // 小地图导航（保留原有逻辑）
        }
        
        private InteractionMode _currentInteractionMode = InteractionMode.None;
        
        // 平移状态
        private Point _panStartPoint;
        private Point _panStartOffset;
        private Point _panCurrentOffset;  // 当前偏移（用于 EndPanning 同步）
        
        // 框选状态
        private Point _boxSelectionStartPoint;
        
        #endregion
        
        #region 初始化
        
        /// <summary>
        /// 初始化统一的鼠标交互处理
        /// </summary>
        private void InitializeUnifiedMouseInteraction()
        {
            _mouseCaptureManager = new MouseCaptureManager();
            
            // 取消订阅旧的事件（如果有）
            // 注意：这里不需要显式取消，因为我们会在后面统一处理
            
            System.Diagnostics.Debug.WriteLine("✅ [交互系统] 统一鼠标事件处理已初始化");
        }
        
        #endregion
        
        #region 统一事件入口（新增）
        
        /// <summary>
        /// 统一的鼠标按下处理
        /// </summary>
        private void OnUnifiedMouseDown(object sender, MouseButtonEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine(
                $"🖱️ [MouseDown] 按钮:{e.ChangedButton} 修饰键:{Keyboard.Modifiers} Source:{e.Source?.GetType().Name} OriginalSource:{e.OriginalSource?.GetType().Name}");
            
            // 🎯 优先级0：检查是否点击在小地图区域（最高优先级）
            // 如果点击在小地图上，完全不处理，让小地图的事件处理器处理
            if (_minimapCanvas != null && e.ChangedButton == MouseButton.Left)
            {
                var hitElement = e.OriginalSource as DependencyObject;
                
                // 检查是否点击在小地图或视口指示器上
                bool isMinimapClick = IsDescendantOrSelf(_minimapCanvas, hitElement);
                System.Diagnostics.Debug.WriteLine(
                    $"🖱️ [MouseDown] 小地图检测: _minimapCanvas != null = true, IsDescendantOrSelf = {isMinimapClick}");
                
                if (isMinimapClick)
                {
                    System.Diagnostics.Debug.WriteLine("🗺️ [MouseDown] ✅ 点击在小地图区域，完全不处理，让事件传递");
                    // ⚠️ 关键：不调用 Focus()，不捕获鼠标，不设置 e.Handled
                    // 让事件完整传递到小地图的事件处理器
                    return;
                }
            }
            
            // 确保获取焦点（只有在非小地图点击时才获取）
            if (!IsFocused)
                Focus();
            
            // 检查是否已有活动交互
            if (_currentInteractionMode != InteractionMode.None)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"⚠️ [MouseDown] 当前模式:{_currentInteractionMode}，忽略新交互");
                return;
            }
            
            // 优先级1：Ctrl + 左键 = 平移
            if (e.ChangedButton == MouseButton.Left && 
                Keyboard.Modifiers == ModifierKeys.Control &&
                EnablePanning)
            {
                StartPanning(e);
                e.Handled = true;
                return;
            }
            
            // 优先级2：Shift + 左键 + 端口 = 开始连线
            // 注意：连线逻辑在 OnCanvasMouseLeftButtonDown 中处理（已恢复事件订阅）
            // 这里不处理，让事件继续传递
            
            // 优先级3：左键 + 空白区域 = 框选
            if (e.ChangedButton == MouseButton.Left &&
                Keyboard.Modifiers == ModifierKeys.None &&
                EnableBoxSelection)
            {
                var hitElement = e.OriginalSource as DependencyObject;
                if (IsClickOnCanvasBackground(hitElement))
                {
                    StartBoxSelectionUnified(e);
                    e.Handled = true;
                    return;
                }
            }
            
            // 其他情况让事件传递给子控件（NodeControl 等）
            System.Diagnostics.Debug.WriteLine("📤 [MouseDown] 事件传递给子控件");
        }
        
        /// <summary>
        /// 统一的鼠标移动处理
        /// </summary>
        private void OnUnifiedMouseMove(object sender, MouseEventArgs e)
        {
            // 如果正在连线，让连线事件处理器优先处理（不标记为 Handled）
            if (_isConnecting)
            {
                // 不处理，让 OnCanvasMouseMove 处理
                return;
            }
            
            switch (_currentInteractionMode)
            {
                case InteractionMode.Panning:
                    UpdatePanning(e);
                    e.Handled = true;
                    break;
                    
                case InteractionMode.BoxSelecting:
                    UpdateBoxSelection(e.GetPosition(this));
                    e.Handled = true;
                    break;
            }
        }
        
        /// <summary>
        /// 统一的鼠标释放处理
        /// </summary>
        private void OnUnifiedMouseUp(object sender, MouseButtonEventArgs e)
        {
            // 如果正在连线，让连线事件处理器优先处理（不标记为 Handled）
            if (_isConnecting)
            {
                // 不处理，让 OnCanvasMouseLeftButtonUp 处理
                return;
            }
            
            System.Diagnostics.Debug.WriteLine(
                $"🖱️ [MouseUp] 当前模式:{_currentInteractionMode}");
            
            switch (_currentInteractionMode)
            {
                case InteractionMode.Panning:
                    EndPanning();
                    e.Handled = true;
                    break;
                    
                case InteractionMode.BoxSelecting:
                    EndBoxSelectionUnified();
                    e.Handled = true;
                    break;
            }
        }
        
        /// <summary>
        /// 统一的鼠标滚轮处理
        /// </summary>
        private void OnUnifiedMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (!EnableZoom) return;
            
            // 直接缩放，无需修饰键
            if (Keyboard.Modifiers == ModifierKeys.None)
            {
                var zoomFactor = e.Delta > 0 ? 1.15 : 0.85;
                ZoomToPoint(e.GetPosition(this), zoomFactor);
                e.Handled = true;
            }
        }
        
        #endregion
        
        #region 平移交互
        
        private void StartPanning(MouseButtonEventArgs e)
        {
            _panStartPoint = e.GetPosition(this);
            _panStartOffset = new Point(PanX, PanY);
            _panCurrentOffset = _panStartOffset;  // 初始化当前偏移
            
            if (_mouseCaptureManager.TryCapture(this, "画布平移"))
            {
                _currentInteractionMode = InteractionMode.Panning;
                Cursor = Cursors.Hand;
                System.Diagnostics.Debug.WriteLine("✅ [平移] 开始");
            }
        }
        
        private void UpdatePanning(MouseEventArgs e)
        {
            var current = e.GetPosition(this);
            var delta = current - _panStartPoint;
            
            // 基于起始偏移计算新位置（_panStartOffset 保持不变！）
            var newPanX = _panStartOffset.X + delta.X;
            var newPanY = _panStartOffset.Y + delta.Y;
            
            // 🚀 性能优化：拖动过程中只更新 Transform，不触发服务层和依赖属性
            // 这样避免了循环更新和不必要的事件触发，确保最实时的响应
            if (_translateTransform != null)
            {
                _translateTransform.X = newPanX;
                _translateTransform.Y = newPanY;
            }
            
            // 保存当前偏移，在 EndPanning 时同步到服务层
            _panCurrentOffset = new Point(newPanX, newPanY);
        }
        
        private void EndPanning()
        {
            // 🔄 拖动结束，同步最终位置到服务层和依赖属性
            var finalPanX = _panCurrentOffset.X;
            var finalPanY = _panCurrentOffset.Y;
            
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
            
            _currentInteractionMode = InteractionMode.None;
            _mouseCaptureManager.Release();
            Cursor = Cursors.Arrow;
            
            // 重置节流时间，确保更新网格
            _lastGridUpdateTime = DateTime.MinValue;
            UpdateGrid();
            UpdateViewportIndicator();
            
            System.Diagnostics.Debug.WriteLine($"✅ [平移] 结束 - 最终位置: ({finalPanX:F2}, {finalPanY:F2})");
        }
        
        #endregion
        
        #region 框选交互（集成原有实现）
        
        /// <summary>
        /// 开始框选（包装原有方法，添加新的状态管理）
        /// </summary>
        private void StartBoxSelectionUnified(MouseButtonEventArgs e)
        {
            var startPoint = e.GetPosition(this);
            
            if (_mouseCaptureManager.TryCapture(this, "框选"))
            {
                _currentInteractionMode = InteractionMode.BoxSelecting;
                
                // 调用原有的框选开始方法
                StartBoxSelection(startPoint);
                
                System.Diagnostics.Debug.WriteLine("✅ [框选] 开始（统一管理）");
            }
        }
        
        /// <summary>
        /// 结束框选（包装原有方法，添加新的状态管理）
        /// </summary>
        private void EndBoxSelectionUnified()
        {
            _currentInteractionMode = InteractionMode.None;
            _mouseCaptureManager.Release();
            
            // 调用原有的框选结束方法
            EndBoxSelection();
            
            System.Diagnostics.Debug.WriteLine("✅ [框选] 结束（统一管理）");
        }
        
        #endregion
    }
}

