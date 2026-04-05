using System;
using System.Collections.Generic;

namespace Astra.Core.Reporting
{
    /// <summary>写入 <see cref="Astra.Core.Nodes.Models.DataArtifactReference"/> 的 <c>Preview</c> 字典的键：报告标题「设备/通道」段。</summary>
    public static class ReportArtifactPreviewKeys
    {
        public const string DeviceChannel = "__ReportDeviceChannel";
    }

    /// <summary>写入 <see cref="Astra.Core.Nodes.Models.NodeContext"/> 全局变量的键（合并报告按工况编排顺序排序）。</summary>
    public static class ReportContextKeys
    {
        /// <summary>整型，从 0 递增：对主计划 <c>OrderedEntries</c>（含非 ExecuteLast 段后接 ExecuteLast 段）中的位置。</summary>
        public const string SectionSequenceOrder = "__ReportSectionSequence";
    }

    /// <summary>
    /// 图表在报告中的来源分类（用于章节与排版）。
    /// </summary>
    public enum ReportChartSourceKind
    {
        Algorithm = 0,
        Raw = 1
    }

    /// <summary>
    /// 测试报告完整数据模型（仅含最终判定与结果图，不含节点执行过程与文本附录）。
    /// </summary>
    public sealed class TestReportData
    {
        public string ExecutionId { get; set; } = string.Empty;
        public string WorkFlowName { get; set; } = string.Empty;
        public string SN { get; set; } = string.Empty;

        /// <summary>测试工位（来自 NodeContext 全局变量 <c>测试工位</c>/<c>TestStation</c>，未配置时为空）。</summary>
        public string TestStation { get; set; } = string.Empty;

        /// <summary>测试线体（来自 NodeContext 全局变量 <c>测试线体</c>/<c>TestLine</c>，未配置时为空）。</summary>
        public string TestLine { get; set; } = string.Empty;

        public string Condition { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public string OverallResult { get; set; } = "UNK";
        public string Strategy { get; set; } = string.Empty;

        /// <summary>主流程编排顺序（<see cref="ReportContextKeys.SectionSequenceOrder"/>），合并报告与 JSON/CSV 按此排序；未设置时为 0。</summary>
        public int SectionSequenceOrder { get; set; }

        public List<ScalarJudgmentRow> ScalarJudgments { get; set; } = new();
        public List<CurveJudgmentRow> CurveJudgments { get; set; } = new();
        public List<ChartSection> Charts { get; set; } = new();
    }

    public sealed class ScalarJudgmentRow
    {
        public string NodeName { get; set; } = string.Empty;
        public string ParameterName { get; set; } = string.Empty;
        public double? ActualValue { get; set; }
        public double? LowerLimit { get; set; }
        public double? UpperLimit { get; set; }
        public string Unit { get; set; } = string.Empty;
        public bool Pass { get; set; }
    }

    public sealed class CurveJudgmentRow
    {
        /// <summary>节点 ID，与数据总线产物 <c>__ProducerNodeId</c> 对齐，用于报告曲线配图匹配。</summary>
        public string NodeId { get; set; } = string.Empty;

        public string NodeName { get; set; } = string.Empty;
        public string CurveName { get; set; } = string.Empty;
        public bool Pass { get; set; }
        public string? FailDetail { get; set; }

        /// <summary>渲染后的图表 PNG base64（报告中内嵌）。</summary>
        public string? ChartImageBase64 { get; set; }

        /// <summary>报告图表上方标题：测试项-设备/通道-名称（曲线判定场景下中段多为判定节点名）。</summary>
        public string ReportHeading { get; set; } = string.Empty;
    }

    public sealed class ChartSection
    {
        public string Title { get; set; } = string.Empty;
        public string NodeName { get; set; } = string.Empty;
        public string? Description { get; set; }

        public ReportChartSourceKind SourceKind { get; set; } = ReportChartSourceKind.Algorithm;

        /// <summary>渲染后的图表 PNG base64。</summary>
        public string? ImageBase64 { get; set; }

        public int Width { get; set; } = 800;

        /// <summary>单张导出 PNG 的高度（像素）。分子图（SubPlots）已拆成多张独立图时，每张均使用本高度。</summary>
        public int Height { get; set; } = 400;

        /// <summary>引用的 artifact key（用于渲染时取 payload）。</summary>
        public string? ArtifactKey { get; set; }

        /// <summary>
        /// 非 null 时：从 <see cref="ArtifactKey"/> 对应载荷的 <c>Series[index]</c> 单独成图（分子图拆分，报告里每图一行）。
        /// </summary>
        public int? SubPlotSeriesIndex { get; set; }

        /// <summary>报告图表区块主标题：测试项名字-设备/通道名-算法名（由采集阶段根据总线 Preview 等拼装）。</summary>
        public string ReportHeading { get; set; } = string.Empty;
    }
}
