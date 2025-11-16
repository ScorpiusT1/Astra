namespace Astra.Core.Triggers.Enums
{
    /// <summary>
    /// 触发源类型
    /// </summary>
    public enum TriggerSource
    {
        /// <summary>手动扫码</summary>
        ManualScan,
        /// <summary>自动扫码</summary>
        AutoScan,
        /// <summary>PLC监控</summary>
        PLCMonitor,
        /// <summary>网络API</summary>
        NetworkAPI,
        /// <summary>定时器</summary>
        Timer,
        /// <summary>其他</summary>
        Other
    }
}
