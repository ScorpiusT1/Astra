using Astra.Core.Nodes.Models;
using Astra.Engine.Execution.NodeExecutor;
using Astra.Engine.Execution.WorkFlowEngine;
using System;
using System.Collections.Generic;
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
            var sequencedNodes = context.DetectedStrategy.Nodes;
            var outputs = new Dictionary<string, object>();

            for (int i = 0; i < sequencedNodes.Count; i++)
            {
                var node = sequencedNodes[i];
                var nodeContext = i == 0 ? context.NodeContext : PrepareNodeContext(node, workflow, context.NodeContext);

                var nodeResult = await ExecuteNodeAsync(node, nodeContext, context);

                foreach (var kvp in nodeResult.OutputData)
                {
                    outputs[kvp.Key] = kvp.Value;
                    workflow.Variables[kvp.Key] = kvp.Value;
                }

                context.OnProgressChanged?.Invoke((int)((i + 1) * 100.0 / sequencedNodes.Count));

                if (!nodeResult.Success && !nodeResult.IsSkipped && workflow.Configuration.StopOnError)
                {
                    return ExecutionResult.Failed($"节点 '{node.Name}' 执行失败: {nodeResult.Message}", nodeResult.Exception)
                        .WithOutputs(outputs);
                }

                context.CancellationToken.ThrowIfCancellationRequested();
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
                ServiceProvider = baseContext.ServiceProvider
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
            workflowContext.OnNodeExecutionStarted?.Invoke(node, nodeContext);

            ExecutionResult result;
            try
            {
                // 使用扩展方法执行节点
                result = await node.ExecuteAsync(nodeContext, workflowContext.CancellationToken);
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

