using System;
using System.Collections.Generic;
using System.Linq;
using Astra.Core.Reporting;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Astra.Reporting
{
    /// <summary>
    /// PDF 报告构建器：基于 QuestPDF 将一份或多份 <see cref="TestReportData"/> 排版为 A4 PDF。
    /// 图表依赖各节中已填充的 Base64 PNG（与 HTML 报告使用相同的物化结果）。
    /// </summary>
    public static class ReportPdfBuilder
    {
        private const float GridBorderWidth = 0.5f;
        private static readonly Color GridBorderColor = Colors.Grey.Medium;
        private static readonly Color TableHeaderBackground = Colors.Grey.Lighten3;
        private static readonly Color TableZebraBackground = Colors.Grey.Lighten4;
        private static readonly Color TableRowBackground = Colors.White;

        private static IContainer GridTableHeaderCell(IContainer container) =>
            container
                .Background(TableHeaderBackground)
                .Border(GridBorderWidth)
                .BorderColor(GridBorderColor)
                .Padding(4)
                .DefaultTextStyle(x => x.SemiBold());

        /// <param name="dataRowIndex">数据行从零开始的序号（用于斑马纹）。</param>
        private static IContainer GridTableBodyCell(IContainer container, int dataRowIndex) =>
            container
                .Background(dataRowIndex % 2 == 1 ? TableZebraBackground : TableRowBackground)
                .Border(GridBorderWidth)
                .BorderColor(GridBorderColor)
                .Padding(4);

        /// <summary>
        /// 静态构造：将 QuestPDF 许可证设为社区版，满足库的使用条款。
        /// </summary>
        static ReportPdfBuilder()
        {
            QuestPDF.Settings.License = LicenseType.Community;
        }

        /// <summary>
        /// 图表页中纵向堆叠的图块数量；A4 上默认每页两张以控制页数。
        /// </summary>
        private const int ChartsPerPdfStackPage = 2;

        /// <summary>
        /// 根据有序的多工况报告数据生成合并 PDF：封面总览、各工况摘要页、曲线附图与算法/原始数据图表按固定槽高分页堆叠。
        /// </summary>
        /// <param name="sections">按展示顺序排列的各工况 <see cref="TestReportData"/>；不得为 null 或空集合。</param>
        /// <param name="outputPath">输出 PDF 的完整文件路径；父目录应已存在或由调用方创建。</param>
        public static void BuildCombined(IReadOnlyList<TestReportData> sections, string outputPath)
        {
            if (sections == null || sections.Count == 0) return;

            Document.Create(document =>
            {
                document.Page(page => ComposeCombinedCoverPage(page, sections));

                foreach (var data in sections)
                {
                    document.Page(page => ComposeSummaryPage(page, data));

                    var imagePages = new List<(string Title, byte[] Png, string? Description)>();

                    foreach (var cj in data.CurveJudgments.Where(c => !string.IsNullOrEmpty(c.ChartImageBase64)))
                    {
                        var bytes = TryDecodeBase64(cj.ChartImageBase64);
                        if (bytes == null) continue;
                        var title = !string.IsNullOrWhiteSpace(cj.ReportHeading)
                            ? cj.ReportHeading
                            : $"曲线图: {cj.CurveName}";
                        imagePages.Add((title, bytes, null));
                    }

                    foreach (var chart in data.Charts.Where(c => !string.IsNullOrEmpty(c.ImageBase64)))
                    {
                        var bytes = TryDecodeBase64(chart.ImageBase64);
                        if (bytes == null) continue;
                        var title = !string.IsNullOrWhiteSpace(chart.ReportHeading)
                            ? chart.ReportHeading
                            : (chart.SourceKind == ReportChartSourceKind.Raw ? "[原始数据] " : "[算法] ") + chart.Title;
                        imagePages.Add((title, bytes, chart.Description));
                    }

                    var totalCharts = imagePages.Count;
                    for (var i = 0; i < imagePages.Count; i += ChartsPerPdfStackPage)
                    {
                        var chunk = imagePages.Skip(i).Take(ChartsPerPdfStackPage).ToList();
                        var chartFrom = i + 1;
                        var chartTo = Math.Min(i + chunk.Count, totalCharts);
                        document.Page(page => ComposeImageStackPage(page, chunk, data.SN, data.Condition, chartFrom, chartTo, totalCharts));
                    }
                }
            }).GeneratePdf(outputPath);
        }

        /// <summary>
        /// 在多个报告节中选取第一个非空的元数据字段（用于合并封面上的工位、线体等）。
        /// </summary>
        private static string MergeMetaField(IReadOnlyList<TestReportData> sections, Func<TestReportData, string> pick)
        {
            foreach (var s in sections)
            {
                var v = pick(s)?.Trim();
                if (!string.IsNullOrEmpty(v)) return v;
            }

            return string.Empty;
        }

        /// <summary>
        /// 将空或空白元数据显示为长横线占位符 “—”。
        /// </summary>
        private static string DisplayMeta(string? value) =>
            string.IsNullOrWhiteSpace(value) ? "—" : value!;

        /// <summary>
        /// 为页面添加居中的页脚：当前页码 / 总页数。
        /// </summary>
        private static void ApplyStandardFooter(PageDescriptor page)
        {
            page.Footer()
                .AlignCenter()
                .PaddingTop(6)
                .DefaultTextStyle(x => x.FontSize(8).FontColor(Colors.Grey.Medium))
                .Text(t =>
                {
                    t.Span("— ");
                    t.CurrentPageNumber();
                    t.Span(" / ");
                    t.TotalPages();
                    t.Span(" —");
                });
        }

        /// <summary>
        /// 排版合并报告的封面页：总 SN、时间跨度、总体结果、工位/线体及工况概览表。
        /// </summary>
        private static void ComposeCombinedCoverPage(PageDescriptor page, IReadOnlyList<TestReportData> sections)
        {
            page.Size(PageSizes.A4);
            page.Margin(36);
            page.DefaultTextStyle(x => x.FontSize(9).FontFamily("Microsoft YaHei"));
            ApplyStandardFooter(page);

            page.Content().Column(column =>
            {
                column.Spacing(10);
                column.Item().Text("测试报告").FontSize(22).Bold();

                var sn = sections[0].SN;
                var minStart = sections.Min(s => s.StartTime);
                var maxEnd = sections.Max(s => s.EndTime);
                var allOk = sections.All(s => s.OverallResult == "OK");
                var station = MergeMetaField(sections, s => s.TestStation);
                var line = MergeMetaField(sections, s => s.TestLine);

                column.Item().Table(meta =>
                {
                    meta.ColumnsDefinition(cols =>
                    {
                        cols.RelativeColumn(0.32f);
                        cols.RelativeColumn(1.68f);
                    });

                    var metaRow = 0;
                    foreach (var (label, value) in new (string, string)[]
                    {
                        ("SN", sn),
                        ("测试工位", DisplayMeta(station)),
                        ("测试线体", DisplayMeta(line)),
                        ("开始时间", minStart.ToString("yyyy-MM-dd HH:mm:ss")),
                        ("总耗时 (s)", $"{(maxEnd - minStart).TotalSeconds:F1}"),
                        ("总体结果", allOk ? "OK" : "NG")
                    })
                    {
                        meta.Cell().Element(c => GridTableBodyCell(c, metaRow)).AlignRight().Text(label).SemiBold();
                        var okRow = label == "总体结果" && allOk;
                        var ngRow = label == "总体结果" && !allOk;
                        if (okRow)
                            meta.Cell().Element(c => GridTableBodyCell(c, metaRow)).Text(value).Bold().FontColor(Colors.Green.Medium);
                        else if (ngRow)
                            meta.Cell().Element(c => GridTableBodyCell(c, metaRow)).Text(value).Bold().FontColor(Colors.Red.Medium);
                        else
                            meta.Cell().Element(c => GridTableBodyCell(c, metaRow)).Text(value);
                        metaRow++;
                    }
                });

                column.Item().PaddingTop(8).Text("工况概览").FontSize(12).Bold();
                column.Item().Table(table =>
                {
                    table.ColumnsDefinition(cols =>
                    {
                        cols.ConstantColumn(28);
                        cols.RelativeColumn(1.15f);
                        cols.RelativeColumn(1.1f);
                        cols.RelativeColumn(0.45f);
                        cols.RelativeColumn(0.45f);
                    });

                    table.Header(header =>
                    {
                        header.Cell().Element(GridTableHeaderCell).Text("#");
                        header.Cell().Element(GridTableHeaderCell).Text("工况");
                        header.Cell().Element(GridTableHeaderCell).Text("开始时间");
                        header.Cell().Element(GridTableHeaderCell).Text("结果");
                        header.Cell().Element(GridTableHeaderCell).Text("耗时");
                    });

                    for (var i = 0; i < sections.Count; i++)
                    {
                        var d = sections[i];
                        var ok = d.OverallResult == "OK";

                        table.Cell().Element(c => GridTableBodyCell(c, i)).Text((i + 1).ToString());
                        table.Cell().Element(c => GridTableBodyCell(c, i)).Text(d.Condition);
                        table.Cell().Element(c => GridTableBodyCell(c, i)).Text(d.StartTime.ToString("yyyy-MM-dd HH:mm:ss"));
                        table.Cell().Element(c => GridTableBodyCell(c, i)).Text(d.OverallResult)
                            .FontColor(ok ? Colors.Green.Medium : Colors.Red.Medium);
                        table.Cell().Element(c => GridTableBodyCell(c, i)).Text($"{(d.EndTime - d.StartTime).TotalSeconds:F1}s");
                    }
                });

                column.Item().PaddingTop(16).AlignRight()
                    .Text($"生成时间: {maxEnd:yyyy-MM-dd HH:mm:ss}").FontSize(8).FontColor(Colors.Grey.Medium);
            });
        }

        /// <summary>
        /// 生成单工况 PDF 的便捷封装，内部等价于仅含一节数据的 <see cref="BuildCombined"/>。
        /// </summary>
        /// <param name="data">单次工况的报告数据。</param>
        /// <param name="outputPath">输出 PDF 路径。</param>
        public static void Build(TestReportData data, string outputPath)
            => BuildCombined([data], outputPath);

        /// <summary>
        /// 将 Base64 字符串解码为字节数组；格式非法时返回 null。
        /// </summary>
        private static byte[]? TryDecodeBase64(string? base64)
        {
            if (string.IsNullOrEmpty(base64)) return null;
            try
            {
                return Convert.FromBase64String(base64);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 排版某一工况的摘要页：分节标题（流程/工况名）、判定统计（与 HTML 一致；开始时间/耗时/总体结果见封面工况概览）。
        /// </summary>
        private static void ComposeSummaryPage(PageDescriptor page, TestReportData d)
        {
            page.Size(PageSizes.A4);
            page.Margin(36);
            page.DefaultTextStyle(x => x.FontSize(9).FontFamily("Microsoft YaHei"));
            ApplyStandardFooter(page);

            page.Content().Column(column =>
            {
                column.Spacing(10);

                column.Item().Text("测试报告").FontSize(16).Bold();
                var sectionTitle = ReportConditionDisplay.FormatSectionTitle(d.Condition);
                column.Item().PaddingTop(2).Text(sectionTitle).FontSize(22).Bold();

                var ts = d.ScalarJudgments.Count;
                var ps = d.ScalarJudgments.Count(j => j.Pass);
                var tc = d.CurveJudgments.Count;
                var pc = d.CurveJudgments.Count(j => j.Pass);
                if (ts > 0 || tc > 0)
                {
                    column.Item().PaddingTop(4).Text("判定统计").FontSize(12).Bold();
                    column.Item().Text($"单值数据: {ps}/{ts} 项 OK  |  曲线判定: {pc}/{tc} 项 OK");
                }

                if (d.ScalarJudgments.Count > 0)
                {
                    column.Item().PaddingTop(8).Text("单值数据").FontSize(12).Bold();
                    column.Item().Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.RelativeColumn(1.2f);
                            cols.RelativeColumn(1.2f);
                            cols.RelativeColumn();
                            cols.RelativeColumn();
                            cols.RelativeColumn();
                            cols.RelativeColumn(0.7f);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Element(GridTableHeaderCell).Text("节点");
                            header.Cell().Element(GridTableHeaderCell).Text("参数");
                            header.Cell().Element(GridTableHeaderCell).Text("实际值");
                            header.Cell().Element(GridTableHeaderCell).Text("下限");
                            header.Cell().Element(GridTableHeaderCell).Text("上限");
                            header.Cell().Element(GridTableHeaderCell).Text("结果");
                        });

                        var sRow = 0;
                        foreach (var r in d.ScalarJudgments)
                        {
                            table.Cell().Element(c => GridTableBodyCell(c, sRow)).Text(r.NodeName);
                            table.Cell().Element(c => GridTableBodyCell(c, sRow)).Text(r.ParameterName);
                            table.Cell().Element(c => GridTableBodyCell(c, sRow)).Text(FormatDouble(r.ActualValue));
                            table.Cell().Element(c => GridTableBodyCell(c, sRow)).Text(FormatDouble(r.LowerLimit));
                            table.Cell().Element(c => GridTableBodyCell(c, sRow)).Text(FormatDouble(r.UpperLimit));
                            var resText = r.Pass ? "OK" : "NG";
                            table.Cell().Element(c => GridTableBodyCell(c, sRow)).Text(resText).FontColor(r.Pass ? Colors.Green.Medium : Colors.Red.Medium);
                            sRow++;
                        }
                    });
                }

                if (d.CurveJudgments.Count > 0)
                {
                    column.Item().PaddingTop(8).Text("曲线判定").FontSize(12).Bold();
                    column.Item().Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.RelativeColumn();
                            cols.RelativeColumn();
                            cols.RelativeColumn(0.7f);
                            cols.RelativeColumn(2);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Element(GridTableHeaderCell).Text("节点");
                            header.Cell().Element(GridTableHeaderCell).Text("曲线");
                            header.Cell().Element(GridTableHeaderCell).Text("结果");
                            header.Cell().Element(GridTableHeaderCell).Text("失败详情");
                        });

                        var cRow = 0;
                        foreach (var r in d.CurveJudgments)
                        {
                            table.Cell().Element(c => GridTableBodyCell(c, cRow)).Text(r.NodeName);
                            table.Cell().Element(c => GridTableBodyCell(c, cRow)).Text(r.CurveName);
                            var resText = r.Pass ? "OK" : "NG";
                            table.Cell().Element(c => GridTableBodyCell(c, cRow)).Text(resText)
                                .FontColor(r.Pass ? Colors.Green.Medium : Colors.Red.Medium);
                            table.Cell().Element(c => GridTableBodyCell(c, cRow)).Text(r.FailDetail ?? "-");
                            cRow++;
                        }
                    });
                }

                column.Item().PaddingTop(16).AlignRight().Text($"生成时间: {d.EndTime:yyyy-MM-dd HH:mm:ss}").FontSize(8).FontColor(Colors.Grey.Medium);
            });
        }

        /// <summary>
        /// 将多张图表 PNG 垂直排列在同一 PDF 页上，使用固定槽高与底对齐缩放，避免宽图在 A4 上产生大块留白。
        /// </summary>
        /// <param name="page">QuestPDF 页描述符。</param>
        /// <param name="cells">本页包含的标题、PNG 字节与可选说明文案。</param>
        /// <param name="sn">页眉展示的序列号。</param>
        /// <param name="condition">当前节的工况/流程名称（页眉展示）。</param>
        /// <param name="chartIndexFromOneBased">本页第一张图在全书中的序号（从 1 起）。</param>
        /// <param name="chartIndexToOneBased">本页最后一张图在全书中的序号。</param>
        /// <param name="imageTotal">全部图表总数，用于 “图 x/y” 说明。</param>
        private static void ComposeImageStackPage(
            PageDescriptor page,
            IReadOnlyList<(string Title, byte[] Png, string? Description)> cells,
            string sn,
            string condition,
            int chartIndexFromOneBased,
            int chartIndexToOneBased,
            int imageTotal)
        {
            if (cells == null || cells.Count == 0)
                return;

            page.Size(PageSizes.A4);
            page.Margin(24);
            page.DefaultTextStyle(x => x.FontSize(9).FontFamily("Microsoft YaHei"));
            ApplyStandardFooter(page);

            var total = Math.Max(imageTotal, 1);
            var figCaption = chartIndexFromOneBased == chartIndexToOneBased
                ? $"图 {chartIndexFromOneBased}/{total}"
                : $"图 {chartIndexFromOneBased}–{chartIndexToOneBased}/{total}";

            page.Content().Column(outer =>
            {
                outer.Spacing(6);
                var conditionLine = ReportConditionDisplay.FormatSectionTitle(condition);
                outer.Item().Text($"SN: {sn}  |  {conditionLine}  |  {figCaption}")
                    .FontSize(8).FontColor(Colors.Grey.Darken2);

                // 固定槽高使两图在 A4 内堆叠；图用 FitWidth + 底对齐，避免 FitArea 纵向居中在宽图下产生大块底部留白（HTML 为自然流式无此问题）
                const float chartSlotHeight = 300f;
                foreach (var cell in cells)
                    outer.Item().Height(chartSlotHeight).Element(c => ComposeChartBlock(c, cell, compact: true));
            });
        }

        /// <summary>
        /// 在容器内绘制单张图表卡片：边框、标题、可选描述与按宽度适配的图像。
        /// </summary>
        /// <param name="container">父级布局容器。</param>
        /// <param name="item">图表标题、PNG 数据与描述。</param>
        /// <param name="compact">为 true 时使用更紧凑的字体与内边距（用于多图堆叠页）。</param>
        private static void ComposeChartBlock(
            IContainer container,
            (string Title, byte[] Png, string? Description) item,
            bool compact = false)
        {
            var titleSize = compact ? 9f : 10f;
            var descSize = compact ? 6.5f : 7f;
            var pad = compact ? 6f : 8f;
            container.Border(1).BorderColor(Colors.Grey.Lighten2).Padding(pad).Column(col =>
            {
                col.Spacing(compact ? 4f : 6f);
                col.Item().Text(item.Title).FontSize(titleSize).Bold();
                if (!string.IsNullOrWhiteSpace(item.Description))
                    col.Item().Text(item.Description!).FontSize(descSize).FontColor(Colors.Grey.Darken1);
                col.Item().Extend().AlignBottom().Image(item.Png).FitWidth();
            });
        }

        /// <summary>
        /// 将可空双精度格式化为四位小数字符串；无值时返回 “-”。
        /// </summary>
        private static string FormatDouble(double? v) => v.HasValue ? v.Value.ToString("F4") : "-";
    }
}
