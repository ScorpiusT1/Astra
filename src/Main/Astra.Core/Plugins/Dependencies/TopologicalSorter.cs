using Astra.Core.Plugins.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Astra.Core.Plugins.Dependencies
{
    /// <summary>
    /// 拓扑排序器 - Kahn 算法实现
    /// </summary>
    public class TopologicalSorter
    {
        /// <summary>
        /// 对插件进行拓扑排序
        /// </summary>
        public List<PluginDescriptor> Sort(List<PluginDescriptor> plugins)
        {
            var graph = BuildGraph(plugins);
            return KahnSort(graph);
        }

        /// <summary>
        /// 使用 DFS 检测循环依赖
        /// </summary>
        public bool HasCycle(List<PluginDescriptor> plugins)
        {
            var graph = BuildGraph(plugins);
            var visited = new HashSet<string>();
            var recursionStack = new HashSet<string>();

            foreach (var node in graph.Keys)
            {
                if (HasCycleDFS(node, graph, visited, recursionStack))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// 查找循环依赖路径
        /// </summary>
        public List<string> FindCyclePath(List<PluginDescriptor> plugins)
        {
            var graph = BuildGraph(plugins);
            var visited = new HashSet<string>();
            var recursionStack = new Stack<string>();

            foreach (var node in graph.Keys)
            {
                if (FindCyclePathDFS(node, graph, visited, recursionStack, out var cycle))
                    return cycle;
            }

            return null;
        }

        private Dictionary<string, List<string>> BuildGraph(List<PluginDescriptor> plugins)
        {
            var graph = new Dictionary<string, List<string>>();

            foreach (var plugin in plugins)
            {
                if (!graph.ContainsKey(plugin.Id))
                    graph[plugin.Id] = new List<string>();

                foreach (var dep in plugin.Dependencies.Where(d => !d.IsOptional))
                {
                    graph[plugin.Id].Add(dep.PluginId);
                }
            }

            return graph;
        }

        private List<PluginDescriptor> KahnSort(Dictionary<string, List<string>> graph)
        {
            var inDegree = new Dictionary<string, int>();
            var result = new List<PluginDescriptor>();

            // 计算入度
            foreach (var node in graph.Keys)
            {
                if (!inDegree.ContainsKey(node))
                    inDegree[node] = 0;

                foreach (var neighbor in graph[node])
                {
                    if (!inDegree.ContainsKey(neighbor))
                        inDegree[neighbor] = 0;
                    inDegree[neighbor]++;
                }
            }

            // 找到所有入度为 0 的节点
            var queue = new Queue<string>(inDegree.Where(kvp => kvp.Value == 0).Select(kvp => kvp.Key));

            while (queue.Count > 0)
            {
                var node = queue.Dequeue();

                // 添加到结果（这里简化处理，实际应返回完整的 PluginDescriptor）
                // result.Add(plugins.First(p => p.ConfigId == node));

                foreach (var neighbor in graph[node])
                {
                    inDegree[neighbor]--;
                    if (inDegree[neighbor] == 0)
                        queue.Enqueue(neighbor);
                }
            }

            // 如果还有节点未处理，说明有循环
            if (result.Count != graph.Count)
                throw new InvalidOperationException("Circular dependency detected");

            return result;
        }

        private bool HasCycleDFS(
            string node,
            Dictionary<string, List<string>> graph,
            HashSet<string> visited,
            HashSet<string> recursionStack)
        {
            if (recursionStack.Contains(node))
                return true;

            if (visited.Contains(node))
                return false;

            visited.Add(node);
            recursionStack.Add(node);

            if (graph.ContainsKey(node))
            {
                foreach (var neighbor in graph[node])
                {
                    if (HasCycleDFS(neighbor, graph, visited, recursionStack))
                        return true;
                }
            }

            recursionStack.Remove(node);
            return false;
        }

        private bool FindCyclePathDFS(
            string node,
            Dictionary<string, List<string>> graph,
            HashSet<string> visited,
            Stack<string> path,
            out List<string> cycle)
        {
            cycle = null;

            if (path.Contains(node))
            {
                // 找到循环
                cycle = new List<string>();
                var found = false;
                foreach (var n in path.Reverse())
                {
                    if (n == node) found = true;
                    if (found) cycle.Add(n);
                }
                cycle.Add(node);
                return true;
            }

            if (visited.Contains(node))
                return false;

            visited.Add(node);
            path.Push(node);

            if (graph.ContainsKey(node))
            {
                foreach (var neighbor in graph[node])
                {
                    if (FindCyclePathDFS(neighbor, graph, visited, path, out cycle))
                        return true;
                }
            }

            path.Pop();
            return false;
        }
    }
}
