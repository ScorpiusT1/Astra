using Astra.Core.Nodes.Models;
using System.Collections.Generic;
using System.Linq;

namespace Astra.Engine.Execution.Strategies
{
    /// <summary>
    /// 图分析器
    /// 用于分析工作流的图结构，检测循环依赖和执行顺序
    /// </summary>
    public class GraphAnalyzer
    {
        private readonly WorkFlowNode _workflow;
        private readonly HashSet<string>? _mainPhaseOnlyIds;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="workflow">要分析的工作流</param>
        /// <param name="mainPhaseOnlyIds">非 null 时仅分析主执行阶段节点（排除「最后执行」节点）之间的子图</param>
        public GraphAnalyzer(WorkFlowNode workflow, HashSet<string>? mainPhaseOnlyIds = null)
        {
            _workflow = workflow;
            _mainPhaseOnlyIds = mainPhaseOnlyIds;
        }

        /// <summary>
        /// 拓扑排序
        /// 返回节点的执行顺序，如果存在循环依赖则返回null
        /// </summary>
        /// <returns>排序后的节点列表，如果存在循环依赖则返回null</returns>
        public List<Node> TopologicalSort()
        {
            var enabledNodes = GetEnabledNodesForAnalysis();
            var inDegree = new Dictionary<string, int>();
            var graph = new Dictionary<string, List<string>>();

            foreach (var node in enabledNodes)
            {
                inDegree[node.Id] = 0;
                graph[node.Id] = new List<string>();
            }

            // 拓扑依赖：无论是 Flow 还是 Data 连接，只要存在连接关系就参与顺序约束
            foreach (var conn in _workflow.Connections.Where(c => c != null))
            {
                if (_mainPhaseOnlyIds != null &&
                    (!_mainPhaseOnlyIds.Contains(conn.SourceNodeId) || !_mainPhaseOnlyIds.Contains(conn.TargetNodeId)))
                {
                    continue;
                }

                if (!graph.ContainsKey(conn.SourceNodeId) || !graph.ContainsKey(conn.TargetNodeId))
                    continue;

                graph[conn.SourceNodeId].Add(conn.TargetNodeId);
                inDegree[conn.TargetNodeId]++;
            }

            var queue = new Queue<string>(inDegree.Where(kvp => kvp.Value == 0).Select(kvp => kvp.Key));
            var result = new List<Node>();

            while (queue.Count > 0)
            {
                var nodeId = queue.Dequeue();
                var node = _workflow.GetNode(nodeId);
                if (node != null && node.IsEnabled)
                {
                    result.Add(node);
                }

                foreach (var neighbor in graph[nodeId])
                {
                    inDegree[neighbor]--;
                    if (inDegree[neighbor] == 0)
                    {
                        queue.Enqueue(neighbor);
                    }
                }
            }

            return result.Count == enabledNodes.Count ? result : null;
        }

        /// <summary>
        /// 检测是否存在循环依赖
        /// </summary>
        /// <returns>如果存在循环依赖返回true，否则返回false</returns>
        public bool HasCycle()
        {
            return TopologicalSort() == null;
        }

        private List<Node> GetEnabledNodesForAnalysis()
        {
            var q = _workflow.Nodes.Where(n => n.IsEnabled);
            if (_mainPhaseOnlyIds != null)
            {
                q = q.Where(n => _mainPhaseOnlyIds.Contains(n.Id));
            }

            return q.ToList();
        }
    }
}

