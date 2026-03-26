using Astra.Core.Nodes.Models;
using Astra.Engine.Execution.NodeExecutor;
using Astra.Engine.Execution.WorkFlowEngine;
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Diagnostics;
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
            var hasNonSkippedFailure = false;
            ExecutionResult firstFailureResult = null;
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
                var options = new ParallelOptions
                {
                    MaxDegreeOfParallelism = workflow.Configuration.MaxParallelism,
                    CancellationToken = context.CancellationToken
                };

                await Parallel.ForEachAsync(batch, options, async (node, ct) =>
                {
                    if (context.ExecutionController != null)
                    {
                        await context.ExecutionController.WaitIfPausedAsync(ct);
                    }

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

                    if (!result.Success && !result.IsSkipped)
                    {
                        if (workflow.Configuration.StopOnError && !node.ContinueOnFailure)
                        {
                            return ExecutionResult.Failed($"节点 '{node.Name}' 执行失败: {result.Message}", result.Exception)
                                .WithOutputs(outputs);
                        }

                        hasNonSkippedFailure = true;
                        firstFailureResult ??= result;
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

            if (workflow.Configuration.StopOnError && hasNonSkippedFailure)
            {
                return ExecutionResult.Failed($"复杂图执行过程中发生失败：{firstFailureResult?.Message}", firstFailureResult?.Exception)
                    .WithOutputs(outputs);
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
            var pausedBefore = workflowContext.ExecutionController?.TotalPausedDuration ?? TimeSpan.Zero;
            workflowContext.OnNodeExecutionStarted?.Invoke(node, nodeContext);
            var activeStopwatch = Stopwatch.StartNew();

            ExecutionResult result;
            try
            {
                // 使用扩展方法执行节点
                result = await node.ExecuteAsync(nodeContext, cancellationToken);
                result.StartTime = startTime;
                result.EndTime = AdjustEndTimeForPause(DateTime.Now, pausedBefore, workflowContext);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                result = ExecutionResult.Failed($"节点 '{node.Name}' 执行异常: {ex.Message}", ex);
                result.StartTime = startTime;
                result.EndTime = AdjustEndTimeForPause(DateTime.Now, pausedBefore, workflowContext);
            }
            finally
            {
                activeStopwatch.Stop();
            }

            result.ActiveDurationMs = activeStopwatch.Elapsed.TotalMilliseconds;

            node.LastExecutionResult = result;
            workflowContext.OnNodeExecutionCompleted?.Invoke(node, nodeContext, result);
            return result;
        }

        private static DateTime AdjustEndTimeForPause(
            DateTime rawEndTime,
            TimeSpan pausedBefore,
            WorkFlowExecutionContext workflowContext)
        {
            var pausedAfter = workflowContext.ExecutionController?.TotalPausedDuration ?? pausedBefore;
            var pausedDelta = pausedAfter - pausedBefore;
            if (pausedDelta <= TimeSpan.Zero)
            {
                return rawEndTime;
            }

            return rawEndTime - pausedDelta;
        }

        private static void MarkDisabledNodesAsSkipped(WorkFlowNode workflow, WorkFlowExecutionContext workflowContext)
        {
            var disabledNodes = workflow.Nodes.Where(n => !n.IsEnabled).ToList();
            foreach (var disabledNode in disabledNodes)
            {
                var skippedResult = ExecutionResult.Skip($"节点 '{disabledNode.Name}' 未启用，已跳过")
                    .WithOutput("SkipReason", "Disabled");
                disabledNode.LastExecutionResult = skippedResult;
                disabledNode.ExecutionState = NodeExecutionState.Skipped;
                workflowContext.OnNodeExecutionCompleted?.Invoke(disabledNode, workflowContext.NodeContext, skippedResult);
            }
        }
    }
}

