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

namespace Astra.Plugins.WorkflowArchive.Reporting
{
    /// <summary>
    /// 测试报告：采集 → PNG 物化（一次）→ HTML / PDF 双渲染。
    /// </summary>
    public sealed class DefaultTestReportGenerator : ITestReportGenerator
    {
        private readonly ILogger _logger;

        public DefaultTestReportGenerator(ILogger<DefaultTestReportGenerator>? logger = null)
        {
            _logger = logger ?? (ILogger)NullLogger.Instance;
        }

        /// <summary>
        /// 无 <see cref="TestReportRequest.ReportOptions"/> 时（如引擎归档）使用「全部单值/曲线/算法图」，
        /// Raw 图是否纳入由 <see cref="TestReportRequest.IncludeRawDataCharts"/> 决定。
        /// </summary>
        private static ReportGenerationOptions MergeReportOptions(TestReportRequest request)
        {
            if (request.ReportOptions != null)
                return request.ReportOptions;

            return new ReportGenerationOptions
            {
                IncludeAlgorithmCharts = true,
                IncludeRawDataCharts = request.IncludeRawDataCharts
            };
        }

        public async Task<ReportOutput> GenerateAsync(TestReportRequest request, CancellationToken cancellationToken)
        {
            var archiveReq = request.ArchiveRequest;
            var runRecord = archiveReq?.RunRecord;
            if (runRecord == null)
            {
                return new ReportOutput { Success = false };
            }

            var formats = request.Formats;
            if (formats == 0)
                formats = ReportExportFormats.Html | ReportExportFormats.Pdf;

            var reportData = ReportDataCollector.Collect(
                runRecord,
                request.DataBus,
                archiveReq!.NodeContext,
                MergeReportOptions(request));

            var chartPaths = new List<string>();
            await RenderChartsAsync(request, reportData, chartPaths, cancellationToken);
            RenderCurveJudgmentCharts(request, reportData);

            Directory.CreateDirectory(request.OutputDirectory);

            string? htmlPath = null;
            if ((formats & ReportExportFormats.Html) != 0)
            {
                var html = ReportHtmlBuilder.Build(reportData);
                htmlPath = Path.Combine(request.OutputDirectory, $"{request.FilePrefix}_report.html");
                await File.WriteAllTextAsync(htmlPath, html, new UTF8Encoding(true), cancellationToken);
            }

            string? pdfPath = null;
            if ((formats & ReportExportFormats.Pdf) != 0)
            {
                pdfPath = Path.Combine(request.OutputDirectory, $"{request.FilePrefix}_report.pdf");
                await Task.Run(() => ReportPdfBuilder.Build(reportData, pdfPath), cancellationToken);
            }

            return new ReportOutput
            {
                Success = true,
                HtmlPath = htmlPath,
                PdfPath = pdfPath,
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
