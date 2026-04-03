using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Astra.UI.Abstractions.Nodes;
using ScottPlot;

namespace Astra.Plugins.WorkflowArchive.Reporting
{
    /// <summary>
    /// ScottPlot 5 无头渲染器：将 <see cref="ChartDisplayPayload"/> 渲染为 PNG 字节数组。
    /// 可脱离 WPF 窗口运行，用于报告离线生成。
    /// 支持根级单图与 <see cref="ChartDisplayPayload.Series"/> 多系列（单图叠加 / 分子图），与主界面图表语义对齐。
    /// </summary>
    public static class ReportChartRenderer
    {
        /// <summary>多子图（按行）时，单行最小高度（像素）。</summary>
        private const int SubPlotMinRowHeight = 180;

        /// <summary>多子图时，单行最大高度（像素），防止单行过高。</summary>
        private const int SubPlotMaxRowHeight = 900;

        /// <summary>多子图合成图总高度上限（像素），防止 PNG 过大。</summary>
        private const int SubPlotMaxTotalHeight = 12000;

        private static readonly string[] DefaultPalette =
        {
            "#3b82f6", "#ef4444", "#22c55e", "#f59e0b", "#8b5cf6",
            "#ec4899", "#06b6d4", "#84cc16", "#f97316", "#6366f1",
            "#14b8a6", "#e11d48", "#a855f7", "#0ea5e9", "#65a30d"
        };

        /// <summary>渲染 PNG；多子图按行堆叠时，总高度 = 行数 ×（<paramref name="height"/> 夹紧后的行高），不超过 <see cref="SubPlotMaxTotalHeight"/>。</summary>
        public static byte[] RenderToPng(
            ChartDisplayPayload payload,
            int width = 800,
            int height = 400,
            string? title = null)
        {
            return RenderToPng(payload, width, height, title, out _, out _);
        }

        /// <inheritdoc cref="RenderToPng(ChartDisplayPayload,int,int,string?)"/>
        /// <param name="renderedWidth">实际导出宽度（与请求宽度一致）。</param>
        /// <param name="renderedHeight">实际导出高度；多子图时为合成图总高度。</param>
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
                    var bytes = RenderSeriesSubPlotsToPng(payload, width, height, title, out renderedHeight);
                    return bytes;
                }

                return RenderSeriesSinglePlotToPng(payload, width, height, title);
            }

            return RenderSingleRootPayloadToPng(payload, width, height, title);
        }

        /// <summary>
        /// <paramref name="requestedRowHeight"/> 为期望的<strong>每行</strong>高度（通常来自报告 <c>ChartSection.Height</c>）。
        /// </summary>
        private static int ComputeSubPlotTotalHeight(int rowCount, int requestedRowHeight)
        {
            if (rowCount <= 0)
            {
                return Math.Clamp(requestedRowHeight, SubPlotMinRowHeight, SubPlotMaxRowHeight);
            }

            var perRow = Math.Clamp(requestedRowHeight, SubPlotMinRowHeight, SubPlotMaxRowHeight);
            var total = rowCount * perRow;
            if (total <= SubPlotMaxTotalHeight)
            {
                return total;
            }

            // 总高度超上限：均分行高（不低于最小行高，必要时顶到上限）
            var scaledPerRow = SubPlotMaxTotalHeight / rowCount;
            scaledPerRow = Math.Max(SubPlotMinRowHeight, scaledPerRow);
            return Math.Min(SubPlotMaxTotalHeight, rowCount * scaledPerRow);
        }

        private static byte[] RenderSingleRootPayloadToPng(
            ChartDisplayPayload payload,
            int width,
            int height,
            string? title)
        {
            var plt = new Plot();
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

        private static byte[] RenderSeriesSinglePlotToPng(
            ChartDisplayPayload payload,
            int width,
            int height,
            string? title)
        {
            var plt = new Plot();
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

        private static byte[] RenderSeriesSubPlotsToPng(
            ChartDisplayPayload payload,
            int width,
            int height,
            string? title,
            out int totalHeightUsed)
        {
            var series = payload.Series!;
            var n = series.Count;

            totalHeightUsed = ComputeSubPlotTotalHeight(n, height);

            var multiplot = new Multiplot();
            multiplot.AddPlots(n);
            // 按行纵向堆叠（每个子图占一行）；Columns 为横向单行排布
            multiplot.Layout = new ScottPlot.MultiplotLayouts.Rows();

            for (var i = 0; i < n; i++)
            {
                var plt = multiplot.GetPlot(i);
                var entry = series[i];

                if (!string.IsNullOrWhiteSpace(entry.Name))
                    plt.Title(entry.Name.Trim());
                else if (i == 0 && !string.IsNullOrEmpty(title))
                    plt.Title(title);

                ApplyAxisLabels(plt, payload);
                RenderPayloadByKind(plt, entry.Data);

                if (entry.Data.Kind is not ChartPayloadKind.Pie
                    and not ChartPayloadKind.Donut
                    and not ChartPayloadKind.Radar)
                {
                    AddHorizontalLimits(plt, payload);
                }

                plt.Axes.AutoScale();
            }

            return SaveMultiplotPng(multiplot, width, totalHeightUsed);
        }

        private static byte[] SaveMultiplotPng(Multiplot multiplot, int width, int height)
        {
            var path = Path.Combine(Path.GetTempPath(), $"astra_mp_{Guid.NewGuid():N}.png");
            try
            {
                multiplot.SavePng(path, width, height);
                return File.ReadAllBytes(path);
            }
            finally
            {
                try
                {
                    if (File.Exists(path))
                        File.Delete(path);
                }
                catch
                {
                    /* best-effort cleanup */
                }
            }
        }

        private static void ApplyAxisLabels(Plot plt, ChartDisplayPayload payload)
        {
            plt.XLabel(ChartDisplayPayload.FormatAxisTitle(payload.BottomAxisLabel, payload.BottomAxisUnit));
            plt.YLabel(ChartDisplayPayload.FormatAxisTitle(payload.LeftAxisLabel, payload.LeftAxisUnit));
        }

        /// <summary>与主界面 <c>TestItemChartWindow.AddSeriesEntryToPlot</c> 对齐：单坐标系内叠加多系列。</summary>
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

        private static void RenderSignal1D(Plot plt, ChartDisplayPayload payload)
        {
            if (payload.SignalY == null || payload.SignalY.Length == 0) return;
            var period = payload.SamplePeriod > 0 ? payload.SamplePeriod : 1.0;
            var sig = plt.Add.Signal(payload.SignalY, period);
            sig.Color = ScottPlot.Color.FromHex("#2196F3");
            sig.LineWidth = 1.5f;
        }

        private static void RenderXY(Plot plt, ChartDisplayPayload payload, bool line)
        {
            if (payload.X == null || payload.Y == null || payload.X.Length == 0 || payload.X.Length != payload.Y.Length)
                return;
            var scatter = plt.Add.Scatter(payload.X, payload.Y);
            scatter.Color = ScottPlot.Color.FromHex("#2196F3");
            if (line) { scatter.LineWidth = 1.5f; scatter.MarkerSize = 0; }
            else { scatter.LineWidth = 0; scatter.MarkerSize = 5; }
        }

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

        private static ScottPlot.Color ResolveColor(string? hex, int index)
        {
            if (!string.IsNullOrEmpty(hex))
                return ScottPlot.Color.FromHex(hex);
            return ScottPlot.Color.FromHex(DefaultPalette[index % DefaultPalette.Length]);
        }
    }
}
