using System;
using System.Collections.Generic;
using System.Linq;
using Astra.Core.Nodes.Models;

namespace Astra.Core.Data
{
    /// <summary>
    /// 统一的 Raw 上游生产者查找：沿工作流连线 BFS 向上游搜索
    /// 实现 <see cref="IRawDataPipelineNode"/> 或 <see cref="IMultiRawDataPipelineNode"/> 且设备名匹配的节点。
    /// 取代各插件中重复的 TryFindUpstreamRawProducerNodeId 逻辑。
    /// </summary>
    public static class RawDataPipelineResolver
    {
        /// <summary>
        /// 从 <paramref name="targetNodeId"/> 出发，沿连线向上游 BFS，
        /// 找到第一个与 <paramref name="deviceDisplayName"/> 匹配的 Raw 生产者节点 ID。
        /// </summary>
        public static bool TryFindUpstreamRawProducerNodeId(
            WorkFlowNode workflow,
            string targetNodeId,
            string deviceDisplayName,
            out string producerNodeId)
        {
            producerNodeId = string.Empty;
            if (workflow == null || string.IsNullOrEmpty(targetNodeId) || string.IsNullOrEmpty(deviceDisplayName))
                return false;

            var visited = new HashSet<string>(StringComparer.Ordinal);
            var queue = new Queue<string>();

            foreach (var c in workflow.Connections ?? Enumerable.Empty<Connection>())
            {
                if (c.TargetNodeId == targetNodeId)
                    queue.Enqueue(c.SourceNodeId);
            }

            while (queue.Count > 0)
            {
                var nid = queue.Dequeue();
                if (!visited.Add(nid))
                    continue;

                var node = workflow.Nodes?.FirstOrDefault(n => n.Id == nid);

                if (node is IRawDataPipelineNode pipe &&
                    string.Equals(pipe.DataAcquisitionDeviceDisplayName?.Trim(), deviceDisplayName, StringComparison.OrdinalIgnoreCase))
                {
                    producerNodeId = nid;
                    return true;
                }

                if (node is IMultiRawDataPipelineNode multi)
                {
                    var names = multi.DataAcquisitionDeviceDisplayNames;
                    if (names != null && names.Any(x =>
                        string.Equals(x?.Trim(), deviceDisplayName, StringComparison.OrdinalIgnoreCase)))
                    {
                        producerNodeId = nid;
                        return true;
                    }
                }

                foreach (var c in workflow.Connections ?? Enumerable.Empty<Connection>())
                {
                    if (c.TargetNodeId == nid)
                        queue.Enqueue(c.SourceNodeId);
                }
            }

            return false;
        }
    }
}
