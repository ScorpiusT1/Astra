namespace Astra.Core.Reporting
{
    /// <summary>
    /// 测试报告内容过滤。未设置或各过滤字段为空/空白时表示「不限制」（与「全部写入」一致）。
    /// </summary>
    public sealed class ReportGenerationOptions
    {
        /// <summary>
        /// 仅将这些 <see cref="Astra.Core.Nodes.Models.NodeRunRecord.NodeId"/> 对应的单值行写入报告；
        /// 多个 ID 可用逗号、分号或换行分隔；留空表示全部单值节点。
        /// </summary>
        public string? ScalarNodeIdsFilter { get; set; }

        /// <summary>
        /// 仅将这些节点 ID 的曲线判定行写入报告；留空表示全部。
        /// </summary>
        public string? CurveNodeIdsFilter { get; set; }

        /// <summary>
        /// 仅包含产生者为这些节点 ID 的图表（见数据总线 Preview 中 __ProducerNodeId）；留空表示不按产生者过滤。
        /// 若同时设置了 <see cref="ChartArtifactKeysFilter"/>，则以产物 Key 为准。
        /// </summary>
        public string? ChartProducerNodeIdsFilter { get; set; }

        /// <summary>
        /// 仅包含这些 <see cref="Astra.Core.Nodes.Models.DataArtifactReference.Key"/>（精确匹配）；
        /// 多个 Key 用逗号或换行分隔；若非空则优先于 <see cref="ChartProducerNodeIdsFilter"/>。
        /// </summary>
        public string? ChartArtifactKeysFilter { get; set; }

        /// <summary>是否纳入算法类图表（<see cref="Astra.Core.Nodes.Models.DataArtifactCategory.Algorithm"/>）。默认 true。</summary>
        public bool IncludeAlgorithmCharts { get; set; } = true;

        /// <summary>是否纳入可渲染为图表的原始数据类产物（<see cref="Astra.Core.Nodes.Models.DataArtifactCategory.Raw"/>）。默认 true。</summary>
        public bool IncludeRawDataCharts { get; set; } = true;
    }
}
