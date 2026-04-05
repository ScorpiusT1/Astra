using Astra.Core.Nodes.Management;
using Astra.Core.Nodes.Models;
using Astra.Core.Nodes.Ui;
using Astra.Engine.Execution.WorkFlowEngine;
using Astra.UI.Abstractions.Nodes;
using Astra.UI.Services;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Astra.Services.Startup
{
    public sealed class DefaultWorkflowEngineProvider : IWorkflowEngineProvider
    {
        private readonly INodeExecutionUiHydrator? _nodeExecutionUiHydrator;

        public event EventHandler<WorkflowNodeExecutionChangedEventArgs>? NodeExecutionChanged;

        public DefaultWorkflowEngineProvider(INodeExecutionUiHydrator? nodeExecutionUiHydrator = null)
        {
            _nodeExecutionUiHydrator = nodeExecutionUiHydrator;
        }

        public IWorkFlowEngine Create()
        {
            return WorkFlowEngineFactory.CreateDefault();
        }

        public IWorkFlowEngine CreateWithNodeEventBridge()
        {
            var engine = WorkFlowEngineFactory.CreateDefault();
            if (engine == null)
                return null!;

            engine.NodeExecutionStarted += (_, e) =>
            {
                if (e?.Node == null) return;

                NodeExecutionChanged?.Invoke(this, new WorkflowNodeExecutionChangedEventArgs
                {
                    ExecutionId = ResolveExecutionId(e.Context),
                    WorkflowKey = ResolveWorkflowKey(e.Context),
                    NodeId = e.Node.Id,
                    State = NodeExecutionState.Running,
                    DetailMessage = null
                });
            };

            engine.ParallelWaveExecutionStarted += (_, wave) =>
            {
                if (wave?.Nodes == null || wave.Nodes.Count == 0) return;
                var first = wave.Nodes[0];
                if (first?.Node == null || first.Context == null) return;

                var ids = wave.Nodes
                    .Where(x => x?.Node != null && !string.IsNullOrWhiteSpace(x.Node.Id))
                    .Select(x => x.Node!.Id)
                    .ToList();
                if (ids.Count == 0) return;

                NodeExecutionChanged?.Invoke(this, new WorkflowNodeExecutionChangedEventArgs
                {
                    ExecutionId = ResolveExecutionId(first.Context),
                    WorkflowKey = ResolveWorkflowKey(first.Context),
                    NodeId = ids[0],
                    State = NodeExecutionState.Running,
                    DetailMessage = null,
                    ParallelRunningNodeIds = ids
                });
            };

            engine.NodeExecutionCompleted += (_, e) =>
            {
                if (e?.Node == null) return;

                _nodeExecutionUiHydrator?.OnNodeExecutionCompleted(e.Context, e.Node.Id, e.Result);

                NodeExecutionChanged?.Invoke(this, new WorkflowNodeExecutionChangedEventArgs
                {
                    ExecutionId = ResolveExecutionId(e.Context),
                    WorkflowKey = ResolveWorkflowKey(e.Context),
                    NodeId = e.Node.Id,
                    State = MapState(e.Result),
                    DetailMessage = BuildNodeDetailMessage(e.Result),
                    UiPayload = NodeUiPayloadFactory.FromOutputData(e.Result?.OutputData)
                });
            };

            return engine;
        }

        private static string ResolveExecutionId(NodeContext context)
        {
            if (context?.Metadata != null &&
                context.Metadata.TryGetValue("ExecutionId", out var v) && v is string id)
                return id;
            return context?.ExecutionId ?? string.Empty;
        }

        private static string ResolveWorkflowKey(NodeContext context)
        {
            if (context?.Metadata != null &&
                context.Metadata.TryGetValue("WorkFlowKey", out var v) && v is string key)
                return key;
            return string.Empty;
        }

        private static NodeExecutionState MapState(ExecutionResult? result)
        {
            if (result == null) return NodeExecutionState.Failed;
            if (result.ResultType == ExecutionResultType.Cancelled) return NodeExecutionState.Cancelled;
            if (result.IsSkipped || result.ResultType == ExecutionResultType.Skipped) return NodeExecutionState.Skipped;
            return result.Success ? NodeExecutionState.Success : NodeExecutionState.Failed;
        }

        private static string? BuildNodeDetailMessage(ExecutionResult? r)
        {
            if (r == null) return "未返回结果";
            if (r.IsSkipped || r.ResultType == ExecutionResultType.Skipped)
                return string.IsNullOrWhiteSpace(r.Message) ? "已跳过" : r.Message.Trim();
            if (r.ResultType == ExecutionResultType.Cancelled)
                return string.IsNullOrWhiteSpace(r.Message) ? "已取消" : r.Message.Trim();

            if (!r.Success)
            {
                var parts = new List<string>();
                if (r.OutputData != null &&
                    r.OutputData.TryGetValue(NodeUiOutputKeys.Summary, out var sumObj) &&
                    sumObj != null && !string.IsNullOrWhiteSpace(sumObj.ToString()))
                    parts.Add(sumObj.ToString()!.Trim());
                if (!string.IsNullOrWhiteSpace(r.Message))
                    parts.Add(r.Message.Trim());
                if (!string.IsNullOrWhiteSpace(r.ErrorCode))
                    parts.Add($"错误码 {r.ErrorCode}");
                if (r.Exception != null && !string.IsNullOrWhiteSpace(r.Exception.Message))
                    parts.Add(r.Exception.Message);
                return parts.Count > 0 ? string.Join(" · ", parts) : "执行失败";
            }

            if (r.OutputData != null &&
                r.OutputData.TryGetValue(NodeUiOutputKeys.Summary, out var okSum) &&
                okSum != null && !string.IsNullOrWhiteSpace(okSum.ToString()))
                return okSum.ToString()!.Trim();

            return string.IsNullOrWhiteSpace(r.Message) ? string.Empty : r.Message.Trim();
        }
    }
}
