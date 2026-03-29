using System;
using System.Collections.Generic;

namespace Astra.Core.Nodes.Models
{
    /// <summary>
    /// 节点产物分类（用于统一描述采集数据、算法数据等）。
    /// </summary>
    public enum DataArtifactCategory
    {
        Unknown = 0,
        Raw = 1,
        Algorithm = 2,
        Feature = 3,
        Debug = 4,

        /// <summary>
        /// 单值/标量数据（如数值、布尔、枚举等）。
        /// </summary>
        Scalar = 5,

        /// <summary>
        /// 文本数据（如 string、JSON 文本等）。
        /// </summary>
        Text = 6
    }

    /// <summary>
    /// 统一的数据产物引用描述（避免在结果链中直接携带大对象）。
    /// </summary>
    public class DataArtifactReference
    {
        public string Key { get; set; }
        public DataArtifactCategory Category { get; set; } = DataArtifactCategory.Unknown;
        public string DataType { get; set; }
        public string DisplayName { get; set; }
        public long? SizeBytes { get; set; }
        public string Description { get; set; }
        public Dictionary<string, object> Preview { get; set; } = new Dictionary<string, object>();
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// 原始数据引用描述（避免在结果链中直接携带大对象）。
    /// </summary>
    public class RawDataReference : DataArtifactReference
    {
        public RawDataReference()
        {
            Category = DataArtifactCategory.Raw;
        }
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

        /// <summary>
        /// 快照指定前缀下的键值（用于归档等）。不支持时返回 false。
        /// </summary>
        bool TrySnapshotByPrefix(string prefix, out IReadOnlyList<KeyValuePair<string, object>> items);
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
        public List<DataArtifactReference> DataArtifacts { get; set; } = new List<DataArtifactReference>();
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
