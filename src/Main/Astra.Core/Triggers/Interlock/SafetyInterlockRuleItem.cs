namespace Astra.Core.Triggers.Interlock
{
    /// <summary>
    /// 单条联锁规则：监控一个 IO 点（通常为 BOOL），在上升沿/下降沿或电平满足时触发动作。
    /// </summary>
    public sealed class SafetyInterlockRuleItem
    {
        /// <summary>显示名称（日志用）。</summary>
        public string RuleName { get; set; } = string.Empty;

        /// <summary>PLC 设备名称（与设备配置一致）。</summary>
        public string PlcDeviceName { get; set; } = string.Empty;

        /// <summary>IO 配置中的点位名称。</summary>
        public string IoPointName { get; set; } = string.Empty;

        /// <summary>当 IO 为 true 时执行的动作（如安全门打开、急停按下）。</summary>
        public InterlockRuleAction ActionOnTrue { get; set; } = InterlockRuleAction.None;

        /// <summary>当 IO 为 false 时执行的动作（如安全门关闭）。</summary>
        public InterlockRuleAction ActionOnFalse { get; set; } = InterlockRuleAction.None;

        /// <summary>
        /// 为 true 时仅在 IO 值<strong>变化</strong>时执行动作（推荐，避免每周期重复暂停）。
        /// 为 false 时按电平持续满足重复触发（一般不建议）。
        /// </summary>
        public bool EdgeTriggered { get; set; } = true;
    }
}
