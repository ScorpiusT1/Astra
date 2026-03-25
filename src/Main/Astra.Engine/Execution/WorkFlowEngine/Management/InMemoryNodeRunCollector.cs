using Astra.Core.Nodes.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Astra.Engine.Execution.WorkFlowEngine.Management
{
    /// <summary>
    /// 基于内存的节点执行记录采集器。
    /// </summary>
    public class InMemoryNodeRunCollector : INodeRunCollector
    {
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, NodeRunRecord>> _recordsByExecution
            = new ConcurrentDictionary<string, ConcurrentDictionary<string, NodeRunRecord>>();

        public void MarkNodeStarted(string executionId, string workFlowKey, Node node, NodeContext context, DateTime startTime)
        {
            if (string.IsNullOrWhiteSpace(executionId) || node == null) return;

            var executionMap = _recordsByExecution.GetOrAdd(executionId, _ => new ConcurrentDictionary<string, NodeRunRecord>());
            executionMap[node.Id] = new NodeRunRecord
            {
                ExecutionId = executionId,
                WorkFlowKey = workFlowKey,
                NodeId = node.Id,
                NodeName = node.Name,
                State = node.ExecutionState,
                StartTime = startTime,
                InputSnapshot = CloneDictionary(context?.InputData)
            };
        }

        public void MarkNodeCompleted(string executionId, string workFlowKey, Node node, NodeContext context, ExecutionResult result, DateTime endTime)
        {
            if (string.IsNullOrWhiteSpace(executionId) || node == null || result == null) return;

            var executionMap = _recordsByExecution.GetOrAdd(executionId, _ => new ConcurrentDictionary<string, NodeRunRecord>());
            var record = executionMap.GetOrAdd(node.Id, _ => new NodeRunRecord
            {
                ExecutionId = executionId,
                WorkFlowKey = workFlowKey,
                NodeId = node.Id,
                NodeName = node.Name,
                StartTime = result.StartTime ?? endTime
            });

            record.EndTime = endTime;
            record.State = node.ExecutionState;
            record.Message = result.Message;
            record.ErrorCode = result.ErrorCode;
            record.OutputSnapshot = CloneDictionary(result.OutputData);
            record.SkipReason = result.GetOutput("SkipReason", string.Empty);

            foreach (var kvp in result.OutputData)
            {
                if (kvp.Value is DataArtifactReference artifactRef)
                {
                    record.DataArtifacts.Add(artifactRef);
                }

                if (kvp.Value is RawDataReference rawRef)
                {
                    record.RawDataReferences.Add(rawRef);
                }
            }
        }

        public IReadOnlyList<NodeRunRecord> GetByExecutionId(string executionId)
        {
            if (string.IsNullOrWhiteSpace(executionId)) return Array.Empty<NodeRunRecord>();
            if (!_recordsByExecution.TryGetValue(executionId, out var executionMap))
            {
                return Array.Empty<NodeRunRecord>();
            }

            return executionMap.Values.OrderBy(v => v.StartTime).ToList();
        }

        public void Clear(string executionId)
        {
            if (string.IsNullOrWhiteSpace(executionId)) return;
            _recordsByExecution.TryRemove(executionId, out _);
        }

        private static Dictionary<string, object> CloneDictionary(Dictionary<string, object> source)
        {
            return source == null
                ? new Dictionary<string, object>()
                : new Dictionary<string, object>(source);
        }
    }
}
