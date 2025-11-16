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
            if (groups == null || groups.Count == 0)
            {
                return ExecutionResult.Successful("无可执行层");
            }

            var outputs = new Dictionary<string, object>();
            int totalLayers = groups.Count;
            int finishedLayers = 0;

            foreach (var layer in groups)
            {
                var options = new ParallelOptions
                {
                    MaxDegreeOfParallelism = workflow.Configuration.MaxParallelism,
                    CancellationToken = context.CancellationToken
                };

                var results = new ConcurrentBag<(Node node, ExecutionResult result)>();

                await Parallel.ForEachAsync(layer, options, async (node, ct) =>
                {
                    var nodeResult = await ExecuteNodeAsync(node, context.NodeContext, context, ct);
                    results.Add((node, nodeResult));
                });

                foreach (var (node, result) in results)
                {
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
                }

                finishedLayers++;
                context.OnProgressChanged?.Invoke((int)(finishedLayers * 100.0 / totalLayers));
            }

            return ExecutionResult.Successful($"部分并行执行完成，共 {totalLayers} 层").WithOutputs(outputs);
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

