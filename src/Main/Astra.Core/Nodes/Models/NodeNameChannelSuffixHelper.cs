using System;
using System.Collections.Generic;
using System.Linq;

namespace Astra.Core.Nodes.Models
{
    /// <summary>
    /// 节点标题上自动追加「设备/通道」说明（与手动重命名共存：通过记录的自动段剥离后再拼接）。
    /// </summary>
    public static class NodeNameChannelSuffixHelper
    {
        public const string Separator = " - ";

        /// <summary>
        /// 由多选「设备名/通道名」列表生成后缀片段（已排序、去重）；无合法项时返回空串。
        /// </summary>
        public static string BuildMultiQualifiedChannelSuffix(IEnumerable<string>? qualifiedSelections)
        {
            if (qualifiedSelections == null)
                return "";

            var parts = qualifiedSelections
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s.Trim())
                .Where(s => QualifiedChannelHelper.TrySplit(s, out _, out _))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return parts.Count == 0 ? "" : string.Join("; ", parts);
        }

        /// <summary>
        /// 卡控/播放等单选场景：内部已存储的通道或设备字符串直接作为后缀（trim 后）。
        /// </summary>
        public static string BuildSingleSelectionSuffix(string? storedValue)
        {
            return string.IsNullOrWhiteSpace(storedValue) ? "" : storedValue.Trim();
        }

        public static string StripTrackedSuffix(string? currentName, string? trackedSuffixFragment)
        {
            var n = currentName ?? "";
            if (string.IsNullOrEmpty(trackedSuffixFragment))
                return n;

            var tail = Separator + trackedSuffixFragment;
            return n.EndsWith(tail, StringComparison.Ordinal) ? n[..^tail.Length] : n;
        }

        public static string ComposeWithAutoSuffix(string baseName, string suffixFragment)
        {
            var s = suffixFragment ?? "";
            if (string.IsNullOrEmpty(s))
                return baseName ?? "";
            return (baseName ?? "") + Separator + s;
        }

        /// <summary>
        /// 反序列化后「自动通道后缀」未持久化时，用当前 <paramref name="channelNames"/> 重建标题后缀。
        /// 基标题取首个 <see cref="Separator"/> 之前（视为用户/默认节点名）；其后无论多长（含历史错误叠层、或「垃圾前缀 + 正确尾部」）一律丢弃。
        /// 不能仅用 <c>EndsWith(Separator + frag)</c> 跳过重建，否则末尾已是当前通道但前面仍残留多轮追加的长串。
        /// </summary>
        public static (string? TrackedSuffix, string? RecomposedFullName) ReconcileAutoSuffixAfterDeserialization(
            string? currentName,
            string defaultDisplayName,
            IEnumerable<string>? channelNames)
        {
            var frag = BuildMultiQualifiedChannelSuffix(channelNames);
            if (string.IsNullOrEmpty(frag))
                return (null, null);

            string basePart;
            if (string.IsNullOrWhiteSpace(currentName))
                basePart = defaultDisplayName ?? "";
            else
            {
                var idx = currentName.IndexOf(Separator, StringComparison.Ordinal);
                basePart = idx >= 0 ? currentName[..idx].TrimEnd() : currentName.TrimEnd();
                if (string.IsNullOrEmpty(basePart))
                    basePart = defaultDisplayName ?? "";
            }

            return (frag, ComposeWithAutoSuffix(basePart, frag));
        }
    }
}
