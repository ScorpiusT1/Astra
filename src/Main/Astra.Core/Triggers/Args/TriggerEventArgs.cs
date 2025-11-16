using Astra.Core.Triggers.Enums;

namespace Astra.Core.Triggers.Args
{

    #region ========== 事件和委托定义 ==========

    /// <summary>
    /// 触发事件参数（改进版 - SN 在 AdditionalData 中）
    /// </summary>
    public class TriggerEventArgs : EventArgs
    {
        /// <summary>
        /// 触发源类型
        /// </summary>
        public TriggerSource Source { get; set; }

        /// <summary>
        /// 触发时间
        /// </summary>
        public DateTime TriggerTime { get; set; }

        /// <summary>
        /// 额外数据（包含 SN、TriggerId、TriggerName 等）
        /// </summary>
        public Dictionary<string, object> AdditionalData { get; set; }

        /// <summary>
        /// 构造函数
        /// </summary>
        public TriggerEventArgs(TriggerSource source)
        {
            Source = source;
            TriggerTime = DateTime.Now;
            AdditionalData = new Dictionary<string, object>();
        }

        /// <summary>
        /// 便捷方法：获取 SN
        /// </summary>
        public string GetSN()
        {
            return AdditionalData.TryGetValue("SN", out var sn) ? sn?.ToString() : null;
        }

        /// <summary>
        /// 便捷方法：设置 SN
        /// </summary>
        public void SetSN(string sn)
        {
            AdditionalData["SN"] = sn;
        }

        /// <summary>
        /// 便捷方法：获取触发器ID
        /// </summary>
        public string GetTriggerId()
        {
            return AdditionalData.TryGetValue("TriggerId", out var id) ? id?.ToString() : null;
        }

        /// <summary>
        /// 便捷方法：获取触发器名称
        /// </summary>
        public string GetTriggerName()
        {
            return AdditionalData.TryGetValue("TriggerName", out var name) ? name?.ToString() : null;
        }
    }

    #endregion
}
