using Astra.Core.Nodes.Models;
using System;
using System.Collections.Generic;

namespace Astra.Engine.Execution.WorkFlowEngine.Management
{
    /// <summary>
    /// 节点执行记录采集器。
    /// </summary>
    public interface INodeRunCollector
    {
        void MarkNodeStarted(string executionId, string workFlowKey, Node node, NodeContext context, DateTime startTime);
        void MarkNodeCompleted(string executionId, string workFlowKey, Node node, NodeContext context, ExecutionResult result, DateTime endTime);
        IReadOnlyList<NodeRunRecord> GetByExecutionId(string executionId);
        void Clear(string executionId);
    }
}
