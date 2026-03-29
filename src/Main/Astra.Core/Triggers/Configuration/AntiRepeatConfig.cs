namespace Astra.Core.Triggers.Configuration
{
    /// <summary>
    /// 防重复触发配置：启用后，在最小间隔内对相同信号（如同一 SN）的重复触发予以抑制。
    /// </summary>
    public class AntiRepeatConfig
    {
        /// <summary>是否启用防重复（false 则允许连续重复触发）</summary>
        public bool Enabled { get; set; } = true;

        /// <summary>相同信号最小间隔（毫秒），例如同一条码、同一 PLC 边沿信号在短时间内只响应一次</summary>
        public int MinIntervalMs { get; set; } = 3000;

        /// <summary>
        /// 全局最小触发间隔（毫秒）。0 表示不限制；非 UI 常用项，默认 0 与「仅限制相同信号间隔」一致。
        /// </summary>
        public int GlobalMinIntervalMs { get; set; }
    }
}
