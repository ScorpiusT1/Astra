using System;
using System.Collections.Generic;
using System.Linq;
using Astra.Core.Nodes.Models;

namespace Astra.Plugins.Limits.Helpers
{
    /// <summary>
    /// 从上游算法等节点的设计期标量键（<c>节点Id:Scalar.指标名(通道标签)</c>）解析通道标签，
    /// 与 <see cref="LimitScalarKeyFilter"/> 中按括号内标签过滤的规则一致。
    /// 用于仅连接标量上游、未直连 Raw 数据源节点时的「通道」下拉。
    /// </summary>
    internal static class LimitScalarKeyChannelExtractor
    {
        public static List<string> ExtractQualifiedChannelLabels(IEnumerable<string>? scalarInputKeys)
        {
            var set = new HashSet<string>(StringComparer.Ordinal);
            foreach (var key in scalarInputKeys ?? Enumerable.Empty<string>())
            {
                if (TryExtractChannelLabel(key, out var label) && !string.IsNullOrWhiteSpace(label))
                    set.Add(label.Trim());
            }

            return set.OrderBy(s => s, StringComparer.Ordinal).ToList();
        }

        private static bool TryExtractChannelLabel(string fullKey, out string label)
        {
            label = string.Empty;
            if (string.IsNullOrEmpty(fullKey))
                return false;

            var scalarIdx = fullKey.IndexOf(NodeScalarOutputContracts.KeyPrefix, StringComparison.Ordinal);
            if (scalarIdx < 0)
                return false;

            var logical = fullKey.Substring(scalarIdx + NodeScalarOutputContracts.KeyPrefix.Length);
            var open = logical.LastIndexOf('(');
            if (open < 0)
                return false;
            var close = logical.LastIndexOf(')');
            if (close <= open)
                return false;
            label = logical.Substring(open + 1, close - open - 1).Trim();
            return label.Length > 0;
        }
    }
}
