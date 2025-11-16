using Astra.Core.Nodes.Models;
using Astra.Engine.Execution.NodeExecutor;
using Astra.Engine.Execution.WorkFlowEngine;
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Astra.Engine.Execution.Strategies
{
    /// <summary>
    /// 复杂图执行策略
    /// 使用拓扑排序确定执行顺序，支持复杂依赖关系的工作流
    /// </summary>
    public class ComplexGraphExecutionStrategy : IExecutionStrategy
    {
        /// <summary>
        /// 执行工作流（复杂图模式）
        /// </summary>
        public async Task<ExecutionResult> ExecuteAsync(WorkFlowExecutionContext context)
        {
            var workflow = context.Workflow;
            var enabledNodes = workflow.Nodes.Where(n => n.IsEnabled).ToList();
            var outputs = new Dictionary<string, object>();

            // 构建图与入度
            var inDegree = new Dictionary<string, int>();
            var graph = new Dictionary<string, List<string>>();
            var nodeById = enabledNodes.ToDictionary(n => n.Id, n => n);

            foreach (var node in enabledNodes)
            {
                inDegree[node.Id] = 0;
                graph[node.Id] = new List<string>();
            }

            foreach (var conn in workflow.Connections)
            {
                if (!graph.ContainsKey(conn.SourceNodeId) || !graph.ContainsKey(conn.TargetNodeId))
                    continue;
                graph[conn.SourceNodeId].Add(conn.TargetNodeId);
                inDegree[conn.TargetNodeId]++;
            }

            var ready = new Queue<Node>(enabledNodes.Where(n => inDegree[n.Id] == 0));
            int processed = 0;

            while (ready.Count > 0)
            {
                // 取最多 MaxParallelism 个就绪节点执行
                var batch = new List<Node>();
                int take = Math.Min(workflow.Configuration.MaxParallelism, ready.Count);
                for (int i = 0; i < take; i++)
                {
                    batch.Add(ready.Dequeue());
                }

                var results = new ConcurrentBag<(Node node, ExecutionResult result)>();
                var options = new ParallelOptions
                {
                    MaxDegreeOfParallelism = workflow.Configuration.MaxParallelism,
                    CancellationToken = context.CancellationToken
                };

                await Parallel.ForEachAsync(batch, options, async (node, ct) =>
                {
                    var nodeContext = PrepareNodeContext(node, workflow, context.NodeContext);
                    var nodeResult = await ExecuteNodeAsync(node, nodeContext, context, ct);
                    results.Add((node, nodeResult));
                });

                foreach (var (node, result) in results)
                {
                    processed++;
                    foreach (var kvp in result.OutputData)
                    {
                        outputs[$"{node.Name}_{kvp.Key}"] = kvp.Value;
                        workflow.Variables[$"{node.Name}_{kvp.Key}"] = kvp.Value;
                    }

                    if (!result.Success && !result.IsSkipped && workflow.Configuration.StopOnError)
                    {
                        return ExecutionResult.Failed($"节点 '{node.Name}' 执行失败: {result.Message}", result.Exception)
                            .WithOutputs(outputs);
                    }

                    // 入度更新
                    foreach (var neighborId in graph[node.Id])
                    {
                        inDegree[neighborId]--;
                        if (inDegree[neighborId] == 0 && nodeById.TryGetValue(neighborId, out var neighbor))
                        {
                            ready.Enqueue(neighbor);
                        }
                    }
                }

                context.OnProgressChanged?.Invoke((int)(processed * 100.0 / enabledNodes.Count));
            }

            if (processed != enabledNodes.Count)
            {
                return ExecutionResult.Failed("检测到循环依赖，复杂图执行未完成");
            }

            return ExecutionResult.Successful($"复杂图执行完成，共执行 {processed} 个节点").WithOutputs(outputs);
        }

        /// <summary>
        /// 准备节点执行上下文
        /// </summary>
        private NodeContext PrepareNodeContext(Node node, WorkFlowNode workflow, NodeContext baseContext)
        {
            var context = new NodeContext
            {
                InputData = new Dictionary<string, object>(),
                GlobalVariables = new Dictionary<string, object>(baseContext.GlobalVariables),
                ServiceProvider = baseContext.ServiceProvider
            };

            var inputConnections = workflow.GetInputConnections(node.Id);
            foreach (var conn in inputConnections)
            {
                var prev = workflow.GetNode(conn.SourceNodeId);
                if (prev?.LastExecutionResult != null)
                {
                    foreach (var kvp in prev.LastExecutionResult.OutputData)
                    {
                        context.InputData[kvp.Key] = kvp.Value;
                    }
                }
            }

            return context;
        }

        /// <summary>
        /// 执行单个节点
        /// </summary>
        private async Task<ExecutionResult> ExecuteNodeAsync(Node node, NodeContext nodeContext, WorkFlowExecutionContext workflowContext, CancellationToken cancellationToken)
        {
            var startTime = DateTime.Now;
            workflowContext.OnNodeExecutionStarted?.Invoke(node, nodeContext);

            ExecutionResult result;
            try
            {
                // 使用扩展方法执行节点
                result = await node.ExecuteAsync(nodeContext, cancellationToken);
                result.StartTime = startTime;
                result.EndTime = DateTime.Now;
            }
            catch (Exception ex)
            {
                result = ExecutionResult.Failed($"节点 '{node.Name}' 执行异常: {ex.Message}", ex);
                result.StartTime = startTime;
                result.EndTime = DateTime.Now;
            }

            node.LastExecutionResult = result;
            workflowContext.OnNodeExecutionCompleted?.Invoke(node, nodeContext, result);
            return result;
        }
    }
}

