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
            var enabledNodes = context.DetectedStrategy.Nodes;
            var outputs = new Dictionary<string, object>();

            var options = new ParallelOptions
            {
                MaxDegreeOfParallelism = workflow.Configuration.MaxParallelism,
                CancellationToken = context.CancellationToken
            };

            var results = new List<(Node node, ExecutionResult result)>();
            var lockObj = new object();
            int completedCount = 0;

            await Parallel.ForEachAsync(enabledNodes, options, async (node, ct) =>
            {
                var nodeResult = await ExecuteNodeAsync(node, context.NodeContext, context, ct);

                lock (lockObj)
                {
                    results.Add((node, nodeResult));
                    completedCount++;
                    context.OnProgressChanged?.Invoke((int)(completedCount * 100.0 / enabledNodes.Count));
                }
            });

            foreach (var (node, result) in results)
            {
                foreach (var kvp in result.OutputData)
                {
                    outputs[$"{node.Name}_{kvp.Key}"] = kvp.Value;
                }

                if (!result.Success && !result.IsSkipped && workflow.Configuration.StopOnError)
                {
                    return ExecutionResult.Failed($"节点 '{node.Name}' 执行失败: {result.Message}", result.Exception)
                        .WithOutputs(outputs);
                }
            }

            return ExecutionResult.Successful($"并行执行完成，共执行 {enabledNodes.Count} 个节点")
                .WithOutputs(outputs);
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

            workflowContext.OnNodeExecutionCompleted?.Invoke(node, nodeContext, result);
            return result;
        }
    }
}

