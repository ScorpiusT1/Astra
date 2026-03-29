using Astra.Core.Nodes.Models;
using Astra.Engine.Execution.NodeExecutor;
using Astra.Engine.Execution.WorkFlowEngine;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Astra.Engine.Execution.Strategies
{
    /// <summary>
    /// 部分并行执行策略
    /// 按层并行执行，层间顺序执行，适用于有部分依赖关系的工作流
    /// </summary>
    public class PartiallyParallelExecutionStrategy : IExecutionStrategy
    {
        /// <summary>
        /// 执行工作流（部分并行模式）
        /// </summary>
        public async Task<ExecutionResult> ExecuteAsync(WorkFlowExecutionContext context)
        {
            var workflow = context.Workflow;
            var groups = context.DetectedStrategy.ParallelGroups;
            MarkDisabledNodesAsSkipped(workflow, context);
            if (groups == null || groups.Count == 0)
            {
                return ExecutionResult.Successful("无可执行层");
            }

            var forward = WorkflowDependencyTraversal.BuildForwardAdjacency(workflow);
            var blocked = new HashSet<string>(StringComparer.Ordinal);
            var outputs = new Dictionary<string, object>();
            int totalLayers = groups.Count;
            int finishedLayers = 0;
            var reportWorkflowFailed = false;

            foreach (var layer in groups)
            {
                if (context.ExecutionController != null)
                {
                    await context.ExecutionController.WaitIfPausedAsync(context.CancellationToken);
                }

                var options = new ParallelOptions
                {
                    MaxDegreeOfParallelism = workflow.Configuration.MaxParallelism,
                    CancellationToken = context.CancellationToken
                };

                var results = new ConcurrentBag<(Node node, ExecutionResult result)>();

                await Parallel.ForEachAsync(layer, options, async (listedNode, ct) =>
                {
                    if (context.ExecutionController != null)
                    {
                        await context.ExecutionController.WaitIfPausedAsync(ct);
                    }

                    var node = WorkflowNodeFailurePolicy.ResolveExecutionNode(workflow, listedNode);
                    if (blocked.Contains(node.Id))
                    {
                        // 因上游失败未调度执行：保持「未执行」态，与主动跳过（禁用/条件）区分
                        node.LastExecutionResult = null;
                        node.ExecutionState = NodeExecutionState.Idle;
                        return;
                    }

                    var nodeContext = PrepareNodeContext(node, workflow, context.NodeContext);
                    var nodeResult = await ExecuteNodeAsync(node, nodeContext, context, ct);
                    results.Add((node, nodeResult));
                });

                var ordered = results.ToList();
                ordered.Sort((a, b) => string.CompareOrdinal(a.node.Id, b.node.Id));

                foreach (var (node, result) in ordered)
                {
                    foreach (var kvp in result.OutputData)
                    {
                        outputs[$"{node.Name}_{kvp.Key}"] = kvp.Value;
                        workflow.Variables[$"{node.Name}_{kvp.Key}"] = kvp.Value;
                    }

                    if (!result.Success && !result.IsSkipped &&
                        WorkflowNodeFailurePolicy.ShouldAbortRemainingAfterFailedStep(workflow, node))
                    {
                        reportWorkflowFailed = true;
                    }

                    if (!WorkflowNodeFailurePolicy.ShouldReleaseDownstreamEdges(workflow, node, result))
                    {
                        foreach (var d in WorkflowDependencyTraversal.CollectDescendants(forward, node.Id))
                        {
                            blocked.Add(d);
                        }
                    }
                }

                finishedLayers++;
                context.OnProgressChanged?.Invoke((int)(finishedLayers * 100.0 / totalLayers));
            }

            if (reportWorkflowFailed)
            {
                return ExecutionResult.Failed("部分并行执行完成，但有节点失败且未允许继续").WithOutputs(outputs);
            }

            return ExecutionResult.Successful($"部分并行执行完成，共 {totalLayers} 层").WithOutputs(outputs);
        }

        /// <summary>
        /// 准备节点执行上下文（与复杂图策略一致：合并来自所有输入连线的上游输出）
        /// </summary>
        private static NodeContext PrepareNodeContext(Node node, WorkFlowNode workflow, NodeContext baseContext)
        {
            var ctx = new NodeContext
            {
                InputData = new Dictionary<string, object>(),
                GlobalVariables = new Dictionary<string, object>(baseContext.GlobalVariables),
                ServiceProvider = baseContext.ServiceProvider,
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
                        ctx.InputData[kvp.Key] = kvp.Value;
                    }
                }
            }

            return ctx;
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
