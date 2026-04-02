using System;
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

        public static void Build(TestReportData data, string outputPath)
        {
            Document.Create(document =>
            {
                document.Page(page => ComposeSummaryPage(page, data));

                foreach (var cj in data.CurveJudgments.Where(c => !string.IsNullOrEmpty(c.ChartImageBase64)))
                {
                    var bytes = TryDecodeBase64(cj.ChartImageBase64);
                    if (bytes == null) continue;
                    var title = $"曲线图: {cj.CurveName}";
                    document.Page(page => ComposeImagePage(page, title, bytes));
                }

                foreach (var chart in data.Charts.Where(c => !string.IsNullOrEmpty(c.ImageBase64)))
                {
                    var bytes = TryDecodeBase64(chart.ImageBase64);
                    if (bytes == null) continue;
                    var prefix = chart.SourceKind == ReportChartSourceKind.Raw ? "[原始数据] " : "[算法] ";
                    var title = $"{prefix}{chart.Title}";
                    document.Page(page => ComposeImagePage(page, title, bytes, chart.Description));
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

                column.Item().Text(text =>
                {
                    text.Span("序列号: ").SemiBold();
                    text.Span(d.SN);
                    text.Line("");
                    text.Span("工况: ").SemiBold();
                    text.Span(d.Condition);
                    text.Line("");
                    text.Span("开始: ").SemiBold();
                    text.Span(d.StartTime.ToString("yyyy-MM-dd HH:mm:ss"));
                    text.Span("   耗时: ").SemiBold();
                    text.Span($"{(d.EndTime - d.StartTime).TotalSeconds:F1}s");
                    text.Line("");
                    text.Span("策略: ").SemiBold();
                    text.Span(d.Strategy);
                    text.Span("   执行ID: ").SemiBold();
                    text.Span(d.ExecutionId);
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

        private static void ComposeImagePage(PageDescriptor page, string title, byte[] png, string? description = null)
        {
            page.Size(PageSizes.A4);
            page.Margin(36);
            page.DefaultTextStyle(x => x.FontSize(9).FontFamily("Microsoft YaHei"));

            page.Content().Column(column =>
            {
                column.Spacing(8);
                column.Item().Text(title).FontSize(12).Bold();
                if (!string.IsNullOrWhiteSpace(description))
                    column.Item().Text(description).FontSize(8).FontColor(Colors.Grey.Darken1);
                column.Item().Image(png).FitArea();
            });
        }

        private static string FormatDouble(double? v) => v.HasValue ? v.Value.ToString("F4") : "-";
    }
}
