using System;
using System.Windows;

namespace Astra.UI.Interaction
{
    /// <summary>
    /// 鼠标捕获管理器
    /// 职责：统一管理鼠标捕获，防止多个交互冲突
    /// </summary>
    public class MouseCaptureManager
    {
        #region 私有字段
        
        private FrameworkElement _currentOwner;
        private string _captureReason;
        
        #endregion
        
        #region 属性
        
        /// <summary>
        /// 是否正在捕获鼠标
        /// </summary>
        public bool IsCapturing => _currentOwner != null;
        
        /// <summary>
        /// 当前捕获原因
        /// </summary>
        public string CurrentReason => _captureReason;
        
        /// <summary>
        /// 当前捕获的控件
        /// </summary>
        public FrameworkElement CurrentOwner => _currentOwner;
        
        #endregion
        
        #region 公共方法
        
        /// <summary>
        /// 尝试捕获鼠标
        /// </summary>
        /// <param name="element">要捕获鼠标的控件</param>
        /// <param name="reason">捕获原因（用于调试）</param>
        /// <returns>是否成功捕获</returns>
        public bool TryCapture(FrameworkElement element, string reason)
        {
            if (element == null)
            {
                System.Diagnostics.Debug.WriteLine("❌ [鼠标捕获] 失败：element 为 null");
                return false;
            }
            
            // 如果已经被其他控件捕获
            if (_currentOwner != null && _currentOwner != element)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"❌ [鼠标捕获] 失败：已被 '{_captureReason}' 占用");
                return false;
            }
            
            // 如果是同一个控件重复捕获，直接返回成功
            if (_currentOwner == element)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"✅ [鼠标捕获] 已被同一控件捕获：{reason}");
                return true;
            }
            
            // 尝试捕获
            if (element.CaptureMouse())
            {
                _currentOwner = element;
                _captureReason = reason;
                System.Diagnostics.Debug.WriteLine(
                    $"✅ [鼠标捕获] 成功：{reason}");
                return true;
            }
            
            System.Diagnostics.Debug.WriteLine(
                $"❌ [鼠标捕获] 系统拒绝（元素可能不可见或被禁用）");
            return false;
        }
        
        /// <summary>
        /// 释放鼠标捕获
        /// </summary>
        public void Release()
        {
            if (_currentOwner != null)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"🔓 [鼠标捕获] 释放：{_captureReason}");
                
                _currentOwner.ReleaseMouseCapture();
                _currentOwner = null;
                _captureReason = null;
            }
        }
        
        /// <summary>
        /// 强制释放（即使不是当前所有者）
        /// </summary>
        public void ForceRelease()
        {
            if (_currentOwner != null)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"⚠️ [鼠标捕获] 强制释放：{_captureReason}");
                
                try
                {
                    _currentOwner.ReleaseMouseCapture();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"❌ [鼠标捕获] 释放失败：{ex.Message}");
                }
                finally
                {
                    _currentOwner = null;
                    _captureReason = null;
                }
            }
        }
        
        #endregion
    }
}

