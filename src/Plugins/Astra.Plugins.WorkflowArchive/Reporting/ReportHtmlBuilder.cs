using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using Astra.Core.Reporting;

namespace Astra.Plugins.WorkflowArchive.Reporting
{
    /// <summary>
    /// 将 <see cref="TestReportData"/> 渲染为自包含的 HTML 报告（图表以 base64 PNG 内嵌）。
    /// </summary>
    public static class ReportHtmlBuilder
    {
        public static string Build(TestReportData data)
        {
            var sb = new StringBuilder(32_768);
            sb.AppendLine("<!DOCTYPE html><html lang='zh-CN'><head><meta charset='utf-8'/>");
            sb.AppendLine($"<title>测试报告 - {E(data.SN)}</title>");
            AppendStyles(sb);
            sb.AppendLine("</head><body>");

            AppendHeader(sb, data);
            AppendOverallResult(sb, data);

            if (data.ScalarJudgments.Count > 0)
                AppendScalarJudgmentTable(sb, data.ScalarJudgments);

            if (data.CurveJudgments.Count > 0)
                AppendCurveJudgmentTable(sb, data.CurveJudgments);

            if (data.Charts.Any(c => !string.IsNullOrEmpty(c.ImageBase64)))
                AppendChartGallery(sb, data.Charts);

            sb.AppendLine("<footer style='margin-top:40px;padding-top:12px;border-top:1px solid #e5e7eb;font-size:11px;color:#94a3b8;'>");
            sb.AppendLine($"Astra Test Report &mdash; {data.EndTime:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine("</footer></body></html>");
            return sb.ToString();
        }

        private static void AppendStyles(StringBuilder sb)
        {
            sb.AppendLine(@"<style>
:root{--ok:#22c55e;--ng:#ef4444;--border:#e5e7eb;--bg-header:#f8fafc;}
*{box-sizing:border-box;}
body{font-family:'Microsoft YaHei','Segoe UI',sans-serif;margin:0;padding:24px 32px;color:#1e293b;background:#fff;}
h1{font-size:24px;border-bottom:2px solid #3b82f6;padding-bottom:8px;}
h2{font-size:18px;color:#334155;margin-top:32px;border-left:4px solid #3b82f6;padding-left:12px;}
table{border-collapse:collapse;width:100%;margin:12px 0;}
th,td{border:1px solid var(--border);padding:8px 12px;text-align:left;font-size:13px;}
th{background:var(--bg-header);font-weight:600;}
.pass{color:var(--ok);font-weight:700;} .fail{color:var(--ng);font-weight:700;}
.badge-ok{background:var(--ok);color:#fff;padding:4px 16px;border-radius:4px;font-size:20px;font-weight:700;display:inline-block;}
.badge-ng{background:var(--ng);color:#fff;padding:4px 16px;border-radius:4px;font-size:20px;font-weight:700;display:inline-block;}
.chart-gallery-table{width:100%;border-collapse:collapse;table-layout:fixed;margin:12px 0;}
.chart-gallery-table td.chart-cell{border:1px solid var(--border);vertical-align:top;padding:8px;width:50%;background:#fff;}
.chart-gallery-table td.chart-cell-empty{background:transparent;border-style:dashed;}
.chart-card{margin:0;border:1px solid var(--border);border-radius:8px;overflow:hidden;}
.chart-card img{display:block;max-width:100%;height:auto;}
.chart-title{padding:8px 12px;font-size:13px;font-weight:600;background:var(--bg-header);}
.chart-desc{padding:4px 12px 8px;font-size:12px;color:#64748b;}
.meta-table{width:100%;max-width:720px;border-collapse:collapse;margin:16px 0;}
.meta-table th,.meta-table td{border:1px solid var(--border);padding:8px 12px;font-size:13px;}
.meta-table th{width:22%;background:var(--bg-header);font-weight:600;text-align:right;vertical-align:top;white-space:nowrap;}
.meta-table td{text-align:left;background:#fff;word-break:break-word;}
@media print{body{padding:12px;} .chart-gallery-table tr{break-inside:avoid;} .chart-card{break-inside:avoid;}}
</style>");
        }

        private static void AppendHeader(StringBuilder sb, TestReportData d)
        {
            sb.AppendLine("<h1>测试报告</h1>");
            sb.AppendLine("<table class='meta-table' role='presentation'>");
            MetaRow(sb, "产品序列号", d.SN);
            MetaRow(sb, "工况", d.Condition);
            MetaRow(sb, "开始时间", d.StartTime.ToString("yyyy-MM-dd HH:mm:ss"));
            MetaRow(sb, "耗时", $"{(d.EndTime - d.StartTime).TotalSeconds:F1}s");
            MetaRow(sb, "执行策略", d.Strategy);
            MetaRow(sb, "执行ID", d.ExecutionId);
            sb.AppendLine("</table>");
        }

        private static void AppendOverallResult(StringBuilder sb, TestReportData d)
        {
            var css = d.OverallResult == "OK" ? "badge-ok" : "badge-ng";
            sb.AppendLine($"<h2>总体结果</h2><p><span class='{css}'>{E(d.OverallResult)}</span></p>");

            var ts = d.ScalarJudgments.Count;
            var ps = d.ScalarJudgments.Count(j => j.Pass);
            var tc = d.CurveJudgments.Count;
            var pc = d.CurveJudgments.Count(j => j.Pass);
            if (ts > 0 || tc > 0)
                sb.AppendLine($"<p>数值判定：{ps}/{ts} 通过 &nbsp;|&nbsp; 曲线判定：{pc}/{tc} 通过</p>");
        }

        private static void AppendScalarJudgmentTable(StringBuilder sb, List<ScalarJudgmentRow> rows)
        {
            sb.AppendLine("<h2>数值判定</h2><table>");
            sb.AppendLine("<tr><th>节点</th><th>参数</th><th>实际值</th><th>下限</th><th>上限</th><th>单位</th><th>判定</th></tr>");
            foreach (var r in rows)
            {
                var cls = r.Pass ? "pass" : "fail";
                sb.AppendLine($"<tr><td>{E(r.NodeName)}</td><td>{E(r.ParameterName)}</td>");
                sb.AppendLine($"<td>{Fmt(r.ActualValue)}</td><td>{Fmt(r.LowerLimit)}</td><td>{Fmt(r.UpperLimit)}</td>");
                sb.AppendLine($"<td>{E(r.Unit)}</td><td class='{cls}'>{(r.Pass ? "PASS" : "FAIL")}</td></tr>");
            }
            sb.AppendLine("</table>");
        }

        private static void AppendCurveJudgmentTable(StringBuilder sb, List<CurveJudgmentRow> rows)
        {
            sb.AppendLine("<h2>曲线判定</h2><table>");
            sb.AppendLine("<tr><th>节点</th><th>曲线</th><th>判定</th><th>失败详情</th></tr>");
            foreach (var r in rows)
            {
                var cls = r.Pass ? "pass" : "fail";
                sb.AppendLine($"<tr><td>{E(r.NodeName)}</td><td>{E(r.CurveName)}</td>");
                sb.AppendLine($"<td class='{cls}'>{(r.Pass ? "PASS" : "FAIL")}</td>");
                sb.AppendLine($"<td>{E(r.FailDetail ?? "-")}</td></tr>");
            }
            sb.AppendLine("</table>");

            AppendCurveJudgmentChartGrid(sb, rows);
        }

        private static void AppendCurveJudgmentChartGrid(StringBuilder sb, List<CurveJudgmentRow> rows)
        {
            var withImg = rows.Where(r => !string.IsNullOrEmpty(r.ChartImageBase64)).ToList();
            if (withImg.Count == 0)
                return;

            sb.AppendLine("<h3 style='margin-top:20px;font-size:16px;color:#334155;'>曲线附图</h3>");
            sb.AppendLine("<table class='chart-gallery-table' role='presentation'><tbody>");
            for (var i = 0; i < withImg.Count; i += 2)
            {
                sb.AppendLine("<tr>");
                AppendChartCellCurve(sb, withImg[i]);
                AppendChartCellCurve(sb, i + 1 < withImg.Count ? withImg[i + 1] : null);
                sb.AppendLine("</tr>");
            }

            sb.AppendLine("</tbody></table>");
        }

        private static void AppendChartCellCurve(StringBuilder sb, CurveJudgmentRow? row)
        {
            if (row == null)
            {
                sb.AppendLine("<td class='chart-cell chart-cell-empty'></td>");
                return;
            }

            sb.AppendLine("<td class='chart-cell'>");
            sb.AppendLine("<div class='chart-card'>");
            sb.AppendLine($"<div class='chart-title'>{E(row.CurveName)}</div>");
            sb.AppendLine($"<img src='data:image/png;base64,{row.ChartImageBase64}' alt='' />");
            sb.AppendLine("</div></td>");
        }

        private static void AppendChartGallery(StringBuilder sb, List<ChartSection> charts)
        {
            var list = charts.Where(c => !string.IsNullOrEmpty(c.ImageBase64)).ToList();
            if (list.Count == 0)
                return;

            sb.AppendLine("<h2>算法与数据图表</h2>");
            sb.AppendLine("<table class='chart-gallery-table' role='presentation'><tbody>");
            for (var i = 0; i < list.Count; i += 2)
            {
                sb.AppendLine("<tr>");
                AppendChartCellSection(sb, list[i]);
                AppendChartCellSection(sb, i + 1 < list.Count ? list[i + 1] : null);
                sb.AppendLine("</tr>");
            }

            sb.AppendLine("</tbody></table>");
        }

        private static void AppendChartCellSection(StringBuilder sb, ChartSection? c)
        {
            if (c == null)
            {
                sb.AppendLine("<td class='chart-cell chart-cell-empty'></td>");
                return;
            }

            var kindLabel = c.SourceKind == ReportChartSourceKind.Raw ? "[原始数据] " : "[算法] ";
            sb.AppendLine("<td class='chart-cell'>");
            sb.AppendLine("<div class='chart-card'>");
            sb.AppendLine($"<div class='chart-title'>{kindLabel}{E(c.Title)}</div>");
            sb.AppendLine($"<img src='data:image/png;base64,{c.ImageBase64}' width='{c.Width}' height='{c.Height}' alt='' />");
            if (!string.IsNullOrEmpty(c.Description))
                sb.AppendLine($"<div class='chart-desc'>{E(c.Description)}</div>");
            sb.AppendLine("</div></td>");
        }

        private static void MetaRow(StringBuilder sb, string label, string value)
        {
            sb.AppendLine($"<tr><th>{E(label)}</th><td>{E(value)}</td></tr>");
        }

        private static string E(string? s) => WebUtility.HtmlEncode(s ?? "");
        private static string Fmt(double? v) => v.HasValue ? v.Value.ToString("F4") : "-";
    }
}
