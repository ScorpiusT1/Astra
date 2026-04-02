using System;
using System.Collections.Generic;
using System.Linq;
using Astra.Core.Nodes.Models;

namespace Astra.Plugins.Limits.Helpers
{
    /// <summary>
    /// 实测值变量名：界面不展示上游节点 Id 与 <c>Scalar.</c> 前缀；存储与运行仍为 <c>上游节点Id:Scalar.xxx</c>。
    /// </summary>
    internal static class LimitScalarKeyUiFormatter
    {
        /// <summary>去掉 <c>上游Id:</c> 与 <c>Scalar.</c>，仅保留逻辑名（如指标名与括号内通道标签）。</summary>
        public static string ToDisplay(string? storedFullKey)
        {
            if (string.IsNullOrWhiteSpace(storedFullKey))
                return string.Empty;
            var t = storedFullKey.Trim();
            var i = t.IndexOf(':');
            if (i > 0 && i < t.Length - 1)
                t = t.Substring(i + 1).Trim();
            if (t.StartsWith(NodeScalarOutputContracts.KeyPrefix, StringComparison.Ordinal))
                t = t.Substring(NodeScalarOutputContracts.KeyPrefix.Length);
            return t;
        }

        public static List<string> ToDisplayOptions(IEnumerable<string> fullKeys)
        {
            var list = fullKeys?.Where(k => !string.IsNullOrWhiteSpace(k)).Select(ToDisplay).Distinct(StringComparer.Ordinal)
                .OrderBy(s => s, StringComparer.Ordinal).ToList();
            return list ?? new List<string>();
        }

        /// <summary>
        /// 用户选择/输入 → 与可选完整键匹配后写入；已是完整键或全局变量名则保留。
        /// </summary>
        public static string ResolveToStoredKey(string? displayOrFull, IReadOnlyList<string> fullKeyOptions)
        {
            var v = displayOrFull?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(v))
                return string.Empty;

            if (fullKeyOptions.Contains(v, StringComparer.Ordinal))
                return v;

            // 用户可能粘贴「Scalar.xxx」或带节点 Id 的完整键；与 ToDisplay(full) 对齐后再匹配
            var normalized = v;
            if (normalized.StartsWith(NodeScalarOutputContracts.KeyPrefix, StringComparison.Ordinal))
                normalized = normalized.Substring(NodeScalarOutputContracts.KeyPrefix.Length);

            foreach (var full in fullKeyOptions)
            {
                if (string.Equals(ToDisplay(full), normalized, StringComparison.Ordinal))
                    return full;
            }

            // 手输或反序列化得到的完整限定键
            if (v.IndexOf(':') >= 0 && v.IndexOf(NodeScalarOutputContracts.KeyPrefix, StringComparison.Ordinal) >= 0)
                return v;

            return v;
        }
    }
}
