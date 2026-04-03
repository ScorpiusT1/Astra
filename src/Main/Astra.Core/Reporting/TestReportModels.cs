using System;
using System.Collections.Generic;

namespace Astra.Core.Reporting
{
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
        public string Condition { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public string OverallResult { get; set; } = "UNK";
        public string Strategy { get; set; } = string.Empty;

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

        /// <summary>
        /// 单图时为整张图高度（像素）。多子图（分子图 / SubPlots 布局）报告渲染时表示<strong>每一行</strong>的期望高度；
        /// 导出 PNG 总高度 ≈ 行数 × 本值（由 <c>ReportChartRenderer</c> 夹紧）。
        /// </summary>
        public int Height { get; set; } = 400;

        /// <summary>引用的 artifact key（用于渲染时取 payload）。</summary>
        public string? ArtifactKey { get; set; }
    }
}
