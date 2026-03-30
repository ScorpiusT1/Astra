using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Astra.Core.Data;
using Astra.Core.Nodes.Models;
using Astra.Core.Reporting;
using Astra.UI.Abstractions.Nodes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Astra.Services.Reporting
{
    /// <summary>
    /// 默认测试报告生成器：串联 DataCollector → ChartRenderer → HtmlBuilder。
    /// </summary>
    public sealed class DefaultTestReportGenerator : ITestReportGenerator
    {
        private readonly ILogger _logger;

        public DefaultTestReportGenerator(ILogger<DefaultTestReportGenerator>? logger = null)
        {
            _logger = logger ?? (ILogger)NullLogger.Instance;
        }

        public async Task<ReportOutput> GenerateAsync(TestReportRequest request, CancellationToken cancellationToken)
        {
            var archiveReq = request.ArchiveRequest;
            var runRecord = archiveReq?.RunRecord;
            if (runRecord == null)
            {
                return new ReportOutput { Success = false };
            }

            var reportData = ReportDataCollector.Collect(
                runRecord, request.DataBus, archiveReq!.NodeContext);

            var chartPaths = new List<string>();
            await RenderChartsAsync(request, reportData, chartPaths, cancellationToken);
            RenderCurveJudgmentCharts(request, reportData);

            var html = ReportHtmlBuilder.Build(reportData);
            var htmlPath = Path.Combine(request.OutputDirectory,
                $"{request.FilePrefix}_report.html");

            Directory.CreateDirectory(request.OutputDirectory);
            await File.WriteAllTextAsync(htmlPath, html, new UTF8Encoding(true), cancellationToken);

            return new ReportOutput
            {
                Success = true,
                HtmlPath = htmlPath,
                ChartImagePaths = chartPaths
            };
        }

        private async Task RenderChartsAsync(
            TestReportRequest request,
            TestReportData reportData,
            List<string> chartPaths,
            CancellationToken ct)
        {
            for (int i = 0; i < reportData.Charts.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                var chart = reportData.Charts[i];
                byte[]? pngBytes = null;

                try
                {
                    pngBytes = TryRenderChartPayload(request.DataBus, chart);
                    pngBytes ??= TryRenderNvhRaw(request.DataBus, chart);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "渲染图表 [{Title}] 失败", chart.Title);
                }

                if (pngBytes is { Length: > 0 })
                {
                    chart.ImageBase64 = Convert.ToBase64String(pngBytes);

                    if (request.ExportChartFiles)
                    {
                        var pngPath = Path.Combine(request.OutputDirectory,
                            $"{request.FilePrefix}_chart_{i}.png");
                        await File.WriteAllBytesAsync(pngPath, pngBytes, ct);
                        chartPaths.Add(pngPath);
                    }
                }
            }
        }

        private static byte[]? TryRenderChartPayload(ITestDataBus? bus, ChartSection chart)
        {
            if (bus == null || string.IsNullOrEmpty(chart.ArtifactKey))
                return null;

            if (!bus.TryGet<ChartDisplayPayload>(chart.ArtifactKey, out var payload) || payload == null)
                return null;

            return ReportChartRenderer.RenderToPng(payload, chart.Width, chart.Height, chart.Title);
        }

        private static byte[]? TryRenderNvhRaw(ITestDataBus? bus, ChartSection chart)
        {
            if (bus == null || string.IsNullOrEmpty(chart.ArtifactKey))
                return null;

            if (!bus.TryGet<object>(chart.ArtifactKey, out var rawObj) || rawObj == null)
                return null;

            if (rawObj is not ChartDisplayPayload)
            {
                // NvhMemoryFile 等原始数据暂不在此处渲染（依赖 NVH 库），
                // 未来可扩展 INvhChartRenderer 接口来处理。
                return null;
            }

            return null;
        }

        private static void RenderCurveJudgmentCharts(TestReportRequest request, TestReportData reportData)
        {
            if (request.DataBus == null) return;

            foreach (var cj in reportData.CurveJudgments)
            {
                try
                {
                    var algRefs = request.DataBus.Query(DataArtifactCategory.Algorithm);
                    foreach (var algRef in algRefs)
                    {
                        if (!request.DataBus.TryGet<ChartDisplayPayload>(algRef.Key, out var p) || p == null)
                            continue;

                        var png = ReportChartRenderer.RenderToPng(p, 600, 300, cj.CurveName);
                        if (png is { Length: > 0 })
                        {
                            cj.ChartImageBase64 = Convert.ToBase64String(png);
                            break;
                        }
                    }
                }
                catch
                {
                    // best-effort
                }
            }
        }
    }
}
