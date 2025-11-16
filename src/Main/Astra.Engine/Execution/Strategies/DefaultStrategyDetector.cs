using Astra.Core.Nodes.Models;
using Astra.Engine.Execution.WorkFlowEngine;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Astra.Engine.Execution.Strategies
{
    /// <summary>
    /// 默认策略检测器
    /// 分析工作流结构，自动检测最适合的执行策略
    /// </summary>
    public class DefaultStrategyDetector : IStrategyDetector
    {
        /// <summary>
        /// 检测工作流的最佳执行策略
        /// </summary>
        public DetectedExecutionStrategy Detect(WorkFlowNode workflow)
        {
            var enabledNodes = workflow.Nodes.Where(n => n.IsEnabled).ToList();

            if (enabledNodes.Count == 0)
            {
                return new DetectedExecutionStrategy
                {
                    Type = ExecutionStrategyType.Sequential,
                    Description = "无启用节点",
                    Reason = "节点列表为空"
                };
            }

            // 1. 无连接 → 并行
            if (workflow.Connections.Count == 0)
            {
                return new DetectedExecutionStrategy
                {
                    Type = ExecutionStrategyType.Parallel,
                    Description = $"并行执行 {enabledNodes.Count} 个独立节点",
                    Reason = "无连接关系，所有节点相互独立",
                    Nodes = enabledNodes,
                    ExpectedParallelism = Math.Min(enabledNodes.Count, workflow.Configuration.MaxParallelism)
                };
            }

            // 2. 简单线性序列
            if (IsSimpleSequence(workflow, out var sequencedNodes))
            {
                return new DetectedExecutionStrategy
                {
                    Type = ExecutionStrategyType.Sequential,
                    Description = $"顺序执行 {sequencedNodes.Count} 个节点的线性序列",
                    Reason = "检测到简单的线性连接结构",
                    Nodes = sequencedNodes
                };
            }

            // 3. 部分并行（分层）
            if (DetectPartiallyParallel(workflow, out var parallelGroups, out var dependencies))
            {
                return new DetectedExecutionStrategy
                {
                    Type = ExecutionStrategyType.PartiallyParallel,
                    Description = $"分层并行执行，共 {parallelGroups.Count} 层",
                    Reason = "检测到可以分层并行的结构",
                    ParallelGroups = parallelGroups,
                    Dependencies = dependencies
                };
            }

            // 4. 复杂图
            var graphAnalyzer = new GraphAnalyzer(workflow);

            if (graphAnalyzer.HasCycle())
            {
                return new DetectedExecutionStrategy
                {
                    Type = ExecutionStrategyType.ComplexGraph,
                    Description = "检测到循环依赖，无法执行",
                    Reason = "存在循环依赖",
                    HasCycle = true
                };
            }

            var sortedNodes = graphAnalyzer.TopologicalSort();
            return new DetectedExecutionStrategy
            {
                Type = ExecutionStrategyType.ComplexGraph,
                Description = $"复杂图执行，拓扑排序后按序执行 {sortedNodes.Count} 个节点",
                Reason = "复杂的依赖关系，使用拓扑排序",
                Nodes = sortedNodes
            };
        }

        /// <summary>
        /// 检测是否为简单线性序列
        /// </summary>
        private bool IsSimpleSequence(WorkFlowNode workflow, out List<Node> sequencedNodes)
        {
            sequencedNodes = new List<Node>();
            var enabledNodes = workflow.Nodes.Where(n => n.IsEnabled).ToList();

            if (workflow.Connections.Count != enabledNodes.Count - 1)
                return false;

            foreach (var node in enabledNodes)
            {
                var inputCount = workflow.GetInputConnections(node.Id).Count;
                var outputCount = workflow.GetOutputConnections(node.Id).Count;

                if (!((inputCount == 0 && outputCount == 1) ||
                      (inputCount == 1 && outputCount == 1) ||
                      (inputCount == 1 && outputCount == 0)))
                {
                    return false;
                }
            }

            var startNode = enabledNodes.FirstOrDefault(n => workflow.GetInputConnections(n.Id).Count == 0);
            if (startNode == null) return false;

            var current = startNode;
            sequencedNodes.Add(current);

            while (true)
            {
                var nextConnections = workflow.GetOutputConnections(current.Id);
                if (nextConnections.Count == 0) break;
                if (nextConnections.Count > 1) return false;

                var nextNode = workflow.GetNode(nextConnections[0].TargetNodeId);
                if (nextNode == null || !nextNode.IsEnabled) return false;

                sequencedNodes.Add(nextNode);
                current = nextNode;
            }

            return sequencedNodes.Count == enabledNodes.Count;
        }

        /// <summary>
        /// 检测部分并行结构
        /// </summary>
        private bool DetectPartiallyParallel(WorkFlowNode workflow, out List<List<Node>> parallelGroups, out Dictionary<string, List<string>> dependencies)
        {
            parallelGroups = new List<List<Node>>();
            dependencies = new Dictionary<string, List<string>>();

            var enabledNodes = workflow.Nodes.Where(n => n.IsEnabled).ToList();
            var inDegree = new Dictionary<string, int>();
            var graph = new Dictionary<string, List<string>>();

            foreach (var node in enabledNodes)
            {
                inDegree[node.Id] = 0;
                graph[node.Id] = new List<string>();
                dependencies[node.Id] = new List<string>();
            }

            foreach (var conn in workflow.Connections)
            {
                if (!graph.ContainsKey(conn.SourceNodeId) || !graph.ContainsKey(conn.TargetNodeId))
                    continue;

                graph[conn.SourceNodeId].Add(conn.TargetNodeId);
                inDegree[conn.TargetNodeId]++;
                dependencies[conn.TargetNodeId].Add(conn.SourceNodeId);
            }

            var processed = new HashSet<string>();
            var hasLayers = false;

            while (processed.Count < enabledNodes.Count)
            {
                var currentLayer = enabledNodes
                    .Where(n => !processed.Contains(n.Id) && inDegree[n.Id] == 0)
                    .ToList();

                if (currentLayer.Count == 0) return false;
                if (currentLayer.Count > 1) hasLayers = true;

                parallelGroups.Add(currentLayer);

                foreach (var node in currentLayer)
                {
                    processed.Add(node.Id);
                    foreach (var neighbor in graph[node.Id])
                    {
                        inDegree[neighbor]--;
                    }
                }
            }

            return hasLayers && parallelGroups.Count > 1;
        }
    }
}

