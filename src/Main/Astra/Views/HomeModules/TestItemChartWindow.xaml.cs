using System;
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
                default:
                    ItemPlot.Refresh();
                    return;
            }

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
