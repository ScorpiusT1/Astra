using Astra.Core.Nodes.Models;
using Astra.Engine.Execution.NodeExecutor;
using Astra.Engine.Execution.WorkFlowEngine;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Astra.Engine.Execution.Strategies
{
    /// <summary>
    /// 并行层：先单次通知「整层开始」，再执行节点逻辑（不再逐节点触发 Started）。
    /// </summary>
    internal static class StrategyNodeExecutionCore
    {
        public static void NotifyParallelWaveStarted(
            WorkFlowExecutionContext workflowContext,
            IReadOnlyList<(Node Node, NodeContext Context)> prepared)
        {
            if (prepared == null || prepared.Count == 0)
            {
                return;
            }

            if (prepared.Count == 1)
            {
                var p = prepared[0];
                workflowContext.OnNodeExecutionStarted?.Invoke(p.Node, p.Context);
                return;
            }

            var wave = new List<NodeExecutionEventArgs>(prepared.Count);
            foreach (var (node, ctx) in prepared)
            {
                wave.Add(new NodeExecutionEventArgs { Node = node, Context = ctx });
            }

            if (workflowContext.OnParallelWaveNodesStarting != null)
            {
                workflowContext.OnParallelWaveNodesStarting.Invoke(wave);
            }
            else
            {
                foreach (var item in wave)
                {
                    if (item.Node != null && item.Context != null)
                    {
                        workflowContext.OnNodeExecutionStarted?.Invoke(item.Node, item.Context);
                    }
                }
            }
        }

        public static async Task<ExecutionResult> ExecuteWithoutStartNotificationAsync(
            Node node,
            NodeContext nodeContext,
            WorkFlowExecutionContext workflowContext,
            CancellationToken cancellationToken)
        {
            var startTime = DateTime.Now;
            var pausedBefore = workflowContext.ExecutionController?.TotalPausedDuration ?? TimeSpan.Zero;
            var activeStopwatch = Stopwatch.StartNew();

            ExecutionResult result;
            try
            {
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
    }
}
