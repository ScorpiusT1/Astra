using System;
using System.Collections.Generic;
using Astra.Core.Nodes.Models;
using Astra.UI.Abstractions.Nodes;

namespace Astra.Plugins.Limits.Helpers
{
    /// <summary>
    /// 断开连线时按 <c>Edge.SourceNodeId</c> 从下游登记列表中移除（源节点已删时画布传入的源实例可能为 null，不能依赖引用相等）。
    /// </summary>
    internal static class LimitUpstreamDetachHelper
    {
        public static void RemoveUpstreamForDetachedEdge(
            Edge edge,
            List<IDesignTimeDataSourceInfo> sources,
            List<IDesignTimeScalarOutputProvider> scalars)
        {
            if (string.IsNullOrEmpty(edge.SourceNodeId))
                return;
            var sid = edge.SourceNodeId;
            scalars.RemoveAll(p => string.Equals(p.ProviderNodeId, sid, StringComparison.Ordinal));
            sources.RemoveAll(s => s is Node n && string.Equals(n.Id, sid, StringComparison.Ordinal));
        }
    }
}
