using Astra.Core.Nodes.Models;
using Astra.Engine.Execution.NodeExecutor;
using Astra.Engine.Execution.WorkFlowEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Astra.Engine.Execution.Strategies
{
    /// <summary>
    /// 并行执行策略
    /// 所有节点同时并行执行，适用于无依赖关系的独立节点
    /// </summary>
    public class ParallelExecutionStrategy : IExecutionStrategy
    {
        /// <summary>
        /// 执行工作流（并行模式）
        /// </summary>
        public async Task<ExecutionResult> ExecuteAsync(WorkFlowExecutionContext context)
        {
            var workflow = context.Workflow;
            var enabledNodes = context.DetectedStrategy?.Nodes ?? new List<Node>();
            var outputs = new Dictionary<string, object>();
            MarkDisabledNodesAsSkipped(workflow, context);

            if (enabledNodes.Count == 0)
            {
                return ExecutionResult.Successful("无可执行节点").WithOutputs(outputs);
            }

            var nodesList = enabledNodes as IReadOnlyList<Node> ?? enabledNodes.ToList();

            if (context.ExecutionController != null)
            {
                await context.ExecutionController.WaitIfPausedAsync(context.CancellationToken);
            }

            var prepared = new List<(Node Node, NodeContext Context)>(nodesList.Count);
            foreach (var listedNode in nodesList)
            {
                var node = WorkflowNodeFailurePolicy.ResolveExecutionNode(workflow, listedNode);
                var isolatedContext = CreateIsolatedNodeContext(context.NodeContext);
                prepared.Add((node, isolatedContext));
            }

            StrategyNodeExecutionCore.NotifyParallelWaveStarted(context, prepared);

            var results = new List<(Node node, ExecutionResult result)>();
            var lockObj = new object();
            int completedCount = 0;

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

                    lock (lockObj)
                    {
                        results.Add((item.Node, nodeResult));
                        completedCount++;
                        context.OnProgressChanged?.Invoke((int)(completedCount * 100.0 / enabledNodes.Count));
                    }
                }).ConfigureAwait(false);

            foreach (var (node, result) in results)
            {
                foreach (var kvp in result.OutputData)
                {
                    outputs[$"{node.Id}_{kvp.Key}"] = kvp.Value;
                }

                if (!result.Success && !result.IsSkipped)
                {
                    if (WorkflowNodeFailurePolicy.ShouldAbortRemainingAfterFailedStep(workflow, node))
                    {
                        return ExecutionResult.Failed($"节点 '{node.Name}' 执行失败: {result.Message}", result.Exception)
                            .WithOutputs(outputs);
                    }
                }
            }

            return ExecutionResult.Successful($"并行执行完成，共执行 {enabledNodes.Count} 个节点")
                .WithOutputs(outputs);
        }

        private static NodeContext CreateIsolatedNodeContext(NodeContext baseContext)
        {
            return new NodeContext
            {
                InputData = new Dictionary<string, object>(baseContext?.InputData ?? new Dictionary<string, object>()),
                GlobalVariables = new Dictionary<string, object>(baseContext?.GlobalVariables ?? new Dictionary<string, object>()),
                Metadata = new Dictionary<string, object>(baseContext?.Metadata ?? new Dictionary<string, object>()),
                ServiceProvider = baseContext?.ServiceProvider,
                ExecutionId = baseContext?.ExecutionId,
                ParentWorkFlow = baseContext?.ParentWorkFlow
            };
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

