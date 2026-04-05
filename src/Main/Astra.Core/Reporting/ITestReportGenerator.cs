using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Astra.Core.Archiving;
using Astra.Core.Data;

namespace Astra.Core.Reporting
{
    /// <summary>
    /// 报告导出格式（可组合）。
    /// </summary>
    [System.Flags]
    public enum ReportExportFormats
    {
        Html = 1,
        Pdf = 2,
    }

    /// <summary>
    /// 测试报告生成器接口。
    /// </summary>
    public interface ITestReportGenerator
    {
        Task<ReportOutput> GenerateAsync(TestReportRequest request, CancellationToken cancellationToken);

        /// <summary>
        /// 将多个子流程的 <see cref="TestReportData"/> 合并为一份 HTML/PDF 报告。
        /// </summary>
        Task<ReportOutput> GenerateCombinedAsync(CombinedTestReportRequest request, CancellationToken cancellationToken);
    }

    public sealed class TestReportRequest
    {
        public WorkflowArchiveRequest ArchiveRequest { get; set; } = null!;
        public ITestDataBus? DataBus { get; set; }
        public string OutputDirectory { get; set; } = string.Empty;
        public string FilePrefix { get; set; } = string.Empty;

        /// <summary>是否将成功渲染的图表额外保存为 PNG 文件。</summary>
        public bool ExportChartFiles { get; set; }

        /// <summary>导出为 HTML、PDF 或二者（默认二者）。</summary>
        public ReportExportFormats Formats { get; set; } = ReportExportFormats.Html | ReportExportFormats.Pdf;

        /// <summary>
        /// 是否包含可渲染为 PNG 的原始数据图表；当 <see cref="ReportOptions"/> 非空时以其中的
        /// <see cref="ReportGenerationOptions.IncludeRawDataCharts"/> 为准。
        /// </summary>
        public bool IncludeRawDataCharts { get; set; }

        /// <summary>报告内容过滤；非空时按选项筛选单值、曲线与图表。</summary>
        public ReportGenerationOptions? ReportOptions { get; set; }

        /// <summary>来自软件配置的测试工位名；非空时优先于 NodeContext 全局变量。</summary>
        public string? ReportStationFromConfig { get; set; }

        /// <summary>来自软件配置的测试线体名；非空时优先于 NodeContext 全局变量。</summary>
        public string? ReportLineFromConfig { get; set; }
    }

    /// <summary>
    /// 合并报告请求：将多个子流程的报告数据输出为一份文件。
    /// </summary>
    public sealed class CombinedTestReportRequest
    {
        public IReadOnlyList<TestReportData> Sections { get; set; } = [];
        public string OutputDirectory { get; set; } = string.Empty;
        public string FilePrefix { get; set; } = string.Empty;
        public ReportExportFormats Formats { get; set; } = ReportExportFormats.Html | ReportExportFormats.Pdf;
    }

    public sealed class ReportOutput
    {
        public bool Success { get; set; }
        public string? HtmlPath { get; set; }
        public string? PdfPath { get; set; }
        public List<string> ChartImagePaths { get; set; } = new();

        /// <summary>
        /// 已采集并渲染图表的报告数据模型。
        /// <see cref="ITestReportGenerator.GenerateAsync"/> 始终只采集数据；
        /// 文件写入统一走 <see cref="ITestReportGenerator.GenerateCombinedAsync"/>。
        /// </summary>
        public TestReportData? ReportData { get; set; }
    }
}
