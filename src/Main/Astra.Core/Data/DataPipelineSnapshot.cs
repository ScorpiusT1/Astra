using System;
using System.Collections.Generic;
using Astra.Core.Nodes.Models;

namespace Astra.Core.Data
{
    /// <summary>
    /// 数据总线快照（用于归档 / 报告生成）。
    /// </summary>
    public sealed class DataPipelineSnapshot
    {
        public string ExecutionId { get; set; } = string.Empty;

        /// <summary>全部已注册的产物引用。</summary>
        public List<DataArtifactReference> Artifacts { get; set; } = new();

        /// <summary>数据血缘：artifactKey → 消费者节点 ID 列表。</summary>
        public Dictionary<string, IReadOnlyList<string>> Lineage { get; set; } = new();

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
