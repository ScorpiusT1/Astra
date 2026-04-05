using Astra.Core.Nodes.Models;
using Astra.Engine.Execution.NodeExecutor;
using Astra.Engine.Execution.WorkFlowEngine;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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

                var layerList = layer as IReadOnlyList<Node> ?? layer.ToList();
                var results = new ConcurrentBag<(Node node, ExecutionResult result)>();

                var prepared = new List<(Node Node, NodeContext Context)>();
                foreach (var listedNode in layerList)
                {
                    var node = WorkflowNodeFailurePolicy.ResolveExecutionNode(workflow, listedNode);
                    if (blocked.Contains(node.Id))
                    {
                        node.LastExecutionResult = null;
                        node.ExecutionState = NodeExecutionState.Idle;
                        continue;
                    }

                    var nodeContext = PrepareNodeContext(node, workflow, context.NodeContext);
                    prepared.Add((node, nodeContext));
                }

                if (prepared.Count > 0)
                {
                    StrategyNodeExecutionCore.NotifyParallelWaveStarted(context, prepared);
                }

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

                var ordered = results.ToList();
                ordered.Sort((a, b) => string.CompareOrdinal(a.node.Id, b.node.Id));

                foreach (var (node, result) in ordered)
                {
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
                        ctx.InputData[$"{conn.SourceNodeId}:{kvp.Key}"] = kvp.Value;
                    }
                }
            }

            return ctx;
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
