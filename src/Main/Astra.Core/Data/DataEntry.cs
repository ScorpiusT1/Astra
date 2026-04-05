using System.Collections.Generic;
using Astra.Core.Nodes.Models;

namespace Astra.Core.Data
{
    /// <summary>
    /// 数据产物写入描述（由节点在 Publish 时构造）。
    /// </summary>
    public sealed class DataEntry
    {
        /// <summary>生产者节点 ID。</summary>
        public string ProducerNodeId { get; set; } = string.Empty;

        /// <summary>产物分类。</summary>
        public DataArtifactCategory Category { get; set; }

        /// <summary>产物名称（同一节点同一 Category 下需唯一）。</summary>
        public string ArtifactName { get; set; } = string.Empty;

        /// <summary>实际数据对象（大对象通过引用存入 RawDataStore，不做拷贝）。</summary>
        public object? Data { get; set; }

        /// <summary>显示名称（报告 / UI 中使用）。</summary>
        public string? DisplayName { get; set; }

        /// <summary>描述文本。</summary>
        public string? Description { get; set; }

        /// <summary>自由标签，用于同 Category 下细分（如 "vibration"、"order-spectrum"）。</summary>
        public string? Tag { get; set; }

        /// <summary>可选的预览元数据（轻量 KV，会写入 <see cref="DataArtifactReference.Preview"/>）。</summary>
        public Dictionary<string, object>? Preview { get; set; }

        /// <summary>是否纳入测试报告（写入 Preview 中 <see cref="Astra.Core.Reporting.ReportIncludeKeys.IncludeInReport"/>）。默认 true。</summary>
        public bool IncludeInTestReport { get; set; } = true;
    }
}
