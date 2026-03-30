using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Astra.Core.Archiving;
using Astra.Core.Data;

namespace Astra.Core.Reporting
{
    /// <summary>
    /// 测试报告生成器接口。
    /// </summary>
    public interface ITestReportGenerator
    {
        Task<ReportOutput> GenerateAsync(TestReportRequest request, CancellationToken cancellationToken);
    }

    public sealed class TestReportRequest
    {
        public WorkflowArchiveRequest ArchiveRequest { get; set; } = null!;
        public ITestDataBus? DataBus { get; set; }
        public string OutputDirectory { get; set; } = string.Empty;
        public string FilePrefix { get; set; } = string.Empty;
        public bool ExportChartFiles { get; set; }
    }

    public sealed class ReportOutput
    {
        public bool Success { get; set; }
        public string? HtmlPath { get; set; }
        public List<string> ChartImagePaths { get; set; } = new();
    }
}
