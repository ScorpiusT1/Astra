using System;

namespace Astra.Core.Reporting
{
    /// <summary>
    /// 报告标题中「测试项/算法显示名」与总线 Preview 设备/通道段去重：名称里已有则不再单独插入中段。
    /// </summary>
    public static class ReportHeadingChannelMerge
    {
        /// <summary>
        /// 非空 <paramref name="deviceChannelPreview"/> 的每一段（按 <c>;</c> 分割）若长度 ≥ 2，均须在 <paramref name="itemDisplayName"/> 中出现（忽略大小写），才视为名称已含设备/通道信息。
        /// </summary>
        public static bool ItemDisplayNameAlreadyContainsDeviceChannelPreview(string? itemDisplayName, string? deviceChannelPreview)
        {
            if (string.IsNullOrWhiteSpace(itemDisplayName) || string.IsNullOrWhiteSpace(deviceChannelPreview))
                return false;

            var item = itemDisplayName.Trim();
            var meaningful = false;
            foreach (var part in deviceChannelPreview.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                var p = part.Trim();
                if (p.Length < 2)
                    continue;
                meaningful = true;
                if (item.IndexOf(p, StringComparison.OrdinalIgnoreCase) < 0)
                    return false;
            }

            return meaningful;
        }

        /// <summary>
        /// 拼装图表报告主标题：工况/流程段 - [可选设备/通道段] - 算法或图表显示名；若显示名已含 Preview 通道信息则省略设备/通道段（两段式）。
        /// </summary>
        public static string ComposeArtifactChartHeading(string? workFlowNameSegment, string? deviceChannelPreview, string? algorithmDisplayLabel)
        {
            static string Seg(string? value) =>
                string.IsNullOrWhiteSpace(value) ? "-" : value.Trim();

            var wf = Seg(workFlowNameSegment);
            var algo = Seg(algorithmDisplayLabel);

            var devRaw = deviceChannelPreview?.Trim();
            if (string.IsNullOrEmpty(devRaw))
                return $"{wf}-{Seg(null)}-{algo}";

            if (ItemDisplayNameAlreadyContainsDeviceChannelPreview(algorithmDisplayLabel, devRaw))
                return $"{wf}-{algo}";

            return $"{wf}-{Seg(devRaw)}-{algo}";
        }
    }
}
