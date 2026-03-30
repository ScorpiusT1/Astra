using System.Collections.Generic;
using Astra.Core.Nodes.Models;

namespace Astra.Core.Data
{
    /// <summary>
    /// 统一测试数据总线接口（per Execution 生命周期）。
    /// 节点通过 Publish 写入各类数据产物，下游节点通过 Query / TryGet 按类别发现与消费。
    /// 底层委托给 <see cref="IRawDataStore"/>，无额外存储开销。
    /// </summary>
    public interface ITestDataBus
    {
        string ExecutionId { get; }

        /// <summary>发布一条数据产物到总线。</summary>
        DataArtifactReference Publish(DataEntry entry);

        /// <summary>按 Key 精确读取产物数据。</summary>
        bool TryGet<T>(string artifactKey, out T data);

        /// <summary>按 Category / 生产者节点 / Tag 组合查询所有匹配的产物引用。</summary>
        IReadOnlyList<DataArtifactReference> Query(
            DataArtifactCategory? category = null,
            string? producerNodeId = null,
            string? tag = null);

        /// <summary>按 Category + Tag 获取最近一条产物引用。</summary>
        DataArtifactReference? FindLatest(DataArtifactCategory category, string? tag = null);

        /// <summary>标记某产物已被指定节点消费（用于数据血缘追踪）。</summary>
        void MarkConsumed(string artifactKey, string consumerNodeId);

        /// <summary>创建当前总线的完整快照（用于归档 / 报告）。</summary>
        DataPipelineSnapshot CreateSnapshot();
    }
}
