using Astra.Core.Triggers.Args;

namespace Astra.Core.Triggers
{
    #region ========== 触发器基类 ==========

    #endregion

    #region ========== 观察者接口 ==========

    /// <summary>
    /// 触发器观察者接口（测试流程）
    /// </summary>
    public interface ITriggerObserver
    {
        /// <summary>
        /// 处理触发事件
        /// </summary>
        Task HandleTriggerAsync(TriggerEventArgs args);
    }

    #endregion  
}
