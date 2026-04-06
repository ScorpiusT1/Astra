using System;
using System.Collections.Generic;

namespace Astra.Core.Reporting
{
    /// <summary>写入 <see cref="Astra.Core.Nodes.Models.DataArtifactReference"/> 的 <c>Preview</c> 字典的键：报告标题「设备/通道」段。</summary>
    public static class ReportArtifactPreviewKeys
    {
        public const string DeviceChannel = "__ReportDeviceChannel";
        public const string ExportAlgorithmData = "__ExportAlgorithmData";

        /// <summary>
        /// 可选：总线为 <c>Algorithm</c> 的产物在报告中的分层，取值为 <see cref="ReportChartSourceKind"/> 枚举名：
        /// <c>Raw</c>（如文件导入预览波形）、<c>CurveResult</c>（限值曲线重发布）、缺省为 <c>Algorithm</c>（频谱等）。
        /// 总线类别已为 <c>Raw</c> 的采集/导入 Raw 数据不需本键。
        /// </summary>
        public const string ChartReportSourceKind = "__ChartReportSourceKind";
    }

    /// <summary>写入 <see cref="Astra.Core.Nodes.Models.NodeContext"/> 全局变量的键（合并报告按工况编排顺序排序）。</summary>
    public static class ReportContextKeys
    {
        /// <summary>整型，从 0 递增：对主计划 <c>OrderedEntries</c>（含非 ExecuteLast 段后接 ExecuteLast 段）中的位置。</summary>
        public const string SectionSequenceOrder = "__ReportSectionSequence";
    }

    /// <summary>
    /// 图表在报告中的来源分类（用于 HTML/PDF 分层章节与标题前缀）。
    /// </summary>
    public enum ReportChartSourceKind
    {
        /// <summary>算法数据图表：频谱、阶次、滤波等算法节点输出（总线缺省分层）。</summary>
        Algorithm = 0,

        /// <summary>原始数据图表：数据采集、文件导入 Raw 及导入预览（经 Preview 标记的 Algorithm 载荷）等。</summary>
        Raw = 1,

        /// <summary>曲线数据：Limits 库节点为报告重新发布的曲线图（与曲线判定表配套）。</summary>
        CurveResult = 2
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

        /// <summary>曲线卡控等节点输出的标量摘要（如失败点值或通过后 max）；可选。</summary>
        public double? ActualValue { get; set; }

        /// <summary>合格带下限；曲线逐点卡控等场景写入报告表与附图参考线。</summary>
        public double? LowerLimit { get; set; }

        /// <summary>合格带上限。</summary>
        public double? UpperLimit { get; set; }

        public bool Pass { get; set; }
        public string? FailDetail { get; set; }

        /// <summary>渲染后的图表 PNG base64（报告中内嵌）。</summary>
        public string? ChartImageBase64 { get; set; }

        /// <summary>报告图表上方标题：测试项-设备/通道-名称（曲线判定场景下中段多为判定节点名）。</summary>
        public string ReportHeading { get; set; } = string.Empty;

        /// <summary>
        /// 节点输出中的图表产物总线键（与运行记录里 <c>Ui.ChartArtifactKey</c> 一致）；
        /// 渲染曲线附图时优先使用该键，避免仅按生产者 Id 扫描时误选其它已纳入报告的算法图（如导入预览）。
        /// </summary>
        public string? PreferredChartArtifactKey { get; set; }
    }

    public sealed class ChartSection
    {
        public string Title { get; set; } = string.Empty;
        public string NodeName { get; set; } = string.Empty;
        public string? Description { get; set; }

        /// <summary>报告分层：<see cref="ReportChartSourceKind.Raw"/> / <see cref="ReportChartSourceKind.Algorithm"/> / <see cref="ReportChartSourceKind.CurveResult"/>。</summary>
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
