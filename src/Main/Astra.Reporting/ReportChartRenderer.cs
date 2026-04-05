using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Astra.UI.Abstractions.Nodes;
using ScottPlot;

namespace Astra.Reporting
{
    /// <summary>
    /// 报告用图表渲染器：基于 ScottPlot 5 在无 UI 环境下将 <see cref="ChartDisplayPayload"/> 导出为 PNG。
    /// 支持根级单图、多系列单坐标系叠加，以及 <see cref="ChartLayoutMode.SubPlots"/>（整包传入时默认取首子图；报告流水线可预先切片）。
    /// 字体与系列配色策略与主界面图表保持一致性，减少离线报告与在线预览的差异。
    /// </summary>
    public static class ReportChartRenderer
    {
        /// <summary>
        /// 报告导出使用的字体名称；与主界面一致采用微软雅黑，避免中文标题与轴标签在 PNG 中显示为缺字或方框。
        /// </summary>
        private const string ReportFontName = "微软雅黑";

        /// <summary>
        /// 当系列未指定十六进制颜色时循环使用的默认调色板（与工业报表常用对比色相近）。
        /// </summary>
        private static readonly string[] DefaultPalette =
        {
            "#3b82f6", "#ef4444", "#22c55e", "#f59e0b", "#8b5cf6",
            "#ec4899", "#06b6d4", "#84cc16", "#f97316", "#6366f1",
            "#14b8a6", "#e11d48", "#a855f7", "#0ea5e9", "#65a30d"
        };

        /// <summary>
        /// 将载荷渲染为 PNG 字节数组（便捷重载，不返回实际尺寸 out 参数）。
        /// </summary>
        /// <param name="payload">图表数据载荷。</param>
        /// <param name="width">图像宽度（像素）。</param>
        /// <param name="height">图像高度（像素）。</param>
        /// <param name="title">可选图表总标题。</param>
        /// <returns>PNG 二进制内容。</returns>
        /// <remarks>
        /// 若 <paramref name="payload"/> 为 <see cref="ChartLayoutMode.SubPlots"/> 且含多子系列，本实现仅渲染第一个子图；
        /// 多子图报告应在调用前使用 <see cref="ReportChartPayloadSliceHelper.BuildSubPlotSlice"/> 分别导出。
        /// </remarks>
        public static byte[] RenderToPng(
            ChartDisplayPayload payload,
            int width = 800,
            int height = 400,
            string? title = null)
        {
            return RenderToPng(payload, width, height, title, out _, out _);
        }

        /// <summary>
        /// 将载荷渲染为 PNG，并通过 out 参数回传实际采用的宽高（当前实现与请求宽高一致）。
        /// </summary>
        /// <param name="payload">图表数据载荷。</param>
        /// <param name="width">请求宽度（像素）。</param>
        /// <param name="height">请求高度（像素）。</param>
        /// <param name="title">可选图表总标题。</param>
        /// <param name="renderedWidth">实际导出宽度。</param>
        /// <param name="renderedHeight">实际导出高度。</param>
        /// <returns>PNG 二进制内容。</returns>
        public static byte[] RenderToPng(
            ChartDisplayPayload payload,
            int width,
            int height,
            string? title,
            out int renderedWidth,
            out int renderedHeight)
        {
            renderedWidth = width;
            renderedHeight = height;

            if (payload.Series is { Count: > 0 })
            {
                if (payload.LayoutMode == ChartLayoutMode.SubPlots)
                {
                    var slice = ReportChartPayloadSliceHelper.BuildSubPlotSlice(payload, 0);
                    return RenderSingleRootPayloadToPng(slice, width, height, title);
                }

                return RenderSeriesSinglePlotToPng(payload, width, height, title);
            }

            return RenderSingleRootPayloadToPng(payload, width, height, title);
        }

        /// <summary>
        /// 为 ScottPlot 绘图区设置报告字体。
        /// </summary>
        private static void ApplyReportFont(Plot plt)
        {
            if (!string.IsNullOrWhiteSpace(ReportFontName))
                plt.Font.Set(ReportFontName);
        }

        /// <summary>
        /// 渲染根级单载荷（无 <see cref="ChartDisplayPayload.Series"/> 或已切片为单图）：按 <see cref="ChartDisplayPayload.Kind"/> 绘制并附加水平限值线（饼图/环形图/雷达图除外）。
        /// </summary>
        private static byte[] RenderSingleRootPayloadToPng(
            ChartDisplayPayload payload,
            int width,
            int height,
            string? title)
        {
            var plt = new Plot();
            ApplyReportFont(plt);
            if (!string.IsNullOrEmpty(title))
                plt.Title(title);

            ApplyAxisLabels(plt, payload);
            RenderPayloadByKind(plt, payload);

            if (payload.Kind is not ChartPayloadKind.Pie
                and not ChartPayloadKind.Donut
                and not ChartPayloadKind.Radar)
            {
                AddHorizontalLimits(plt, payload);
            }

            return plt.GetImageBytes(width, height, ImageFormat.Png);
        }

        /// <summary>
        /// 在单一坐标系内叠加绘制 <see cref="ChartDisplayPayload.Series"/> 中的多个 <see cref="ChartSeriesEntry"/>，自动缩放并显示图例。
        /// </summary>
        private static byte[] RenderSeriesSinglePlotToPng(
            ChartDisplayPayload payload,
            int width,
            int height,
            string? title)
        {
            var plt = new Plot();
            ApplyReportFont(plt);
            if (!string.IsNullOrEmpty(title))
                plt.Title(title);

            ApplyAxisLabels(plt, payload);

            var series = payload.Series!;
            for (var i = 0; i < series.Count; i++)
                AddSeriesEntryToPlot(plt, series[i], i);

            var anyCartesian = series.Any(e =>
                e.Data.Kind is not ChartPayloadKind.Pie
                and not ChartPayloadKind.Donut
                and not ChartPayloadKind.Radar);

            if (anyCartesian)
            {
                AddHorizontalLimits(plt, payload);
                plt.Axes.AutoScale();
            }

            if (series.Count > 0)
                plt.ShowLegend(Alignment.UpperRight);

            return plt.GetImageBytes(width, height, ImageFormat.Png);
        }

        /// <summary>
        /// 根据载荷底部/左侧轴标签与单位格式化并设置 X/Y 轴标题。
        /// </summary>
        private static void ApplyAxisLabels(Plot plt, ChartDisplayPayload payload)
        {
            plt.XLabel(ChartDisplayPayload.FormatAxisTitle(payload.BottomAxisLabel, payload.BottomAxisUnit));
            plt.YLabel(ChartDisplayPayload.FormatAxisTitle(payload.LeftAxisLabel, payload.LeftAxisUnit));
        }

        /// <summary>
        /// 将一条 <see cref="ChartSeriesEntry"/> 添加到已有笛卡尔或分类坐标系中，行为与主界面 <c>TestItemChartWindow.AddSeriesEntryToPlot</c> 对齐。
        /// </summary>
        /// <param name="plot">目标 ScottPlot 绘图区。</param>
        /// <param name="entry">包含显示名、颜色与子载荷的系列项。</param>
        /// <param name="index">系列序号，用于默认配色与匿名系列命名。</param>
        private static void AddSeriesEntryToPlot(Plot plot, ChartSeriesEntry entry, int index)
        {
            var data = entry.Data;
            var color = ResolveColor(entry.Color, index);
            var name = string.IsNullOrWhiteSpace(entry.Name) ? $"系列 {index + 1}" : entry.Name.Trim();

            switch (data.Kind)
            {
                case ChartPayloadKind.Signal1D:
                {
                    var y = data.SignalY;
                    if (y == null || y.Length == 0) return;
                    var period = data.SamplePeriod > 0 ? data.SamplePeriod : 1.0;
                    var sig = plot.Add.Signal(y, period);
                    sig.LegendText = name;
                    sig.LineWidth = 1.5f;
                    sig.Color = color;
                    return;
                }
                case ChartPayloadKind.XYLine:
                case ChartPayloadKind.XYScatter:
                {
                    var xs = data.X;
                    var ys = data.Y;
                    if (xs == null || ys == null || xs.Length == 0 || xs.Length != ys.Length) return;
                    var scatter = plot.Add.Scatter(xs, ys);
                    scatter.Color = color;
                    scatter.LegendText = name;
                    if (data.Kind == ChartPayloadKind.XYLine)
                    {
                        scatter.LineWidth = 1.5f;
                        scatter.MarkerSize = 0;
                    }
                    else
                    {
                        scatter.LineWidth = 0;
                        scatter.MarkerSize = 5;
                    }

                    return;
                }
                case ChartPayloadKind.Bar:
                case ChartPayloadKind.HorizontalBar:
                {
                    var items = data.Categories;
                    if (items == null || items.Count == 0) return;
                    var bars = new List<ScottPlot.Bar>();
                    for (var i = 0; i < items.Count; i++)
                    {
                        bars.Add(new ScottPlot.Bar
                        {
                            Position = i,
                            Value = items[i].Value,
                            FillColor = ResolveColor(items[i].Color, i),
                            Label = items[i].Value.ToString("G4")
                        });
                    }

                    var bp = plot.Add.Bars(bars.ToArray());
                    bp.Horizontal = data.Kind == ChartPayloadKind.HorizontalBar;
                    bp.LegendText = name;
                    return;
                }
                default:
                    RenderPayloadByKind(plot, data);
                    return;
            }
        }

        /// <summary>
        /// 按 <see cref="ChartDisplayPayload.Kind"/> 分派到具体的单系列绘制例程。
        /// </summary>
        private static void RenderPayloadByKind(Plot plt, ChartDisplayPayload payload)
        {
            switch (payload.Kind)
            {
                case ChartPayloadKind.Signal1D:
                    RenderSignal1D(plt, payload);
                    break;
                case ChartPayloadKind.XYLine:
                    RenderXY(plt, payload, line: true);
                    break;
                case ChartPayloadKind.XYScatter:
                    RenderXY(plt, payload, line: false);
                    break;
                case ChartPayloadKind.Heatmap:
                    RenderHeatmap(plt, payload);
                    break;
                case ChartPayloadKind.LineSegments:
                    RenderSegments(plt, payload);
                    break;
                case ChartPayloadKind.Bar:
                    RenderBar(plt, payload, horizontal: false);
                    break;
                case ChartPayloadKind.HorizontalBar:
                    RenderBar(plt, payload, horizontal: true);
                    break;
                case ChartPayloadKind.GroupedBar:
                    RenderGroupedBar(plt, payload, stacked: false);
                    break;
                case ChartPayloadKind.StackedBar:
                    RenderGroupedBar(plt, payload, stacked: true);
                    break;
                case ChartPayloadKind.Pie:
                    RenderPie(plt, payload, donut: false);
                    break;
                case ChartPayloadKind.Donut:
                    RenderPie(plt, payload, donut: true);
                    break;
                case ChartPayloadKind.Radar:
                    RenderRadar(plt, payload);
                    break;
            }
        }

        #region Existing chart types

        /// <summary>
        /// 绘制一维采样信号（等间隔），自动根据 <see cref="ChartDisplayPayload.SamplePeriod"/> 推断 X 轴比例。
        /// </summary>
        private static void RenderSignal1D(Plot plt, ChartDisplayPayload payload)
        {
            if (payload.SignalY == null || payload.SignalY.Length == 0) return;
            var period = payload.SamplePeriod > 0 ? payload.SamplePeriod : 1.0;
            var sig = plt.Add.Signal(payload.SignalY, period);
            sig.Color = ScottPlot.Color.FromHex("#2196F3");
            sig.LineWidth = 1.5f;
        }

        /// <summary>
        /// 绘制 XY 散点或折线（由 <paramref name="line"/> 控制线宽与标记）。
        /// </summary>
        private static void RenderXY(Plot plt, ChartDisplayPayload payload, bool line)
        {
            if (payload.X == null || payload.Y == null || payload.X.Length == 0 || payload.X.Length != payload.Y.Length)
                return;
            var scatter = plt.Add.Scatter(payload.X, payload.Y);
            scatter.Color = ScottPlot.Color.FromHex("#2196F3");
            if (line) { scatter.LineWidth = 1.5f; scatter.MarkerSize = 0; }
            else { scatter.LineWidth = 0; scatter.MarkerSize = 5; }
        }

        /// <summary>
        /// 绘制二维热力图；若坐标数组与 Z 矩阵维度匹配则设置数据矩形范围并添加色条。
        /// </summary>
        private static void RenderHeatmap(Plot plt, ChartDisplayPayload payload)
        {
            var z = payload.HeatmapZ;
            if (z == null || z.Length == 0) return;
            var hm = plt.Add.Heatmap(z);
            hm.Colormap = new ScottPlot.Colormaps.Viridis();

            var xC = payload.HeatmapXCoordinates;
            var yC = payload.HeatmapYCoordinates;
            if (xC != null && yC != null && xC.Length == z.GetLength(1) && yC.Length == z.GetLength(0))
            {
                hm.Rectangle = new CoordinateRect(xC.Min(), xC.Max(), yC.Min(), yC.Max());
            }
            plt.Add.ColorBar(hm);
        }

        /// <summary>
        /// 将 <see cref="ChartDisplayPayload.SegmentLines"/> 中每行四个坐标 (x1,y1,x2,y2) 绘制为线段。
        /// </summary>
        private static void RenderSegments(Plot plt, ChartDisplayPayload payload)
        {
            var seg = payload.SegmentLines;
            if (seg == null || seg.GetLength(1) != 4) return;
            for (var i = 0; i < seg.GetLength(0); i++)
            {
                var line = plt.Add.Line(seg[i, 0], seg[i, 1], seg[i, 2], seg[i, 3]);
                line.Color = ScottPlot.Color.FromHex("#2196F3");
                line.LineWidth = 1.5f;
            }
        }

        #endregion

        #region Bar charts

        /// <summary>
        /// 绘制简单柱状图或条形图，并在对应坐标轴上设置分类标签。
        /// </summary>
        /// <param name="horizontal">为 true 时条形水平绘制。</param>
        private static void RenderBar(Plot plt, ChartDisplayPayload payload, bool horizontal)
        {
            var items = payload.Categories;
            if (items == null || items.Count == 0) return;

            var bars = new List<ScottPlot.Bar>();
            for (int i = 0; i < items.Count; i++)
            {
                bars.Add(new ScottPlot.Bar
                {
                    Position = i,
                    Value = items[i].Value,
                    FillColor = ResolveColor(items[i].Color, i),
                    Label = items[i].Value.ToString("G4")
                });
            }

            var barPlot = plt.Add.Bars(bars.ToArray());
            barPlot.Horizontal = horizontal;

            var positions = Enumerable.Range(0, items.Count).Select(i => (double)i).ToArray();
            var labels = items.Select(c => c.Label).ToArray();
            if (horizontal)
                plt.Axes.Left.SetTicks(positions, labels);
            else
                plt.Axes.Bottom.SetTicks(positions, labels);

            plt.Axes.Margins(bottom: 0);
        }

        /// <summary>
        /// 绘制分组柱图或堆叠柱图：多组 <see cref="ChartDisplayPayload.BarGroups"/> 共享同一套 <see cref="ChartDisplayPayload.Categories"/>。
        /// </summary>
        /// <param name="stacked">为 true 时同类别内多系列纵向堆叠。</param>
        private static void RenderGroupedBar(Plot plt, ChartDisplayPayload payload, bool stacked)
        {
            var categories = payload.Categories;
            var groups = payload.BarGroups;
            if (categories == null || groups == null || groups.Count == 0) return;

            var catCount = categories.Count;
            double groupWidth = 0.8;
            double barWidth = groupWidth / groups.Count;

            for (int s = 0; s < groups.Count; s++)
            {
                var series = groups[s];
                var bars = new List<ScottPlot.Bar>();

                for (int c = 0; c < catCount && c < series.Values.Length; c++)
                {
                    double pos = stacked ? c : c - groupWidth / 2 + barWidth * (s + 0.5);
                    double value = series.Values[c];
                    double valueBase = 0;

                    if (stacked && s > 0)
                    {
                        for (int ps = 0; ps < s; ps++)
                            valueBase += groups[ps].Values.Length > c ? groups[ps].Values[c] : 0;
                    }

                    bars.Add(new ScottPlot.Bar
                    {
                        Position = pos,
                        Value = stacked ? valueBase + value : value,
                        ValueBase = stacked ? valueBase : 0,
                        FillColor = ResolveColor(series.Color, s)
                    });
                }

                var bp = plt.Add.Bars(bars.ToArray());
                bp.LegendText = series.SeriesName;
            }

            var positions = Enumerable.Range(0, catCount).Select(i => (double)i).ToArray();
            var catLabels = categories.Select(c => c.Label).ToArray();
            plt.Axes.Bottom.SetTicks(positions, catLabels);
            plt.ShowLegend(Alignment.UpperRight);
            plt.Axes.Margins(bottom: 0);
        }

        #endregion

        #region Pie / Donut

        /// <summary>
        /// 绘制饼图或环形图，支持爆裂比例与环形内径比例。
        /// </summary>
        private static void RenderPie(Plot plt, ChartDisplayPayload payload, bool donut)
        {
            var items = payload.Categories;
            if (items == null || items.Count == 0) return;

            var slices = items.Select((c, i) => new PieSlice
            {
                Value = c.Value,
                FillColor = ResolveColor(c.Color, i),
                Label = c.Label,
                LegendText = $"{c.Label} ({c.Value:G4})"
            }).ToList();

            var pie = plt.Add.Pie(slices);
            pie.ExplodeFraction = payload.ExplodeFraction;

            if (donut)
                pie.DonutFraction = payload.DonutFraction > 0 ? payload.DonutFraction : 0.5;

            plt.Axes.Frameless();
            plt.HideGrid();
            plt.ShowLegend(Alignment.LowerRight);
        }

        #endregion

        #region Radar

        /// <summary>
        /// 绘制雷达图：至少三个类别点；坐标轴最大值可由 <see cref="ChartDisplayPayload.RadarAxisMaxValues"/> 指定，否则按数据自动放大。
        /// </summary>
        private static void RenderRadar(Plot plt, ChartDisplayPayload payload)
        {
            var items = payload.Categories;
            if (items == null || items.Count < 3) return;

            var values = items.Select(c => c.Value).ToArray();
            var maxValues = payload.RadarAxisMaxValues
                           ?? values.Select(v => Math.Max(Math.Ceiling(v * 1.2), 1.0)).ToArray();

            var n = values.Length;
            var angleStep = 2 * Math.PI / n;
            var radius = 100.0;

            for (double level = 0.2; level <= 1.0; level += 0.2)
            {
                var gx = new double[n + 1];
                var gy = new double[n + 1];
                for (int i = 0; i < n; i++)
                {
                    var angle = -Math.PI / 2 + i * angleStep;
                    gx[i] = level * radius * Math.Cos(angle);
                    gy[i] = level * radius * Math.Sin(angle);
                }
                gx[n] = gx[0]; gy[n] = gy[0];
                var grid = plt.Add.Scatter(gx, gy);
                grid.Color = ScottPlot.Color.FromHex("#e2e8f0");
                grid.LineWidth = 0.5f;
                grid.MarkerSize = 0;
            }

            for (int i = 0; i < n; i++)
            {
                var angle = -Math.PI / 2 + i * angleStep;
                var line = plt.Add.Line(0, 0, radius * Math.Cos(angle), radius * Math.Sin(angle));
                line.Color = ScottPlot.Color.FromHex("#cbd5e1");
                line.LineWidth = 0.5f;
                var labelX = radius * 1.18 * Math.Cos(angle);
                var labelY = radius * 1.18 * Math.Sin(angle);
                plt.Add.Text(items[i].Label, labelX, labelY);
            }

            var xs = new double[n + 1];
            var ys = new double[n + 1];
            for (int i = 0; i < n; i++)
            {
                var angle = -Math.PI / 2 + i * angleStep;
                var r = maxValues[i] > 0 ? (values[i] / maxValues[i]) * radius : 0;
                xs[i] = r * Math.Cos(angle);
                ys[i] = r * Math.Sin(angle);
            }
            xs[n] = xs[0]; ys[n] = ys[0];

            var fill = plt.Add.Scatter(xs, ys);
            fill.Color = ScottPlot.Color.FromHex("#3b82f6");
            fill.LineWidth = 2;
            fill.MarkerSize = 5;
            fill.FillY = true;
            fill.FillYColor = ScottPlot.Color.FromHex("#3b82f6").WithAlpha(50);

            plt.Axes.Frameless();
            plt.HideGrid();
        }

        #endregion

        /// <summary>
        /// 在笛卡尔图上叠加水平下限/上限虚线（用于限值可视化）。
        /// </summary>
        private static void AddHorizontalLimits(Plot plt, ChartDisplayPayload payload)
        {
            if (payload.HorizontalLimitLower.HasValue)
            {
                var lo = plt.Add.HorizontalLine(payload.HorizontalLimitLower.Value);
                lo.Color = ScottPlot.Colors.OrangeRed;
                lo.LinePattern = LinePattern.Dashed;
                lo.LineWidth = 1.5f;
            }
            if (payload.HorizontalLimitUpper.HasValue)
            {
                var hi = plt.Add.HorizontalLine(payload.HorizontalLimitUpper.Value);
                hi.Color = ScottPlot.Colors.OrangeRed;
                hi.LinePattern = LinePattern.Dashed;
                hi.LineWidth = 1.5f;
            }
        }

        /// <summary>
        /// 解析十六进制颜色字符串；无效或为空时使用 <see cref="DefaultPalette"/> 中按索引循环的默认色。
        /// </summary>
        private static ScottPlot.Color ResolveColor(string? hex, int index)
        {
            if (!string.IsNullOrEmpty(hex))
                return ScottPlot.Color.FromHex(hex);
            return ScottPlot.Color.FromHex(DefaultPalette[index % DefaultPalette.Length]);
        }
    }
}
