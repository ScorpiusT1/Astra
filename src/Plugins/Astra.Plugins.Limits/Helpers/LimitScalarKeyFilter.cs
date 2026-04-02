using System;
using System.Collections.Generic;
using System.Linq;

namespace Astra.Plugins.Limits.Helpers
{
    /// <summary>
    /// 按曲线/主页通道选择过滤上游标量键；键中含逻辑名括号内的「设备/通道」标签。
    /// </summary>
    internal static class LimitScalarKeyFilter
    {
        /// <summary>
        /// 未选通道或「未选择」时返回全部；否则仅保留逻辑名中含 <c>(通道标签)</c> 的键（与算法 <see cref="Astra.Plugins.Algorithms.Nodes.AlgorithmNodeBase"/> 中 label 一致）。
        /// </summary>
        public static IEnumerable<string> FilterByCurveChannel(IEnumerable<string> allKeys, string? curveChannelSelection)
        {
            var list = allKeys?.Where(k => !string.IsNullOrWhiteSpace(k)).Distinct(StringComparer.Ordinal).ToList()
                       ?? new List<string>();
            if (list.Count == 0)
                return list;

            var sel = curveChannelSelection?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(sel) ||
                string.Equals(sel, LimitsDesignTimeOptions.UnselectedLabel, StringComparison.Ordinal))
                return list.OrderBy(s => s, StringComparer.Ordinal);

            var needle = $"({sel})";
            var filtered = list.Where(k => k.IndexOf(needle, StringComparison.Ordinal) >= 0).ToList();
            if (filtered.Count == 0)
                return Enumerable.Empty<string>();

            return filtered.OrderBy(s => s, StringComparer.Ordinal);
        }
    }
}
