using Astra.Core.Nodes.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Astra.Engine.Execution.Strategies
{
    /// <summary>
    /// 工作流依赖图上的可达性（用于「失败不释放边」时标记下游不应执行等场景）。
    /// </summary>
    internal static class WorkflowDependencyTraversal
    {
        public static Dictionary<string, List<string>> BuildForwardAdjacency(WorkFlowNode workflow)
        {
            var enabledIds = new HashSet<string>(
                (workflow.Nodes ?? Enumerable.Empty<Node>()).Where(n => n != null && n.IsEnabled).Select(n => n.Id),
                StringComparer.Ordinal);

            var graph = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (var id in enabledIds)
            {
                graph[id] = new List<string>();
            }

            foreach (var c in workflow.Connections ?? new List<Connection>())
            {
                if (c == null)
                {
                    continue;
                }

                if (!enabledIds.Contains(c.SourceNodeId) || !enabledIds.Contains(c.TargetNodeId))
                {
                    continue;
                }

                graph[c.SourceNodeId].Add(c.TargetNodeId);
            }

            return graph;
        }

        /// <summary>
        /// 从 sourceNodeId 沿有向边正向可达的所有节点（不含自身）。
        /// </summary>
        public static HashSet<string> CollectDescendants(Dictionary<string, List<string>> forward, string sourceNodeId)
        {
            var set = new HashSet<string>(StringComparer.Ordinal);
            if (string.IsNullOrEmpty(sourceNodeId) || forward == null || !forward.ContainsKey(sourceNodeId))
            {
                return set;
            }

            var q = new Queue<string>();
            q.Enqueue(sourceNodeId);
            while (q.Count > 0)
            {
                var u = q.Dequeue();
                if (!forward.TryGetValue(u, out var outs))
                {
                    continue;
                }

                foreach (var v in outs)
                {
                    if (set.Add(v))
                    {
                        q.Enqueue(v);
                    }
                }
            }

            set.Remove(sourceNodeId);
            return set;
        }
    }
}
