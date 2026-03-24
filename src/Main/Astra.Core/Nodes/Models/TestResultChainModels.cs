using System;
using System.Collections.Generic;

namespace Astra.Core.Nodes.Models
{
    /// <summary>
    /// 原始数据引用描述（避免在结果链中直接携带大对象）。
    /// </summary>
    public class RawDataReference
    {
        public string Key { get; set; }
        public string DataType { get; set; }
        public long? SizeBytes { get; set; }
        public string Description { get; set; }
    }

    /// <summary>
    /// 原始数据存储接口（执行期共享，支持节点间原始数据传递）。
    /// </summary>
    public interface IRawDataStore
    {
        void Set(string key, object value);
        bool TryGet(string key, out object value);
        bool Remove(string key);
        int RemoveByPrefix(string keyPrefix);
        RawDataStoreStats GetStats();
    }

    /// <summary>
    /// 原始数据存储运行统计。
    /// </summary>
    public class RawDataStoreStats
    {
        public int ItemCount { get; set; }
        public long EstimatedBytes { get; set; }
        public long MaxBytes { get; set; }
        public int MaxItems { get; set; }
        public long HitCount { get; set; }
        public long MissCount { get; set; }
        public double HitRate => HitCount + MissCount == 0 ? 0 : (HitCount * 100.0) / (HitCount + MissCount);
    }

    /// <summary>
    /// 节点执行明细记录。
    /// </summary>
    public class NodeRunRecord
    {
        public string ExecutionId { get; set; }
        public string WorkFlowKey { get; set; }
        public string NodeId { get; set; }
        public string NodeName { get; set; }
        public NodeExecutionState State { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public TimeSpan Duration => EndTime - StartTime;
        public string Message { get; set; }
        public string ErrorCode { get; set; }
        public string SkipReason { get; set; }
        public Dictionary<string, object> InputSnapshot { get; set; } = new Dictionary<string, object>();
        public Dictionary<string, object> OutputSnapshot { get; set; } = new Dictionary<string, object>();
        public List<RawDataReference> RawDataReferences { get; set; } = new List<RawDataReference>();
    }

    /// <summary>
    /// 工作流执行结果链记录。
    /// </summary>
    public class WorkFlowRunRecord
    {
        public string ExecutionId { get; set; }
        public string WorkFlowKey { get; set; }
        public string WorkFlowName { get; set; }
        public string Strategy { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public string Status { get; set; }
        public ExecutionResult FinalResult { get; set; }
        public List<NodeRunRecord> NodeRuns { get; set; } = new List<NodeRunRecord>();
    }
}
