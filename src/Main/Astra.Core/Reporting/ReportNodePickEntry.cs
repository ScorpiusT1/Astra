using System;
using Newtonsoft.Json;

namespace Astra.Core.Reporting
{
    /// <summary>
    /// 报告节点白名单项：持久化 <see cref="NodeId"/>，界面展示 <see cref="DisplayName"/>。
    /// </summary>
    public sealed class ReportNodePickEntry : IEquatable<ReportNodePickEntry>
    {
        public ReportNodePickEntry()
        {
        }

        public ReportNodePickEntry(string nodeId, string displayName)
        {
            NodeId = nodeId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            WhitelistCategories = ReportWhitelistCategories.LegacyShowInAll;
        }

        public ReportNodePickEntry(string nodeId, string displayName, ReportWhitelistCategories categories)
        {
            NodeId = nodeId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            WhitelistCategories = categories;
        }

        public string NodeId { get; set; } = string.Empty;

        public string DisplayName { get; set; } = string.Empty;

        /// <summary>设计期目录项分类；旧数据未设置时视为 <see cref="ReportWhitelistCategories.LegacyShowInAll"/>。</summary>
        [JsonIgnore]
        public ReportWhitelistCategories WhitelistCategories { get; set; } = ReportWhitelistCategories.LegacyShowInAll;

        public bool Equals(ReportNodePickEntry? other) =>
            other != null && string.Equals(NodeId, other.NodeId, StringComparison.Ordinal);

        public override bool Equals(object? obj) => Equals(obj as ReportNodePickEntry);

        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(NodeId ?? string.Empty);
    }
}
