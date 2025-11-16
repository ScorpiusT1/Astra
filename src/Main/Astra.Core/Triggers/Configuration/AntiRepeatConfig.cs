namespace Astra.Core.Triggers.Configuration
{
    /// <summary>
    /// 防重复触发配置
    /// </summary>
    public class AntiRepeatConfig
    {
        /// <summary>是否启用防重复</summary>
        public bool Enabled { get; set; } = true;

        /// <summary>相同SN最小间隔（毫秒）</summary>
        public int MinIntervalMs { get; set; } = 3000;

        /// <summary>全局最小触发间隔（毫秒）</summary>
        public int GlobalMinIntervalMs { get; set; } = 100;
    }
}
