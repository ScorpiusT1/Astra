using System;

namespace Astra.Core.Nodes.Models
{
    /// <summary>
    /// 「设备显示名/通道名」格式的解析（算法与卡控节点统一使用）。
    /// </summary>
    public static class QualifiedChannelHelper
    {
        /// <summary>
        /// 将 <c>设备/通道</c> 拆为两段；无合法斜杠时返回 false。
        /// </summary>
        public static bool TrySplit(string? qualified, out string deviceDisplayName, out string channelName)
        {
            deviceDisplayName = string.Empty;
            channelName = string.Empty;
            if (string.IsNullOrWhiteSpace(qualified))
                return false;

            var t = qualified.Trim();
            var idx = t.IndexOf('/');
            if (idx <= 0 || idx >= t.Length - 1)
                return false;

            deviceDisplayName = t.Substring(0, idx).Trim();
            channelName = t.Substring(idx + 1).Trim();
            return deviceDisplayName.Length > 0 && channelName.Length > 0;
        }
    }
}
