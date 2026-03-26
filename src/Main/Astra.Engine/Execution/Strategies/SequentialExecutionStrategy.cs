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
    /// 顺序执行策略
    /// 节点按顺序逐个执行，适用于有依赖关系的线性工作流
    /// </summary>
    public class SequentialExecutionStrategy : IExecutionStrategy
    {
        /// <summary>
        /// 执行工作流（顺序模式）
        /// </summary>
        public async Task<ExecutionResult> ExecuteAsync(WorkFlowExecutionContext context)
        {
            var workflow = context.Workflow;
            var sequencedNodes = context.DetectedStrategy?.Nodes ?? new List<Node>();
            var outputs = new Dictionary<string, object>();
            var hasNonSkippedFailure = false;
            ExecutionResult firstFailureResult = null;
            MarkDisabledNodesAsSkipped(workflow, context);

            if (sequencedNodes.Count == 0)
            {
                return ExecutionResult.Successful("无可执行节点").WithOutputs(outputs);
            }

            for (int i = 0; i < sequencedNodes.Count; i++)
            {
                if (context.ExecutionController != null)
                {
                    await context.ExecutionController.WaitIfPausedAsync(context.CancellationToken);
                }

                var node = sequencedNodes[i];
                var nodeContext = i == 0 ? context.NodeContext : PrepareNodeContext(node, workflow, context.NodeContext);

                var nodeResult = await ExecuteNodeAsync(node, nodeContext, context);

                foreach (var kvp in nodeResult.OutputData)
                {
                    outputs[kvp.Key] = kvp.Value;
                    workflow.Variables[kvp.Key] = kvp.Value;
                }

                context.OnProgressChanged?.Invoke((int)((i + 1) * 100.0 / sequencedNodes.Count));

                if (!nodeResult.Success && !nodeResult.IsSkipped)
                {
                    // 如果全局配置了 StopOnError=true：
                    // - 本节点未开启 ContinueOnFailure => 立即终止工作流
                    // - 本节点开启 ContinueOnFailure => 记录失败并继续执行下游
                    if (workflow.Configuration.StopOnError && !node.ContinueOnFailure)
                    {
                        return ExecutionResult.Failed($"节点 '{node.Name}' 执行失败: {nodeResult.Message}", nodeResult.Exception)
                            .WithOutputs(outputs);
                    }

                    hasNonSkippedFailure = true;
                    firstFailureResult ??= nodeResult;
                }

                context.CancellationToken.ThrowIfCancellationRequested();
            }

            if (workflow.Configuration.StopOnError && hasNonSkippedFailure)
            {
                return ExecutionResult.Failed($"顺序执行过程中发生失败：{firstFailureResult?.Message}", firstFailureResult?.Exception)
                    .WithOutputs(outputs);
            }

            return ExecutionResult.Successful($"顺序执行完成，共执行 {sequencedNodes.Count} 个节点")
                .WithOutputs(outputs);
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

                // 继承执行标识与元数据，保证 UI 事件的 ExecutionId 一致
                ExecutionId = baseContext.ExecutionId,
                ParentWorkFlow = baseContext.ParentWorkFlow,
                Metadata = new Dictionary<string, object>(baseContext.Metadata ?? new Dictionary<string, object>())
            };

            var inputConnections = workflow.GetInputConnections(node.Id);
            if (inputConnections.Count > 0)
            {
                var prevNode = workflow.GetNode(inputConnections[0].SourceNodeId);
                if (prevNode?.LastExecutionResult != null)
                {
                    foreach (var kvp in prevNode.LastExecutionResult.OutputData)
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
        private async Task<ExecutionResult> ExecuteNodeAsync(Node node, NodeContext nodeContext, WorkFlowExecutionContext workflowContext)
        {
            var startTime = DateTime.Now;
            var pausedBefore = workflowContext.ExecutionController?.TotalPausedDuration ?? TimeSpan.Zero;
            workflowContext.OnNodeExecutionStarted?.Invoke(node, nodeContext);
            var activeStopwatch = Stopwatch.StartNew();

            ExecutionResult result;
            try
            {
                // 使用扩展方法执行节点
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

