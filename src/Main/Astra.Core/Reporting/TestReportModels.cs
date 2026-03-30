using System;
using System.Collections.Generic;

namespace Astra.Core.Reporting
{
    /// <summary>
    /// 测试报告完整数据模型。
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
        public List<NodeRunSummary> NodeSummaries { get; set; } = new();
        public List<TextArtifactSection> TextArtifacts { get; set; } = new();
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

        /// <summary>渲染后的图表 PNG base64。</summary>
        public string? ImageBase64 { get; set; }

        public int Width { get; set; } = 800;
        public int Height { get; set; } = 400;

        /// <summary>引用的 artifact key（用于渲染时取 payload）。</summary>
        public string? ArtifactKey { get; set; }
    }

    public sealed class NodeRunSummary
    {
        public string NodeName { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string? Message { get; set; }
        public TimeSpan Duration { get; set; }
    }

    public sealed class TextArtifactSection
    {
        public string Title { get; set; } = string.Empty;
        public string ContentType { get; set; } = "text/plain";
        public string Content { get; set; } = string.Empty;
    }
}
