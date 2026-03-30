using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Astra.UI.Abstractions.Nodes;
using Astra.Services.Home;
using Astra.ViewModels.HomeModules;
using Astra.UI.Helpers;
using Microsoft.Extensions.DependencyInjection;
using ScottPlot;

namespace Astra.Views.HomeModules
{
    public partial class TestItemChartWindow : Window
    {
        public TestItemChartWindow()
        {
            InitializeComponent();

            ItemPlot.Plot.Axes.Bottom.Label.Text = string.Empty;
            ItemPlot.Plot.Axes.Left.Label.Text = string.Empty;
            ApplyItemPlotStyleToAllPlots();

            Loaded += OnLoaded;
            DataContextChanged += OnDataContextChanged;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            RenderChart();
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            RenderChart();
        }

        private void RenderChart()
        {
            if (DataContext is not TestTreeNodeItem item)
            {
                return;
            }

            var plot = ItemPlot.Plot;
            plot.Clear();

            var cache = App.ServiceProvider?.GetService<IChartDisplayDataCache>();
            if (cache == null || !cache.TryGetPayload(item.NodeId, out var payload) || payload == null)
            {
                ItemPlot.Refresh();
                return;
            }

            // 底部 = 横轴标签 + 横轴单位；左侧 = 纵轴标签 + 纵轴单位（与 ChartDisplayPayload 字段一一对应）。
            var bottomAxisTitle = ChartDisplayPayload.FormatAxisTitle(payload.BottomAxisLabel, payload.BottomAxisUnit);
            var leftAxisTitle = ChartDisplayPayload.FormatAxisTitle(payload.LeftAxisLabel, payload.LeftAxisUnit);
            if (string.IsNullOrWhiteSpace(bottomAxisTitle))
            {
                bottomAxisTitle = "X";
            }

            if (string.IsNullOrWhiteSpace(leftAxisTitle))
            {
                leftAxisTitle = "Y";
            }

            plot.Axes.Bottom.Label.Text = bottomAxisTitle;
            plot.Axes.Left.Label.Text = leftAxisTitle;

            var isPieOrRadar = payload.Kind is ChartPayloadKind.Pie
                or ChartPayloadKind.Donut or ChartPayloadKind.Radar;

            switch (payload.Kind)
            {
                case ChartPayloadKind.Signal1D:
                    RenderSignal1D(plot, payload);
                    break;
                case ChartPayloadKind.XYLine:
                    RenderXY(plot, payload, line: true);
                    break;
                case ChartPayloadKind.XYScatter:
                    RenderXY(plot, payload, line: false);
                    break;
                case ChartPayloadKind.Heatmap:
                    RenderHeatmap(plot, payload);
                    break;
                case ChartPayloadKind.LineSegments:
                    RenderSegments(plot, payload);
                    break;
                case ChartPayloadKind.Bar:
                    RenderBar(plot, payload, horizontal: false);
                    break;
                case ChartPayloadKind.HorizontalBar:
                    RenderBar(plot, payload, horizontal: true);
                    break;
                case ChartPayloadKind.GroupedBar:
                    RenderGroupedBar(plot, payload, stacked: false);
                    break;
                case ChartPayloadKind.StackedBar:
                    RenderGroupedBar(plot, payload, stacked: true);
                    break;
                case ChartPayloadKind.Pie:
                    RenderPie(plot, payload, donut: false);
                    break;
                case ChartPayloadKind.Donut:
                    RenderPie(plot, payload, donut: true);
                    break;
                case ChartPayloadKind.Radar:
                    RenderRadar(plot, payload);
                    break;
                default:
                    ItemPlot.Refresh();
                    return;
            }

            if (!isPieOrRadar)
                AddHorizontalLimits(plot, payload, item);

            plot.Axes.AutoScale();
            ItemPlot.Refresh();
        }

        private static void RenderSignal1D(Plot plot, ChartDisplayPayload payload)
        {
            var y = payload.SignalY;
            if (y == null || y.Length == 0)
            {
                return;
            }

            var period = payload.SamplePeriod > 0 ? payload.SamplePeriod : 1.0;
            var sig = plot.Add.Signal(y, period);
            sig.Color = ScottPlot.Color.FromHex("#2196F3");
            sig.LineWidth = 1.5f;
        }

        private static void RenderXY(Plot plot, ChartDisplayPayload payload, bool line)
        {
            var xs = payload.X;
            var ys = payload.Y;
            if (xs == null || ys == null || xs.Length == 0 || xs.Length != ys.Length)
            {
                return;
            }

            var scatter = plot.Add.Scatter(xs, ys);
            scatter.Color = ScottPlot.Color.FromHex("#2196F3");
            if (line)
            {
                scatter.LineWidth = 1.5f;
                scatter.MarkerSize = 0;
            }
            else
            {
                scatter.LineWidth = 0;
                scatter.MarkerSize = 6;
            }
        }

        private static void RenderHeatmap(Plot plot, ChartDisplayPayload payload)
        {
            var z = payload.HeatmapZ;
            if (z == null || z.Length == 0)
            {
                return;
            }

            var rows = z.GetLength(0);
            var cols = z.GetLength(1);
            if (rows < 1 || cols < 1)
            {
                return;
            }

            var hm = plot.Add.Heatmap(z);
            hm.Colormap = new ScottPlot.Colormaps.Viridis();

            var xCoords = payload.HeatmapXCoordinates;
            var yCoords = payload.HeatmapYCoordinates;
            if (xCoords != null && yCoords != null &&
                xCoords.Length == cols && yCoords.Length == rows)
            {
                var xMin = xCoords.Min();
                var xMax = xCoords.Max();
                var yMin = yCoords.Min();
                var yMax = yCoords.Max();
                hm.Rectangle = new CoordinateRect(xMin, xMax, yMin, yMax);
            }

            plot.Add.ColorBar(hm);
        }

        private static void RenderSegments(Plot plot, ChartDisplayPayload payload)
        {
            var seg = payload.SegmentLines;
            if (seg == null || seg.GetLength(1) != 4)
            {
                return;
            }

            var n = seg.GetLength(0);
            for (var i = 0; i < n; i++)
            {
                var x0 = seg[i, 0];
                var y0 = seg[i, 1];
                var x1 = seg[i, 2];
                var y1 = seg[i, 3];
                var line = plot.Add.Line(x0, y0, x1, y1);
                line.Color = ScottPlot.Color.FromHex("#2196F3");
                line.LineWidth = 1.5f;
            }
        }

        private static readonly string[] ChartPalette =
        {
            "#3b82f6", "#ef4444", "#22c55e", "#f59e0b", "#8b5cf6",
            "#ec4899", "#06b6d4", "#84cc16", "#f97316", "#6366f1",
            "#14b8a6", "#e11d48", "#a855f7", "#0ea5e9", "#65a30d"
        };

        private static ScottPlot.Color PaletteColor(string? hex, int i)
        {
            return !string.IsNullOrEmpty(hex)
                ? ScottPlot.Color.FromHex(hex)
                : ScottPlot.Color.FromHex(ChartPalette[i % ChartPalette.Length]);
        }

        private static void RenderBar(Plot plot, ChartDisplayPayload payload, bool horizontal)
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
                    FillColor = PaletteColor(items[i].Color, i),
                    Label = items[i].Value.ToString("G4")
                });
            }

            var bp = plot.Add.Bars(bars.ToArray());
            bp.Horizontal = horizontal;

            var positions = Enumerable.Range(0, items.Count).Select(i => (double)i).ToArray();
            var labels = items.Select(c => c.Label).ToArray();
            if (horizontal)
                plot.Axes.Left.SetTicks(positions, labels);
            else
                plot.Axes.Bottom.SetTicks(positions, labels);

            plot.Axes.Margins(bottom: 0);
        }

        private static void RenderGroupedBar(Plot plot, ChartDisplayPayload payload, bool stacked)
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
                        FillColor = PaletteColor(series.Color, s)
                    });
                }
                var bp = plot.Add.Bars(bars.ToArray());
                bp.LegendText = series.SeriesName;
            }

            var positions = Enumerable.Range(0, catCount).Select(i => (double)i).ToArray();
            var catLabels = categories.Select(c => c.Label).ToArray();
            plot.Axes.Bottom.SetTicks(positions, catLabels);
            plot.ShowLegend(Alignment.UpperRight);
            plot.Axes.Margins(bottom: 0);
        }

        private static void RenderPie(Plot plot, ChartDisplayPayload payload, bool donut)
        {
            var items = payload.Categories;
            if (items == null || items.Count == 0) return;

            var slices = items.Select((c, i) => new PieSlice
            {
                Value = c.Value,
                FillColor = PaletteColor(c.Color, i),
                Label = c.Label,
                LegendText = $"{c.Label} ({c.Value:G4})"
            }).ToList();

            var pie = plot.Add.Pie(slices);
            pie.ExplodeFraction = payload.ExplodeFraction;
            if (donut)
                pie.DonutFraction = payload.DonutFraction > 0 ? payload.DonutFraction : 0.5;

            plot.Axes.Frameless();
            plot.HideGrid();
            plot.ShowLegend(Alignment.LowerRight);
        }

        private static void RenderRadar(Plot plot, ChartDisplayPayload payload)
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
                var grid = plot.Add.Scatter(gx, gy);
                grid.Color = ScottPlot.Color.FromHex("#e2e8f0");
                grid.LineWidth = 0.5f;
                grid.MarkerSize = 0;
            }

            for (int i = 0; i < n; i++)
            {
                var angle = -Math.PI / 2 + i * angleStep;
                var line = plot.Add.Line(0, 0, radius * Math.Cos(angle), radius * Math.Sin(angle));
                line.Color = ScottPlot.Color.FromHex("#cbd5e1");
                line.LineWidth = 0.5f;
                plot.Add.Text(items[i].Label, radius * 1.18 * Math.Cos(angle), radius * 1.18 * Math.Sin(angle));
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

            var fill = plot.Add.Scatter(xs, ys);
            fill.Color = ScottPlot.Color.FromHex("#3b82f6");
            fill.LineWidth = 2;
            fill.MarkerSize = 5;
            fill.FillY = true;
            fill.FillYColor = ScottPlot.Color.FromHex("#3b82f6").WithAlpha(50);

            plot.Axes.Frameless();
            plot.HideGrid();
        }

        private static void AddHorizontalLimits(Plot plot, ChartDisplayPayload payload, TestTreeNodeItem item)
        {
            var lo = payload.HorizontalLimitLower ?? item.LowerLimit;
            var hi = payload.HorizontalLimitUpper ?? item.UpperLimit;

            if (double.IsFinite(lo) || double.IsFinite(hi))
            {
                if (double.IsFinite(lo))
                {
                    var hlLo = plot.Add.HorizontalLine(lo);
                    hlLo.Color = ScottPlot.Colors.OrangeRed;
                    hlLo.LineWidth = 1;
                    hlLo.LinePattern = LinePattern.Dashed;
                }

                if (double.IsFinite(hi))
                {
                    var hlHi = plot.Add.HorizontalLine(hi);
                    hlHi.Color = ScottPlot.Colors.OrangeRed;
                    hlHi.LineWidth = 1;
                    hlHi.LinePattern = LinePattern.Dashed;
                }
            }
        }

        private void ApplyItemPlotStyleToAllPlots()
        {
            var styleOptions = ScottPlotStyleHelper.CreateThemeStyleOptions();
            ScottPlotStyleHelper.ApplyToPlotAndSubplots(ItemPlot.Plot, ItemPlot.Multiplot, styleOptions);
        }
    }
}
