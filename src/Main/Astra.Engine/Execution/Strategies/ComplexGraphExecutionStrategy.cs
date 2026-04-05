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
            if (context.DetectedStrategy?.HasCycle == true)
            {
                return ExecutionResult.Failed("存在循环依赖，无法执行");
            }

            var workflow = context.Workflow;
            var enabledNodes = workflow.Nodes.Where(n => n.IsEnabled).ToList();
            var outputs = new Dictionary<string, object>();
            MarkDisabledNodesAsSkipped(workflow, context);

            // 构建图与入度
            var inDegree = new Dictionary<string, int>();
            var graph = new Dictionary<string, List<string>>();
            var nodeById = enabledNodes.ToDictionary(n => n.Id, n => n);

            foreach (var node in enabledNodes)
            {
                inDegree[node.Id] = 0;
                graph[node.Id] = new List<string>();
            }

            // 拓扑依赖：无论是 Flow 还是 Data 连接，只要存在连线就作为依赖边处理
            foreach (var conn in workflow.Connections.Where(c => c != null))
            {
                if (!graph.ContainsKey(conn.SourceNodeId) || !graph.ContainsKey(conn.TargetNodeId))
                    continue;
                graph[conn.SourceNodeId].Add(conn.TargetNodeId);
                inDegree[conn.TargetNodeId]++;
            }

            var ready = new Queue<Node>(enabledNodes.Where(n => inDegree[n.Id] == 0));
            int processed = 0;
            var reportWorkflowFailed = false;
            var executedNodeIds = new HashSet<string>(StringComparer.Ordinal);

            while (ready.Count > 0)
            {
                if (context.ExecutionController != null)
                {
                    await context.ExecutionController.WaitIfPausedAsync(context.CancellationToken);
                }

                // 取最多 MaxParallelism 个就绪节点执行
                var batch = new List<Node>();
                int take = Math.Min(workflow.Configuration.MaxParallelism, ready.Count);
                for (int i = 0; i < take; i++)
                {
                    batch.Add(ready.Dequeue());
                }

                var results = new ConcurrentBag<(Node node, ExecutionResult result)>();

                var prepared = new List<(Node Node, NodeContext Context)>(batch.Count);
                foreach (var batchNode in batch)
                {
                    var node = WorkflowNodeFailurePolicy.ResolveExecutionNode(workflow, batchNode);
                    var nodeContext = PrepareNodeContext(node, workflow, context.NodeContext);
                    prepared.Add((node, nodeContext));
                }

                StrategyNodeExecutionCore.NotifyParallelWaveStarted(context, prepared);

                await SynchronizedParallelLayerExecutor.ForEachAsync(
                    prepared,
                    workflow.Configuration.MaxParallelism,
                    context.CancellationToken,
                    async (item, ct) =>
                    {
                        if (context.ExecutionController != null)
                        {
                            await context.ExecutionController.WaitIfPausedAsync(ct);
                        }

                        var nodeResult = await StrategyNodeExecutionCore.ExecuteWithoutStartNotificationAsync(
                            item.Node,
                            item.Context,
                            context,
                            ct);
                        results.Add((item.Node, nodeResult));
                    }).ConfigureAwait(false);

                var batchOrdered = results.ToList();
                batchOrdered.Sort((a, b) => string.CompareOrdinal(a.node.Id, b.node.Id));

                foreach (var (node, result) in batchOrdered)
                {
                    processed++;
                    executedNodeIds.Add(node.Id);
                    foreach (var kvp in result.OutputData)
                    {
                        outputs[$"{node.Id}_{kvp.Key}"] = kvp.Value;
                        workflow.Variables[$"{node.Id}_{kvp.Key}"] = kvp.Value;
                    }

                    if (!result.Success && !result.IsSkipped &&
                        WorkflowNodeFailurePolicy.ShouldAbortRemainingAfterFailedStep(workflow, node))
                    {
                        reportWorkflowFailed = true;
                    }
                }

                // 必须在整批结果上完成入度更新：并行分支上某一节点失败时，不得在未处理同批其它节点前就 return，
                // 否则仅依赖另一分支的下游（如 A→B 与 C 并行时）将永远无法入队。
                foreach (var (node, result) in batchOrdered)
                {
                    if (!WorkflowNodeFailurePolicy.ShouldReleaseDownstreamEdges(workflow, node, result))
                    {
                        continue;
                    }

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
                foreach (var n in enabledNodes)
                {
                    if (executedNodeIds.Contains(n.Id))
                    {
                        continue;
                    }

                    n.LastExecutionResult = null;
                    n.ExecutionState = NodeExecutionState.Idle;
                    processed++;
                }

                if (processed != enabledNodes.Count)
                {
                    return ExecutionResult.Failed("检测到循环依赖，复杂图执行未完成");
                }
            }

            if (reportWorkflowFailed)
            {
                return ExecutionResult.Failed("复杂图执行完成，但有节点失败且未允许继续").WithOutputs(outputs);
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
                ServiceProvider = baseContext.ServiceProvider,

                // 继承执行标识与元数据，保证 UI 事件过滤时 ExecutionId 一致
                ExecutionId = baseContext.ExecutionId,
                ParentWorkFlow = baseContext.ParentWorkFlow,
                Metadata = new Dictionary<string, object>(baseContext.Metadata ?? new Dictionary<string, object>())
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
                        context.InputData[$"{conn.SourceNodeId}:{kvp.Key}"] = kvp.Value;
                    }
                }
            }

            return context;
        }

        private static void MarkDisabledNodesAsSkipped(WorkFlowNode workflow, WorkFlowExecutionContext workflowContext)
        {
            var disabledNodes = workflow.Nodes.Where(n => !n.IsEnabled).ToList();
            foreach (var disabledNode in disabledNodes)
            {
                var skippedResult = ExecutionResult.Skip($"节点 '{disabledNode.Name}' 未启用，已跳过")
                    .WithOutput(EngineConstants.OutputKeys.SkipReason, EngineConstants.OutputValues.Disabled);
                disabledNode.LastExecutionResult = skippedResult;
                disabledNode.ExecutionState = NodeExecutionState.Skipped;
                workflowContext.OnNodeExecutionCompleted?.Invoke(disabledNode, workflowContext.NodeContext, skippedResult);
            }
        }
    }
}

