using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Astra.Core.Nodes.Models;

namespace Astra.Core.Reporting
{
    /// <summary>
    /// 设计期：由多流程编辑器刷新，汇总全部子流程画布节点，供结果归档节点白名单下拉使用。
    /// </summary>
    public static class WorkflowArchivePeerCatalog
    {
        private static readonly object Gate = new();

        private static string _fingerprint = string.Empty;

        private static List<ReportNodePickEntry> _entries = new();

        /// <summary>
        /// 用当前工程内全部子流程标签页的节点快照刷新目录。
        /// </summary>
        public static void Refresh(IReadOnlyList<(string SubFlowDisplayName, string SubFlowId, IReadOnlyList<Node> Nodes)> subFlows)
        {
            lock (Gate)
            {
                var sb = new StringBuilder();
                var list = new List<ReportNodePickEntry>();
                foreach (var (subFlowDisplayName, subFlowId, nodes) in subFlows)
                {
                    var flowLabel = string.IsNullOrWhiteSpace(subFlowDisplayName)
                        ? (string.IsNullOrWhiteSpace(subFlowId) ? "子流程" : subFlowId)
                        : subFlowDisplayName.Trim();

                    foreach (var n in nodes)
                    {
                        if (n == null || n is WorkFlowNode)
                            continue;

                        var cat = ReportWhitelistNodeCategories.FromNode(n);
                        if (cat == ReportWhitelistCategories.None)
                            continue;

                        sb.Append(subFlowId).Append('\u001f')
                            .Append(n.Id).Append('\u001f')
                            .Append(n.Name).Append('\u001f')
                            .Append(n.NodeType).Append('\u001f')
                            .Append((byte)cat).Append('|');

                        var label = $"{flowLabel} / {ReportNodePickDisplay.FormatNodeLabel(n)}";
                        list.Add(new ReportNodePickEntry(n.Id, label, cat));
                    }
                }

                _fingerprint = sb.ToString();
                _entries = list
                    .OrderBy(e => e.DisplayName, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(e => e.NodeId, StringComparer.Ordinal)
                    .ToList();
            }
        }

        public static void Clear()
        {
            lock (Gate)
            {
                _fingerprint = string.Empty;
                _entries = new List<ReportNodePickEntry>();
            }
        }

        public static string GetFingerprint()
        {
            lock (Gate)
                return _fingerprint;
        }

        public static IReadOnlyList<ReportNodePickEntry> GetEntries()
        {
            lock (Gate)
                return _entries;
        }
    }
}
