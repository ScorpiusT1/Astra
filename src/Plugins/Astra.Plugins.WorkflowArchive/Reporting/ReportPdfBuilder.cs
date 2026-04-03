using System;
using System.Collections.Generic;
using System.Linq;
using Astra.Core.Reporting;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Astra.Plugins.WorkflowArchive.Reporting
{
    /// <summary>
    /// 使用 QuestPDF 将 <see cref="TestReportData"/> 渲染为 PDF（与 HTML 共用同一套已物化的 PNG）。
    /// </summary>
    public static class ReportPdfBuilder
    {
        static ReportPdfBuilder()
        {
            QuestPDF.Settings.License = LicenseType.Community;
        }

        /// <summary>每页 PDF 中图表网格为 2 行 × 2 列（与 HTML 报告一致）。</summary>
        private const int ChartsPerPdfGridPage = 4;

        public static void Build(TestReportData data, string outputPath)
        {
            Document.Create(document =>
            {
                document.Page(page => ComposeSummaryPage(page, data));

                var imagePages = new List<(string Title, byte[] Png, string? Description)>();

                foreach (var cj in data.CurveJudgments.Where(c => !string.IsNullOrEmpty(c.ChartImageBase64)))
                {
                    var bytes = TryDecodeBase64(cj.ChartImageBase64);
                    if (bytes == null) continue;
                    imagePages.Add(($"曲线图: {cj.CurveName}", bytes, null));
                }

                foreach (var chart in data.Charts.Where(c => !string.IsNullOrEmpty(c.ImageBase64)))
                {
                    var bytes = TryDecodeBase64(chart.ImageBase64);
                    if (bytes == null) continue;
                    var prefix = chart.SourceKind == ReportChartSourceKind.Raw ? "[原始数据] " : "[算法] ";
                    imagePages.Add(($"{prefix}{chart.Title}", bytes, chart.Description));
                }

                for (var i = 0; i < imagePages.Count; i += ChartsPerPdfGridPage)
                {
                    var chunk = imagePages.Skip(i).Take(ChartsPerPdfGridPage).ToList();
                    document.Page(page => ComposeImageGridPage(page, chunk));
                }
            }).GeneratePdf(outputPath);
        }

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

        private static void ComposeSummaryPage(PageDescriptor page, TestReportData d)
        {
            page.Size(PageSizes.A4);
            page.Margin(36);
            page.DefaultTextStyle(x => x.FontSize(9).FontFamily("Microsoft YaHei"));

            page.Content().Column(column =>
            {
                column.Spacing(10);

                column.Item().Text("测试报告").FontSize(18).Bold();

                column.Item().Table(meta =>
                {
                    meta.ColumnsDefinition(cols =>
                    {
                        cols.RelativeColumn(0.32f);
                        cols.RelativeColumn(1.68f);
                    });

                    foreach (var (label, value) in new (string Label, string Value)[]
                    {
                        ("序列号", d.SN),
                        ("工况", d.Condition),
                        ("开始时间", d.StartTime.ToString("yyyy-MM-dd HH:mm:ss")),
                        ("耗时 (s)", $"{(d.EndTime - d.StartTime).TotalSeconds:F1}"),
                        ("执行策略", d.Strategy),
                        ("执行ID", d.ExecutionId)
                    })
                    {
                        static IContainer MetaCell(IContainer c) =>
                            c.Padding(4).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2);

                        meta.Cell().Element(MetaCell).AlignRight().Text(label).SemiBold();
                        meta.Cell().Element(MetaCell).Text(value);
                    }
                });

                var ok = d.OverallResult == "OK";
                column.Item().PaddingTop(4).Text($"总体结果: {d.OverallResult}")
                    .FontSize(14)
                    .Bold()
                    .FontColor(ok ? Colors.Green.Medium : Colors.Red.Medium);

                var ts = d.ScalarJudgments.Count;
                var ps = d.ScalarJudgments.Count(j => j.Pass);
                var tc = d.CurveJudgments.Count;
                var pc = d.CurveJudgments.Count(j => j.Pass);
                if (ts > 0 || tc > 0)
                {
                    column.Item().Text($"数值判定: {ps}/{ts} 通过  |  曲线判定: {pc}/{tc} 通过");
                }

                if (d.ScalarJudgments.Count > 0)
                {
                    column.Item().PaddingTop(8).Text("数值判定").FontSize(12).Bold();
                    column.Item().Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.RelativeColumn(1.2f);
                            cols.RelativeColumn(1.2f);
                            cols.RelativeColumn();
                            cols.RelativeColumn();
                            cols.RelativeColumn();
                            cols.RelativeColumn(0.8f);
                            cols.RelativeColumn(0.8f);
                        });

                        table.Header(header =>
                        {
                            static IContainer CellStyle(IContainer c) =>
                                c.DefaultTextStyle(x => x.SemiBold()).Padding(4).BorderBottom(1).BorderColor(Colors.Grey.Medium);

                            header.Cell().Element(CellStyle).Text("节点");
                            header.Cell().Element(CellStyle).Text("参数");
                            header.Cell().Element(CellStyle).Text("实际值");
                            header.Cell().Element(CellStyle).Text("下限");
                            header.Cell().Element(CellStyle).Text("上限");
                            header.Cell().Element(CellStyle).Text("单位");
                            header.Cell().Element(CellStyle).Text("判定");
                        });

                        foreach (var r in d.ScalarJudgments)
                        {
                            static IContainer Cell(IContainer c) =>
                                c.Padding(4).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2);

                            table.Cell().Element(Cell).Text(r.NodeName);
                            table.Cell().Element(Cell).Text(r.ParameterName);
                            table.Cell().Element(Cell).Text(FormatDouble(r.ActualValue));
                            table.Cell().Element(Cell).Text(FormatDouble(r.LowerLimit));
                            table.Cell().Element(Cell).Text(FormatDouble(r.UpperLimit));
                            table.Cell().Element(Cell).Text(r.Unit);
                            var passText = r.Pass ? "PASS" : "FAIL";
                            table.Cell().Element(Cell).Text(passText).FontColor(r.Pass ? Colors.Green.Medium : Colors.Red.Medium);
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
                            cols.RelativeColumn();
                            cols.RelativeColumn(2);
                        });

                        table.Header(header =>
                        {
                            static IContainer CellStyle(IContainer c) =>
                                c.DefaultTextStyle(x => x.SemiBold()).Padding(4).BorderBottom(1).BorderColor(Colors.Grey.Medium);

                            header.Cell().Element(CellStyle).Text("节点");
                            header.Cell().Element(CellStyle).Text("曲线");
                            header.Cell().Element(CellStyle).Text("判定");
                            header.Cell().Element(CellStyle).Text("失败详情");
                        });

                        foreach (var r in d.CurveJudgments)
                        {
                            static IContainer Cell(IContainer c) =>
                                c.Padding(4).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2);

                            table.Cell().Element(Cell).Text(r.NodeName);
                            table.Cell().Element(Cell).Text(r.CurveName);
                            table.Cell().Element(Cell).Text(r.Pass ? "PASS" : "FAIL")
                                .FontColor(r.Pass ? Colors.Green.Medium : Colors.Red.Medium);
                            table.Cell().Element(Cell).Text(r.FailDetail ?? "-");
                        }
                    });
                }

                column.Item().PaddingTop(16).AlignRight().Text($"生成时间: {d.EndTime:yyyy-MM-dd HH:mm:ss}").FontSize(8).FontColor(Colors.Grey.Medium);
            });
        }

        private static void ComposeImageGridPage(PageDescriptor page, IReadOnlyList<(string Title, byte[] Png, string? Description)> cells)
        {
            if (cells == null || cells.Count == 0)
                return;

            page.Size(PageSizes.A4);
            page.Margin(28);
            page.DefaultTextStyle(x => x.FontSize(9).FontFamily("Microsoft YaHei"));

            page.Content().Column(outer =>
            {
                outer.Spacing(10);

                outer.Item().Row(top =>
                {
                    top.Spacing(8);
                    top.RelativeItem().Element(c => ComposeChartCell(c, cells, 0));
                    top.RelativeItem().Element(c => ComposeChartCell(c, cells, 1));
                });

                if (cells.Count > 2)
                {
                    outer.Item().Row(bottom =>
                    {
                        bottom.Spacing(8);
                        bottom.RelativeItem().Element(c => ComposeChartCell(c, cells, 2));
                        bottom.RelativeItem().Element(c => ComposeChartCell(c, cells, 3));
                    });
                }
            });
        }

        private static void ComposeChartCell(IContainer container, IReadOnlyList<(string Title, byte[] Png, string? Description)> cells, int index)
        {
            if (index >= cells.Count)
            {
                container.Height(1);
                return;
            }

            var item = cells[index];
            container.Border(1).BorderColor(Colors.Grey.Lighten2).Padding(6).Column(col =>
            {
                col.Spacing(4);
                col.Item().Text(item.Title).FontSize(10).Bold();
                if (!string.IsNullOrWhiteSpace(item.Description))
                    col.Item().Text(item.Description!).FontSize(7).FontColor(Colors.Grey.Darken1);
                col.Item().Image(item.Png).FitArea();
            });
        }

        private static string FormatDouble(double? v) => v.HasValue ? v.Value.ToString("F4") : "-";
    }
}
