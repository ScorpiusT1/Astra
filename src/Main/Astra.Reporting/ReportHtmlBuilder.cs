using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using Astra.Core.Reporting;

namespace Astra.Reporting
{
    /// <summary>
    /// HTML 报告构建器：将一份或多份 <see cref="TestReportData"/> 生成为 UTF-8 自包含 HTML 文档，
    /// 样式内联于 <c>&lt;style&gt;</c>，图表通过 <c>data:image/png;base64</c> 内嵌，便于邮件转发与离线打开。
    /// </summary>
    public static class ReportHtmlBuilder
    {
        /// <summary>与 <see cref="ChartSection"/> 默认宽高一致，用于曲线附图 &lt;img&gt; 与算法图块对齐。</summary>
        private const int DefaultEmbeddedChartWidth = 800;
        private const int DefaultEmbeddedChartHeight = 400;

        /// <summary>
        /// 生成合并多工况的完整 HTML 字符串：总览封面、各工况分节、单值/曲线表及带 Base64 图片的图库。
        /// </summary>
        /// <param name="sections">按展示顺序排列的各工况数据；为 null 或空时返回空字符串。</param>
        /// <returns>完整 HTML 文档文本。</returns>
        public static string BuildCombined(IReadOnlyList<TestReportData> sections)
        {
            if (sections == null || sections.Count == 0) return string.Empty;

            var sb = new StringBuilder(65_536);
            sb.AppendLine("<!DOCTYPE html><html lang='zh-CN'><head><meta charset='utf-8'/>");

            var sn = sections[0].SN;
            sb.AppendLine($"<title>测试报告 - {E(sn)}</title>");
            AppendStyles(sb);
            sb.AppendLine("</head><body>");

            sb.AppendLine("<h1>测试报告</h1>");

            var minStart = sections.Min(s => s.StartTime);
            var maxEnd = sections.Max(s => s.EndTime);
            var allOk = sections.All(s => s.OverallResult == "OK");

            var station = MergeMetaField(sections, s => s.TestStation);
            var line = MergeMetaField(sections, s => s.TestLine);

            sb.AppendLine("<table class='meta-table' role='presentation'>");
            MetaRow(sb, "SN", sn);
            MetaRow(sb, "测试工位", DisplayMetaValue(station));
            MetaRow(sb, "测试线体", DisplayMetaValue(line));
            MetaRow(sb, "开始时间", minStart.ToString("yyyy-MM-dd HH:mm:ss"));
            MetaRow(sb, "总耗时", $"{(maxEnd - minStart).TotalSeconds:F1}s");
            var badgeClass = allOk ? "badge-ok" : "badge-ng";
            var overallText = allOk ? "OK" : "NG";
            sb.AppendLine($"<tr><th>总体结果</th><td><span class='{badgeClass}'>{overallText}</span></td></tr>");
            sb.AppendLine("</table>");

            AppendConditionOverviewTable(sb, sections);

            foreach (var d in sections)
            {
                sb.AppendLine("<hr style='margin-top:40px;border:none;border-top:2px solid #3b82f6;'/>");
                var sectionTitle = ReportConditionDisplay.FormatSectionTitle(d.Condition);
                sb.AppendLine($"<h2 class='h2-condition'>{E(sectionTitle)}</h2>");

                AppendJudgmentSummary(sb, d);

                if (d.ScalarJudgments.Count > 0)
                    AppendScalarJudgmentTable(sb, d.ScalarJudgments);

                if (d.CurveJudgments.Count > 0)
                    AppendCurveJudgmentTable(sb, d.CurveJudgments);

                if (d.Charts.Any(c => !string.IsNullOrEmpty(c.ImageBase64)))
                    AppendChartGallery(sb, d.Charts);
            }

            sb.AppendLine("<footer style='margin-top:40px;padding-top:12px;border-top:1px solid #e5e7eb;font-size:11px;color:#94a3b8;'>");
            sb.AppendLine($"Astra Test Report &mdash; {maxEnd:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine("</footer></body></html>");
            return sb.ToString();
        }

        /// <summary>
        /// 在多个报告节中选取第一个非空的字符串字段，用于合并封面元数据。
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
        /// 空元数据显示为 “—”，否则返回原值。
        /// </summary>
        private static string DisplayMetaValue(string? value) =>
            string.IsNullOrWhiteSpace(value) ? "—" : value!;

        /// <summary>
        /// 追加「工况概览」表格：序号、工况、开始时间、结果、耗时（与各工况分节内容互补，避免重复）。
        /// </summary>
        private static void AppendConditionOverviewTable(StringBuilder sb, IReadOnlyList<TestReportData> sections)
        {
            sb.AppendLine("<h2>工况概览</h2><table>");
            sb.AppendLine("<tr><th>#</th><th>工况</th><th>开始时间</th><th>结果</th><th>耗时</th></tr>");
            for (var i = 0; i < sections.Count; i++)
            {
                var d = sections[i];
                var cls = d.OverallResult == "OK" ? "pass" : "fail";
                var dur = (d.EndTime - d.StartTime).TotalSeconds;
                var start = d.StartTime.ToString("yyyy-MM-dd HH:mm:ss");
                sb.AppendLine($"<tr><td>{i + 1}</td><td>{E(d.Condition)}</td>");
                sb.AppendLine($"<td>{E(start)}</td>");
                sb.AppendLine($"<td class='{cls}'>{E(d.OverallResult)}</td>");
                sb.AppendLine($"<td>{dur:F1}s</td></tr>");
            }
            sb.AppendLine("</table>");
        }

        /// <summary>
        /// 生成单工况 HTML，等价于仅包含一节数据的 <see cref="BuildCombined"/>。
        /// </summary>
        /// <param name="data">单次工况报告数据。</param>
        /// <returns>完整 HTML 文档。</returns>
        public static string Build(TestReportData data) => BuildCombined([data]);

        /// <summary>
        /// 向 StringBuilder 写入报告所用内联 CSS（打印分页、徽章颜色、图表卡片布局等）。
        /// </summary>
        private static void AppendStyles(StringBuilder sb)
        {
            sb.AppendLine(@"<style>
:root{--ok:#22c55e;--ng:#ef4444;--border:#e5e7eb;--bg-header:#f8fafc;}
*{box-sizing:border-box;}
body{font-family:'Microsoft YaHei','Segoe UI',sans-serif;margin:0 auto;padding:24px 32px;max-width:960px;color:#1e293b;background:#fff;}
h1{font-size:24px;border-bottom:2px solid #3b82f6;padding-bottom:8px;}
h2{font-size:18px;color:#334155;margin-top:32px;border-left:4px solid #3b82f6;padding-left:12px;}
.h2-condition{font-size:24px;font-weight:700;line-height:1.3;margin-top:24px;margin-bottom:10px;color:#0f172a;border-left:5px solid #2563eb;padding-left:14px;}
h3.section{font-size:16px;color:#334155;margin-top:24px;border-left:3px solid #60a5fa;padding-left:10px;}
table{border-collapse:collapse;width:100%;margin:12px 0;}
th,td{border:1px solid var(--border);padding:8px 12px;text-align:left;font-size:13px;}
th{background:var(--bg-header);font-weight:600;}
.pass{color:var(--ok);font-weight:700;} .fail{color:var(--ng);font-weight:700;}
.badge-ok{background:var(--ok);color:#fff;padding:3px 14px;border-radius:4px;font-size:16px;font-weight:700;display:inline-block;}
.badge-ng{background:var(--ng);color:#fff;padding:3px 14px;border-radius:4px;font-size:16px;font-weight:700;display:inline-block;}
.chart-gallery-table{width:100%;border-collapse:collapse;table-layout:fixed;margin:12px 0;}
.chart-gallery-table td.chart-cell{border:1px solid var(--border);vertical-align:top;padding:8px;width:100%;background:#fff;}
.chart-gallery-table td.chart-cell-empty{display:none;}
.chart-card{margin:0;border:1px solid var(--border);border-radius:8px;overflow:hidden;}
.chart-card img{display:block;max-width:100%;height:auto;}
.chart-title{padding:8px 12px;font-size:13px;font-weight:600;background:var(--bg-header);word-break:break-word;}
.chart-desc{padding:4px 12px 8px;font-size:12px;color:#64748b;}
.meta-table{width:100%;border-collapse:collapse;margin:16px 0;}
.meta-table th,.meta-table td{border:1px solid var(--border);padding:8px 12px;font-size:13px;}
.meta-table th{width:22%;background:var(--bg-header);font-weight:600;text-align:right;vertical-align:top;white-space:nowrap;}
.meta-table td{text-align:left;background:#fff;word-break:break-word;}
@media print{body{padding:12px;} .chart-gallery-table tr{break-inside:avoid;} .chart-card{break-inside:avoid;}}
</style>");
        }

        /// <summary>
        /// 单值/曲线通过项统计（总体 OK/NG 已在工况概览中展示，此处不再重复）。
        /// </summary>
        private static void AppendJudgmentSummary(StringBuilder sb, TestReportData d)
        {
            var ts = d.ScalarJudgments.Count;
            var ps = d.ScalarJudgments.Count(j => j.Pass);
            var tc = d.CurveJudgments.Count;
            var pc = d.CurveJudgments.Count(j => j.Pass);
            if (ts == 0 && tc == 0)
                return;

            sb.AppendLine("<h3 class='section'>判定统计</h3>");
            sb.AppendLine($"<p>单值数据：{ps}/{ts} 项 OK &nbsp;|&nbsp; 曲线判定：{pc}/{tc} 项 OK</p>");
        }

        /// <summary>
        /// 追加单值判定数据表（节点、参数、实际值、上下限、结果）。
        /// </summary>
        private static void AppendScalarJudgmentTable(StringBuilder sb, List<ScalarJudgmentRow> rows)
        {
            sb.AppendLine("<h3 class='section'>单值数据</h3><table>");
            sb.AppendLine("<tr><th>节点</th><th>参数</th><th>实际值</th><th>下限</th><th>上限</th><th>结果</th></tr>");
            foreach (var r in rows)
            {
                var cls = r.Pass ? "pass" : "fail";
                var res = r.Pass ? "OK" : "NG";
                sb.AppendLine($"<tr><td>{E(r.NodeName)}</td><td>{E(r.ParameterName)}</td>");
                sb.AppendLine($"<td>{Fmt(r.ActualValue)}</td><td>{Fmt(r.LowerLimit)}</td><td>{Fmt(r.UpperLimit)}</td>");
                sb.AppendLine($"<td class='{cls}'>{res}</td></tr>");
            }
            sb.AppendLine("</table>");
        }

        /// <summary>
        /// 追加曲线判定表，并在存在附图 Base64 时继续输出曲线附图网格。
        /// </summary>
        private static void AppendCurveJudgmentTable(StringBuilder sb, List<CurveJudgmentRow> rows)
        {
            sb.AppendLine("<h3 class='section'>曲线判定</h3><table>");
            sb.AppendLine("<tr><th>节点</th><th>曲线</th><th>结果</th><th>失败详情</th></tr>");
            foreach (var r in rows)
            {
                var cls = r.Pass ? "pass" : "fail";
                var res = r.Pass ? "OK" : "NG";
                sb.AppendLine($"<tr><td>{E(r.NodeName)}</td><td>{E(r.CurveName)}</td>");
                sb.AppendLine($"<td class='{cls}'>{res}</td>");
                sb.AppendLine($"<td>{E(r.FailDetail ?? "-")}</td></tr>");
            }
            sb.AppendLine("</table>");

            AppendCurveJudgmentChartGrid(sb, rows);
        }

        /// <summary>
        /// 为含 <see cref="CurveJudgmentRow.ChartImageBase64"/> 的曲线行输出单列图库表格。
        /// </summary>
        private static void AppendCurveJudgmentChartGrid(StringBuilder sb, List<CurveJudgmentRow> rows)
        {
            var withImg = rows.Where(r => !string.IsNullOrEmpty(r.ChartImageBase64)).ToList();
            if (withImg.Count == 0)
                return;

            sb.AppendLine("<h3 class='section' style='margin-top:20px;'>曲线附图</h3>");
            sb.AppendLine("<table class='chart-gallery-table' role='presentation'><tbody>");
            foreach (var row in withImg)
            {
                sb.AppendLine("<tr>");
                AppendChartCellCurve(sb, row);
                sb.AppendLine("</tr>");
            }

            sb.AppendLine("</tbody></table>");
        }

        /// <summary>
        /// 输出单个曲线判定附图的表格单元：标题与内嵌 Base64 PNG。
        /// </summary>
        private static void AppendChartCellCurve(StringBuilder sb, CurveJudgmentRow row)
        {
            var heading = !string.IsNullOrWhiteSpace(row.ReportHeading)
                ? row.ReportHeading
                : row.CurveName;
            sb.AppendLine("<td class='chart-cell'>");
            sb.AppendLine("<div class='chart-card'>");
            sb.AppendLine($"<div class='chart-title'>{E(heading)}</div>");
            sb.AppendLine(
                $"<img src='data:image/png;base64,{row.ChartImageBase64}' width='{DefaultEmbeddedChartWidth}' height='{DefaultEmbeddedChartHeight}' alt='' />");
            sb.AppendLine("</div></td>");
        }

        /// <summary>
        /// 输出算法与原始数据图表图库：仅包含有 <see cref="ChartSection.ImageBase64"/> 的节。
        /// </summary>
        private static void AppendChartGallery(StringBuilder sb, List<ChartSection> charts)
        {
            var list = charts.Where(c => !string.IsNullOrEmpty(c.ImageBase64)).ToList();
            if (list.Count == 0)
                return;

            sb.AppendLine("<h3 class='section'>算法与数据图表</h3>");
            sb.AppendLine("<table class='chart-gallery-table' role='presentation'><tbody>");
            foreach (var c in list)
            {
                sb.AppendLine("<tr>");
                AppendChartCellSection(sb, c);
                sb.AppendLine("</tr>");
            }

            sb.AppendLine("</tbody></table>");
        }

        /// <summary>
        /// 输出单个 <see cref="ChartSection"/> 的表格单元：多级标题、可选描述与带尺寸的 Base64 图像。
        /// </summary>
        private static void AppendChartCellSection(StringBuilder sb, ChartSection c)
        {
            var heading = !string.IsNullOrWhiteSpace(c.ReportHeading)
                ? c.ReportHeading
                : (c.SourceKind == ReportChartSourceKind.Raw ? "[原始数据] " : "[算法] ") + (c.Title ?? string.Empty);
            sb.AppendLine("<td class='chart-cell'>");
            sb.AppendLine("<div class='chart-card'>");
            sb.AppendLine($"<div class='chart-title'>{E(heading)}</div>");
            sb.AppendLine($"<img src='data:image/png;base64,{c.ImageBase64}' width='{c.Width}' height='{c.Height}' alt='' />");
            if (!string.IsNullOrEmpty(c.Description))
                sb.AppendLine($"<div class='chart-desc'>{E(c.Description)}</div>");
            sb.AppendLine("</div></td>");
        }

        /// <summary>
        /// 向元数据表追加一行（经 HTML 转义的标签与值）。
        /// </summary>
        private static void MetaRow(StringBuilder sb, string label, string value)
        {
            sb.AppendLine($"<tr><th>{E(label)}</th><td>{E(value)}</td></tr>");
        }

        /// <summary>
        /// HTML 属性/文本转义，空引用视为空字符串。
        /// </summary>
        private static string E(string? s) => WebUtility.HtmlEncode(s ?? "");

        /// <summary>
        /// 数值格式化为四位小数，无值时输出 “-”。
        /// </summary>
        private static string Fmt(double? v) => v.HasValue ? v.Value.ToString("F4") : "-";
    }
}
