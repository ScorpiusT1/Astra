using Astra.Core.Nodes.Models;
using Astra.Engine.Execution.NodeExecutor;
using Astra.Engine.Execution.WorkFlowEngine;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Astra.Engine.Execution.Strategies
{
    /// <summary>
    /// 「最后执行」阶段：在常规策略跑完后执行 <see cref="Node.ExecuteLast"/> 的节点；
    /// 仅使用「最后执行」节点之间的连线做拓扑，不受主流程是否因失败提前结束影响（主阶段已结束后仍执行）。
    /// </summary>
    internal static class FinallyPhaseRunner
    {
        public static async Task<ExecutionResult> RunAsync(WorkFlowExecutionContext context)
        {
            var workflow = context.Workflow;
            if (workflow?.Nodes == null)
            {
                return ExecutionResult.Successful(string.Empty);
            }

            var finallyNodes = workflow.Nodes
                .Where(n => n != null && n.IsEnabled && n.ExecuteLast)
                .ToList();

            if (finallyNodes.Count == 0)
            {
                return ExecutionResult.Successful(string.Empty);
            }

            var finallyIds = new HashSet<string>(finallyNodes.Select(n => n.Id));
            var order = TopologicalSortFinally(workflow, finallyNodes, finallyIds);
            if (order == null)
            {
                return ExecutionResult.Failed("最后执行节点之间存在循环依赖，无法排序");
            }

            var outputs = new Dictionary<string, object>();
            for (var i = 0; i < order.Count; i++)
            {
                if (context.ExecutionController != null)
                {
                    await context.ExecutionController.WaitIfPausedAsync(context.CancellationToken);
                }

                context.CancellationToken.ThrowIfCancellationRequested();

                var listedNode = order[i];
                var node = WorkflowNodeFailurePolicy.ResolveExecutionNode(workflow, listedNode);
                var nodeContext = i == 0 ? context.NodeContext : PrepareNodeContext(node, workflow, context.NodeContext);

                var nodeResult = await ExecuteNodeAsync(node, nodeContext, context);

                foreach (var kvp in nodeResult.OutputData)
                {
                    outputs[kvp.Key] = kvp.Value;
                    workflow.Variables[kvp.Key] = kvp.Value;
                }

                if (!nodeResult.Success && !nodeResult.IsSkipped)
                {
                    if (WorkflowNodeFailurePolicy.ShouldAbortRemainingAfterFailedStep(workflow, node))
                    {
                        return ExecutionResult.Failed($"最后执行阶段节点 '{node.Name}' 失败: {nodeResult.Message}", nodeResult.Exception)
                            .WithOutputs(outputs);
                    }
                }
            }

            return ExecutionResult.Successful($"最后执行完成，共 {order.Count} 个节点").WithOutputs(outputs);
        }

        /// <summary>
        /// 仅统计「最后执行」子图内的依赖边并拓扑排序；与主流程之间的连线不参与排序。
        /// </summary>
        private static List<Node>? TopologicalSortFinally(WorkFlowNode workflow, List<Node> finallyNodes, HashSet<string> finallyIds)
        {
            var inDegree = new Dictionary<string, int>(StringComparer.Ordinal);
            var graph = new Dictionary<string, List<string>>(StringComparer.Ordinal);

            foreach (var n in finallyNodes)
            {
                inDegree[n.Id] = 0;
                graph[n.Id] = new List<string>();
            }

            foreach (var conn in workflow.Connections ?? Enumerable.Empty<Connection>())
            {
                if (conn == null || !finallyIds.Contains(conn.SourceNodeId) || !finallyIds.Contains(conn.TargetNodeId))
                {
                    continue;
                }

                graph[conn.SourceNodeId].Add(conn.TargetNodeId);
                inDegree[conn.TargetNodeId]++;
            }

            var queue = new Queue<string>(inDegree.Where(kvp => kvp.Value == 0).Select(kvp => kvp.Key));
            var result = new List<Node>();

            while (queue.Count > 0)
            {
                var nodeId = queue.Dequeue();
                var node = workflow.GetNode(nodeId);
                if (node != null && node.IsEnabled && node.ExecuteLast)
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

            return result.Count == finallyNodes.Count ? result : null;
        }

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
                var prevNode = workflow.GetNode(conn.SourceNodeId);
                if (prevNode?.LastExecutionResult != null)
                {
                    foreach (var kvp in prevNode.LastExecutionResult.OutputData)
                    {
                        ctx.InputData[kvp.Key] = kvp.Value;
                    }
                }
            }

            return ctx;
        }

        private static async Task<ExecutionResult> ExecuteNodeAsync(Node node, NodeContext nodeContext, WorkFlowExecutionContext workflowContext)
        {
            var startTime = DateTime.Now;
            var pausedBefore = workflowContext.ExecutionController?.TotalPausedDuration ?? TimeSpan.Zero;
            workflowContext.OnNodeExecutionStarted?.Invoke(node, nodeContext);
            var activeStopwatch = Stopwatch.StartNew();

            ExecutionResult result;
            try
            {
                result = await node.ExecuteAsync(nodeContext, workflowContext.CancellationToken);
                result.StartTime = startTime;
                result.EndTime = AdjustEndTimeForPause(DateTime.Now, pausedBefore, workflowContext);
            }
            catch (OperationCanceledException) when (workflowContext.CancellationToken.IsCancellationRequested)
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
            return pausedDelta <= TimeSpan.Zero ? rawEndTime : rawEndTime - pausedDelta;
        }
    }
}
