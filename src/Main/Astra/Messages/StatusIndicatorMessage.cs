using Astra;

namespace Astra.Messages
{
    /// <summary>
    /// 用于更新主界面状态指示灯的消息。发送此消息可刷新状态（颜色）与 ToolTip 文案。
    /// </summary>
    public class StatusIndicatorMessage
    {
        /// <summary>
        /// 软件状态，决定指示灯颜色（Running=绿、Connecting=青、Warning=橙、Error=红）。
        /// </summary>
        public SoftwareStatus Status { get; set; } = SoftwareStatus.Running;

        /// <summary>
        /// 要显示的状态文案（如 "软件运行中"、"正在连接..." ）。为 null 时保留当前文案。
        /// </summary>
        public string Text { get; set; }
    }
}
