using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Astra.Core.Data;
using Astra.Core.Nodes.Models;
using Astra.Core.Reporting;
using Astra.UI.Abstractions.Nodes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Astra.Reporting
{
    /// <summary>
    /// 测试报告生成器的默认实现：实现 <see cref="ITestReportGenerator"/>，
    /// 从归档请求与 <see cref="ITestDataBus"/> 汇总 <see cref="TestReportData"/>，将图表渲染为 PNG 并写入 Base64，
    /// 供 HTML 等导出使用；亦支持多工况合并导出。
    /// </summary>
    public sealed class DefaultTestReportGenerator : ITestReportGenerator
    {
        private readonly ILogger _logger;

        /// <summary>
        /// 使用可选日志创建生成器实例。
        /// </summary>
        /// <param name="logger">日志记录器；为 <c>null</c> 时使用 <see cref="NullLogger"/>，不产生日志输出。</param>
        public DefaultTestReportGenerator(ILogger<DefaultTestReportGenerator>? logger = null)
        {
            _logger = logger ?? (ILogger)NullLogger.Instance;
        }

        /// <summary>
        /// 解析单次报告请求中的图表包含策略：若已提供 <see cref="TestReportRequest.ReportOptions"/> 则直接采用；
        /// 否则依据 <see cref="TestReportRequest.IncludeRawDataCharts"/> 等构造默认 <see cref="ReportGenerationOptions"/>。
        /// </summary>
        /// <param name="request">单次测试报告请求。</param>
        /// <returns>传入 <see cref="ReportDataCollector.Collect"/> 的选项实例。</returns>
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

        /// <summary>
        /// 为单次工作流运行生成报告数据：调用 <see cref="ReportDataCollector.Collect"/> 采集标量/曲线与图表清单，
        /// 将各 <see cref="ChartSection"/> 渲染为 PNG 并填充 <see cref="ChartSection.ImageBase64"/>，可选将 PNG 写入磁盘；
        /// 同时为曲线判定行尝试匹配算法图表作为附图。多工况合并请使用 <see cref="GenerateCombinedAsync"/>。
        /// </summary>
        /// <param name="request">含 <see cref="TestReportRequest.ArchiveRequest"/>、数据总线、输出目录与导出选项的请求。</param>
        /// <param name="cancellationToken">用于取消异步文件写入与循环渲染。</param>
        /// <returns>
        /// 当 <see cref="TestReportRequest.ArchiveRequest"/> 中缺少 <see cref="Astra.Core.Archiving.WorkflowArchiveRequest.RunRecord"/> 时返回 <c>Success == false</c>；
        /// 否则返回 <c>Success == true</c>，并填充 <see cref="ReportOutput.ReportData"/> 与按需填充 <see cref="ReportOutput.ChartImagePaths"/>。
        /// </returns>
        public async Task<ReportOutput> GenerateAsync(TestReportRequest request, CancellationToken cancellationToken)
        {
            var archiveReq = request.ArchiveRequest;
            var runRecord = archiveReq?.RunRecord;
            if (runRecord == null)
            {
                return new ReportOutput { Success = false };
            }

            var reportData = ReportDataCollector.Collect(
                runRecord,
                request.DataBus,
                archiveReq!.NodeContext,
                MergeReportOptions(request),
                request.ReportStationFromConfig,
                request.ReportLineFromConfig);

            var chartPaths = new List<string>();
            await RenderChartsAsync(request, reportData, chartPaths, cancellationToken);
            RenderCurveJudgmentCharts(request, reportData);

            return new ReportOutput
            {
                Success = true,
                ReportData = reportData,
                ChartImagePaths = chartPaths
            };
        }

        /// <summary>
        /// 将多段已物化的 <see cref="TestReportData"/>（通常对应多工况）按分节顺序排序后，
        /// 调用 <see cref="ReportHtmlBuilder"/> 生成合并 HTML 并写入磁盘（PDF 导出已暂时关闭）。
        /// </summary>
        /// <param name="request">合并请求：输出目录、文件名前缀、<see cref="CombinedTestReportRequest.Formats"/> 及各节数据。</param>
        /// <param name="cancellationToken">取消令牌；PDF 在线程池执行，取消将传播至对应任务。</param>
        /// <returns>
        /// 当 <see cref="CombinedTestReportRequest.Sections"/> 为 null 或空时返回失败；
        /// 否则返回成功，并在请求包含 HTML 时设置 <see cref="ReportOutput.HtmlPath"/>（<see cref="ReportOutput.PdfPath"/> 当前不生成）。
        /// </returns>
        public async Task<ReportOutput> GenerateCombinedAsync(CombinedTestReportRequest request, CancellationToken cancellationToken)
        {
            if (request.Sections == null || request.Sections.Count == 0)
            {
                return new ReportOutput { Success = false };
            }

            var formats = request.Formats;
            if (formats == 0)
                formats = ReportExportFormats.Html;

            Directory.CreateDirectory(request.OutputDirectory);

            var orderedSections = request.Sections
                .OrderBy(s => s.SectionSequenceOrder)
                .ThenBy(s => s.StartTime)
                .ToList();

            string? htmlPath = null;
            if ((formats & ReportExportFormats.Html) != 0)
            {
                var html = ReportHtmlBuilder.BuildCombined(orderedSections);
                htmlPath = Path.Combine(request.OutputDirectory, $"{request.FilePrefix}_report.html");
                await File.WriteAllTextAsync(htmlPath, html, new UTF8Encoding(true), cancellationToken);
            }

            string? pdfPath = null;
            // PDF 报告生成已关闭，当前仅输出 HTML。
            // if ((formats & ReportExportFormats.Pdf) != 0)
            // {
            //     pdfPath = Path.Combine(request.OutputDirectory, $"{request.FilePrefix}_report.pdf");
            //     await Task.Run(() => ReportPdfBuilder.BuildCombined(orderedSections, pdfPath), cancellationToken);
            // }

            return new ReportOutput
            {
                Success = true,
                HtmlPath = htmlPath,
                PdfPath = pdfPath
            };
        }

        /// <summary>
        /// 遍历 <paramref name="reportData"/> 中的每个 <see cref="ChartSection"/>，从数据总线取图数据并渲染为 PNG，
        /// 将结果写入节的 <see cref="ChartSection.ImageBase64"/>；若 <see cref="TestReportRequest.ExportChartFiles"/> 为 true，同时将文件路径追加到 <paramref name="chartPaths"/>。
        /// </summary>
        /// <param name="request">单次报告请求（数据总线、输出目录、文件名前缀、是否导出图表文件）。</param>
        /// <param name="reportData">已采集的报告模型，本方法会就地更新其中的图表节。</param>
        /// <param name="chartPaths">导出 PNG 时的完整路径列表输出参数。</param>
        /// <param name="ct">取消令牌。</param>
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
                    _logger.LogWarning(ex, "报告图表渲染失败：{Title}", chart.Title);
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

        /// <summary>
        /// 根据 <see cref="ChartSection.ArtifactKey"/> 从总线读取 <see cref="ChartDisplayPayload"/>，必要时按 <see cref="ChartSection.SubPlotSeriesIndex"/> 切片子图后，
        /// 调用 <see cref="ReportChartRenderer.RenderToPng"/> 得到 PNG 字节；成功时同步回写节的宽高。
        /// </summary>
        /// <param name="bus">测试数据总线。</param>
        /// <param name="chart">图表节（产物键、尺寸、子图索引、标题等）。</param>
        /// <returns>PNG 字节；无法读取载荷或渲染失败时返回 <c>null</c>。</returns>
        private static byte[]? TryRenderChartPayload(ITestDataBus? bus, ChartSection chart)
        {
            if (bus == null || string.IsNullOrEmpty(chart.ArtifactKey))
                return null;

            if (!bus.TryGet<ChartDisplayPayload>(chart.ArtifactKey, out var payload) || payload == null)
                return null;

            var effective = payload;
            if (chart.SubPlotSeriesIndex is { } si
                && payload.LayoutMode == ChartLayoutMode.SubPlots
                && payload.Series is { Count: > 0 }
                && si >= 0 && si < payload.Series.Count)
            {
                effective = ReportChartPayloadSliceHelper.BuildSubPlotSlice(payload, si);
            }

            var png = ReportChartRenderer.RenderToPng(effective, chart.Width, chart.Height, chart.Title, out var rw, out var rh);
            chart.Width = rw;
            chart.Height = rh;
            return png;
        }

        /// <summary>
        /// 预留的 NVH 等非标准载荷渲染入口：当前实现仅在总线中对象类型非预期时提前返回 <c>null</c>，与 <see cref="TryRenderChartPayload"/> 形成回退链。
        /// </summary>
        /// <param name="bus">测试数据总线。</param>
        /// <param name="chart">目标图表节。</param>
        /// <returns>当前始终为 <c>null</c>（占位扩展点）。</returns>
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

        /// <summary>
        /// 为每条 <see cref="CurveJudgmentRow"/> 在数据总线的算法产物中查找可纳入报告的 <see cref="ChartDisplayPayload"/>（优先匹配生产者节点），
        /// 渲染为固定尺寸 PNG 后写入 <see cref="CurveJudgmentRow.ChartImageBase64"/>。失败时静默跳过，不影响主报告数据。
        /// </summary>
        /// <param name="request">报告请求；需要非空的 <see cref="TestReportRequest.DataBus"/>。</param>
        /// <param name="reportData">含 <see cref="TestReportData.CurveJudgments"/> 的报告数据。</param>
        private static void RenderCurveJudgmentCharts(TestReportRequest request, TestReportData reportData)
        {
            if (request.DataBus == null) return;

            foreach (var cj in reportData.CurveJudgments)
            {
                try
                {
                    var algRefs = request.DataBus.Query(DataArtifactCategory.Algorithm);
                    var ordered = OrderAlgorithmRefsForCurveJudgment(algRefs, cj);
                    foreach (var algRef in ordered)
                    {
                        if (!ReportIncludeKeys.PreviewIncludesInReport(algRef.Preview))
                            continue;

                        if (!request.DataBus.TryGet<ChartDisplayPayload>(algRef.Key, out var p) || p == null)
                            continue;

                        var png = ReportChartRenderer.RenderToPng(p, 800, 400, cj.CurveName);
                        if (png is { Length: > 0 })
                        {
                            cj.ChartImageBase64 = Convert.ToBase64String(png);
                            break;
                        }
                    }
                }
                catch
                {
                    // 曲线附图为增强展示，异常不向上抛出
                }
            }
        }

        /// <summary>
        /// 对算法产物引用排序/筛选：若 <see cref="CurveJudgmentRow.NodeId"/> 有值，则优先保留预览中 <c>__ProducerNodeId</c> 与该节点 ID 一致的项；
        /// 若无匹配则回退为原始列表顺序，避免附图完全缺失。
        /// </summary>
        /// <param name="algRefs">数据总线查询得到的算法类 <see cref="DataArtifactReference"/> 列表。</param>
        /// <param name="cj">当前曲线判定行。</param>
        /// <returns>供遍历尝试渲染的引用序列。</returns>
        private static IEnumerable<DataArtifactReference> OrderAlgorithmRefsForCurveJudgment(
            IReadOnlyList<DataArtifactReference> algRefs,
            CurveJudgmentRow cj)
        {
            if (algRefs == null || algRefs.Count == 0)
                return Array.Empty<DataArtifactReference>();

            if (string.IsNullOrEmpty(cj.NodeId))
                return algRefs;

            var matched = algRefs
                .Where(r => string.Equals(GetArtifactProducerNodeId(r), cj.NodeId, StringComparison.Ordinal))
                .ToList();

            return matched.Count > 0 ? matched : algRefs;
        }

        /// <summary>
        /// 从产物 <see cref="DataArtifactReference.Preview"/> 中读取生产者节点标识键 <c>__ProducerNodeId</c> 的字符串形式。
        /// </summary>
        /// <param name="r">数据产物引用。</param>
        /// <returns>生产者节点 ID；缺失时返回空字符串。</returns>
        private static string GetArtifactProducerNodeId(DataArtifactReference r)
        {
            if (r.Preview != null && r.Preview.TryGetValue("__ProducerNodeId", out var v))
                return v?.ToString() ?? string.Empty;
            return string.Empty;
        }
    }
}
