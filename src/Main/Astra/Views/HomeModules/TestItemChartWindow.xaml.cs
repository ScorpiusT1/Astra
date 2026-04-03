using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Astra.UI.Abstractions.Nodes;
using Astra.Services.Home;
using Astra.ViewModels.HomeModules;
using Astra.UI.Helpers;
using Microsoft.Extensions.DependencyInjection;
using ScottPlot;
using ScottPlot.Plottables;
using ScottPlot.TickGenerators;
using ScottPlot.WPF;

namespace Astra.Views.HomeModules
{
    public partial class TestItemChartWindow : Window
    {
        private ChartDisplayPayload? _currentPayload;
        private bool _isUpdatingLayoutCombo;
        private bool _isMixedKind;

        private readonly List<SeriesVisibilityOption> _seriesOptions = new();
        private readonly Dictionary<int, IPlottable> _singlePlotPlottables = new();

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

        // ────────────────────────────── 入口 ──────────────────────────────

        private void RenderChart()
        {
            if (DataContext is not TestTreeNodeItem item)
                return;

            ItemPlot.Plot.Clear();
            ClearSubPlots();
            _currentPayload = null;
            _singlePlotPlottables.Clear();

            var cache = App.ServiceProvider?.GetService<IChartDisplayDataCache>();
            if (cache == null || !cache.TryGetPayload(item.NodeId, out var payload) || payload == null)
            {
                HideMultiSeriesUi();
                ItemPlot.Refresh();
                return;
            }

            _currentPayload = payload;

            if (HasMultiSeries(payload))
            {
                BuildMultiSeriesUi(payload);
                RenderMultiSeries(payload);
            }
            else
            {
                HideMultiSeriesUi();
                RenderSinglePayload(ItemPlot.Plot, payload);
                var isPieOrRadar = payload.Kind is ChartPayloadKind.Pie or ChartPayloadKind.Donut or ChartPayloadKind.Radar;
                if (!isPieOrRadar)
                    AddHorizontalLimits(ItemPlot.Plot, payload, item);
                ItemPlot.Plot.Axes.AutoScale();
                AddScalarAnnotations(ItemPlot.Plot, payload.ScalarAnnotations);
                ItemPlot.Refresh();
            }
        }

        // ────────────────────────────── 单图模式（无 Series） ──────────────────────────────

        private static void RenderSinglePayload(Plot plot, ChartDisplayPayload payload)
        {
            var bottomAxisTitle = ChartDisplayPayload.FormatAxisTitle(payload.BottomAxisLabel, payload.BottomAxisUnit);
            var leftAxisTitle = ChartDisplayPayload.FormatAxisTitle(payload.LeftAxisLabel, payload.LeftAxisUnit);
            if (string.IsNullOrWhiteSpace(bottomAxisTitle)) bottomAxisTitle = "X";
            if (string.IsNullOrWhiteSpace(leftAxisTitle)) leftAxisTitle = "Y";
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
            }
        }

        // ────────────────────────────── 多系列 UI ──────────────────────────────

        private static bool HasMultiSeries(ChartDisplayPayload payload)
            => payload.Series != null && payload.Series.Count > 0;

        private void BuildMultiSeriesUi(ChartDisplayPayload payload)
        {
            var series = payload.Series!;

            _isMixedKind = series.Select(s => s.Data.Kind).Distinct().Count() > 1;

            ToolbarPanel.Visibility = Visibility.Visible;
            LeftPanelBorder.Visibility = Visibility.Visible;
            SplitterBorder.Visibility = Visibility.Visible;

            LayoutModeCombo.IsEnabled = !_isMixedKind;

            CurveVisibilityPanel.Children.Clear();
            foreach (var opt in _seriesOptions) opt.Changed -= OnSeriesVisibilityChanged;
            _seriesOptions.Clear();

            for (var i = 0; i < series.Count; i++)
            {
                var name = string.IsNullOrWhiteSpace(series[i].Name) ? $"系列 {i + 1}" : series[i].Name.Trim();
                var option = new SeriesVisibilityOption { Name = name, IsVisible = series[i].IsVisibleByDefault };
                option.Changed += OnSeriesVisibilityChanged;
                _seriesOptions.Add(option);

                var checkBox = new CheckBox
                {
                    Content = name,
                    IsChecked = option.IsVisible,
                    Margin = new Thickness(0, 0, 0, 6)
                };
                var captured = option;
                checkBox.Checked += (_, _) => captured.IsVisible = true;
                checkBox.Unchecked += (_, _) => captured.IsVisible = false;
                CurveVisibilityPanel.Children.Add(checkBox);
            }
        }

        private void HideMultiSeriesUi()
        {
            ToolbarPanel.Visibility = Visibility.Collapsed;
            LeftPanelBorder.Visibility = Visibility.Collapsed;
            SplitterBorder.Visibility = Visibility.Collapsed;
            CurveVisibilityPanel.Children.Clear();
            foreach (var opt in _seriesOptions) opt.Changed -= OnSeriesVisibilityChanged;
            _seriesOptions.Clear();

            ItemPlot.Visibility = Visibility.Visible;
            SubPlotScrollViewer.Visibility = Visibility.Collapsed;
        }

        // ────────────────────────────── 多系列渲染 ──────────────────────────────

        private void RenderMultiSeries(ChartDisplayPayload payload)
        {
            var series = payload.Series ?? new List<ChartSeriesEntry>();
            var useSubPlots = payload.LayoutMode == ChartLayoutMode.SubPlots;

            _isUpdatingLayoutCombo = true;
            LayoutModeCombo.SelectedIndex = useSubPlots ? 1 : 0;
            _isUpdatingLayoutCombo = false;

            if (!useSubPlots)
            {
                RenderMultiSeriesSinglePlot(payload, series);
            }
            else
            {
                RenderMultiSeriesSubPlots(payload, series);
            }
        }

        private void RenderMultiSeriesSinglePlot(ChartDisplayPayload payload, List<ChartSeriesEntry> series)
        {
            ItemPlot.Visibility = Visibility.Visible;
            SubPlotScrollViewer.Visibility = Visibility.Collapsed;
            ClearSubPlots();

            var plt = ItemPlot.Plot;
            plt.Clear();
            _singlePlotPlottables.Clear();

            var bottomAxisTitle = ChartDisplayPayload.FormatAxisTitle(payload.BottomAxisLabel, payload.BottomAxisUnit);
            var leftAxisTitle = ChartDisplayPayload.FormatAxisTitle(payload.LeftAxisLabel, payload.LeftAxisUnit);
            if (string.IsNullOrWhiteSpace(bottomAxisTitle)) bottomAxisTitle = "X";
            if (string.IsNullOrWhiteSpace(leftAxisTitle)) leftAxisTitle = "Y";
            plt.Axes.Bottom.Label.Text = bottomAxisTitle;
            plt.Axes.Left.Label.Text = leftAxisTitle;

            for (var i = 0; i < series.Count; i++)
            {
                var isVisible = i < _seriesOptions.Count && _seriesOptions[i].IsVisible;
                var plottable = AddSeriesEntryToPlot(plt, series[i], i);
                if (plottable != null)
                {
                    plottable.IsVisible = isVisible;
                    _singlePlotPlottables[i] = plottable;
                }
            }

            if (series.Count > 0)
            {
                plt.ShowLegend(Alignment.UpperRight);
                var item = DataContext as TestTreeNodeItem ?? new TestTreeNodeItem();
                AddHorizontalLimits(plt, payload, item);
                plt.Axes.AutoScale();
                AddScalarAnnotations(plt, CollectVisibleScalarAnnotations(payload, _seriesOptions));
            }

            ApplyItemPlotStyleToAllPlots();
            ItemPlot.Refresh();
        }

        private void RenderMultiSeriesSubPlots(ChartDisplayPayload payload, List<ChartSeriesEntry> series)
        {
            ItemPlot.Visibility = Visibility.Collapsed;
            SubPlotScrollViewer.Visibility = Visibility.Visible;
            ClearSubPlots();
            _singlePlotPlottables.Clear();

            var bottomAxisTitle = ChartDisplayPayload.FormatAxisTitle(payload.BottomAxisLabel, payload.BottomAxisUnit);
            var leftAxisTitle = ChartDisplayPayload.FormatAxisTitle(payload.LeftAxisLabel, payload.LeftAxisUnit);
            if (string.IsNullOrWhiteSpace(bottomAxisTitle)) bottomAxisTitle = "X";
            if (string.IsNullOrWhiteSpace(leftAxisTitle)) leftAxisTitle = "Y";

            for (var i = 0; i < series.Count; i++)
            {
                var isVisible = i < _seriesOptions.Count && _seriesOptions[i].IsVisible;
                if (!isVisible)
                    continue;

                var entry = series[i];
                var host = new Border
                {
                    Margin = new Thickness(4, 4, 4, 4),
                    BorderThickness = new Thickness(1),
                    BorderBrush = TryFindResource("BorderBrush") as System.Windows.Media.Brush,
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(6)
                };

                var subPlotControl = new WpfPlot { Height = 260 };
                var sp = subPlotControl.Plot;
                sp.Axes.Bottom.Label.Text = bottomAxisTitle;
                sp.Axes.Left.Label.Text = leftAxisTitle;
                RenderSinglePayload(sp, entry.Data);

                var item = DataContext as TestTreeNodeItem ?? new TestTreeNodeItem();
                var isPieOrRadar = entry.Data.Kind is ChartPayloadKind.Pie or ChartPayloadKind.Donut or ChartPayloadKind.Radar;
                if (!isPieOrRadar)
                    AddHorizontalLimits(sp, payload, item);

                sp.Axes.AutoScale();
                AddScalarAnnotations(sp, entry.Data.ScalarAnnotations);
                sp.Title(entry.Name);

                var styleOptions = ScottPlotStyleHelper.CreateThemeStyleOptions();
                ScottPlotStyleHelper.ApplyToPlotAndSubplots(sp, subPlotControl.Multiplot, styleOptions);
                subPlotControl.Refresh();

                host.Child = subPlotControl;
                SubPlotHost.Children.Add(host);
            }
        }

        /// <summary>
        /// 向 Plot 添加一个系列条目并返回其主 Plottable（用于后续 IsVisible 翻转）。
        /// 复用已有的 Render* 方法——不同的 Kind 有不同的"主 Plottable"类型。
        /// </summary>
        private static IPlottable? AddSeriesEntryToPlot(Plot plot, ChartSeriesEntry entry, int index)
        {
            var data = entry.Data;
            var color = PaletteColor(entry.Color, index);
            var name = string.IsNullOrWhiteSpace(entry.Name) ? $"系列 {index + 1}" : entry.Name.Trim();

            switch (data.Kind)
            {
                case ChartPayloadKind.Signal1D:
                {
                    var y = data.SignalY;
                    if (y == null || y.Length == 0) return null;
                    var period = data.SamplePeriod > 0 ? data.SamplePeriod : 1.0;
                    var sig = plot.Add.Signal(y, period);
                    sig.LegendText = name;
                    sig.LineWidth = 1.5f;
                    sig.Color = color;
                    return sig;
                }
                case ChartPayloadKind.XYLine:
                case ChartPayloadKind.XYScatter:
                {
                    var xs = data.X;
                    var ys = data.Y;
                    if (xs == null || ys == null || xs.Length == 0 || xs.Length != ys.Length) return null;
                    var scatter = plot.Add.Scatter(xs, ys);
                    scatter.Color = color;
                    scatter.LegendText = name;
                    if (data.Kind == ChartPayloadKind.XYLine) { scatter.LineWidth = 1.5f; scatter.MarkerSize = 0; }
                    else { scatter.LineWidth = 0; scatter.MarkerSize = 6; }
                    return scatter;
                }
                case ChartPayloadKind.Bar:
                case ChartPayloadKind.HorizontalBar:
                {
                    var items = data.Categories;
                    if (items == null || items.Count == 0) return null;
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
                    bp.Horizontal = data.Kind == ChartPayloadKind.HorizontalBar;
                    bp.LegendText = name;
                    return bp;
                }
                default:
                {
                    RenderSinglePayload(plot, data);
                    return null;
                }
            }
        }

        // ────────────────────────────── 显隐 + 布局切换 ──────────────────────────────

        private void OnSeriesVisibilityChanged()
        {
            if (_currentPayload == null || !HasMultiSeries(_currentPayload))
                return;

            if (_currentPayload.LayoutMode == ChartLayoutMode.SinglePlot && _singlePlotPlottables.Count > 0)
            {
                if (HasAnyScalarAnnotations(_currentPayload))
                {
                    RenderMultiSeriesSinglePlot(_currentPayload, _currentPayload.Series ?? new List<ChartSeriesEntry>());
                    return;
                }

                for (var i = 0; i < _seriesOptions.Count; i++)
                {
                    if (_singlePlotPlottables.TryGetValue(i, out var p))
                    {
                        p.IsVisible = _seriesOptions[i].IsVisible;
                    }
                }
                ItemPlot.Plot.Axes.AutoScale();
                ItemPlot.Refresh();
            }
            else
            {
                RenderMultiSeries(_currentPayload);
            }
        }

        private void OnLayoutModeChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isUpdatingLayoutCombo || _currentPayload == null || !HasMultiSeries(_currentPayload))
                return;

            _currentPayload.LayoutMode = LayoutModeCombo.SelectedIndex == 1
                ? ChartLayoutMode.SubPlots
                : ChartLayoutMode.SinglePlot;

            RenderMultiSeries(_currentPayload);
        }

        // ────────────────────────────── 各类型渲染方法（单图回退） ──────────────────────────────

        private static void RenderSignal1D(Plot plot, ChartDisplayPayload payload)
        {
            var y = payload.SignalY;
            if (y == null || y.Length == 0) return;
            var period = payload.SamplePeriod > 0 ? payload.SamplePeriod : 1.0;
            var sig = plot.Add.Signal(y, period);
            sig.Color = ScottPlot.Color.FromHex("#2196F3");
            sig.LineWidth = 1.5f;
        }

        private static void RenderXY(Plot plot, ChartDisplayPayload payload, bool line)
        {
            var xs = payload.X;
            var ys = payload.Y;
            if (xs == null || ys == null || xs.Length == 0 || xs.Length != ys.Length) return;
            var scatter = plot.Add.Scatter(xs, ys);
            scatter.Color = ScottPlot.Color.FromHex("#2196F3");
            if (line) { scatter.LineWidth = 1.5f; scatter.MarkerSize = 0; }
            else { scatter.LineWidth = 0; scatter.MarkerSize = 6; }
        }

        private static void RenderHeatmap(Plot plot, ChartDisplayPayload payload)
        {
            var z = payload.HeatmapZ;
            if (z == null || z.Length == 0) return;
            var rows = z.GetLength(0);
            var cols = z.GetLength(1);
            if (rows < 1 || cols < 1) return;

            var hm = plot.Add.Heatmap(z);
            hm.Colormap = new ScottPlot.Colormaps.Viridis();
            var xCoords = payload.HeatmapXCoordinates;
            var yCoords = payload.HeatmapYCoordinates;
            if (xCoords != null && yCoords != null && xCoords.Length == cols && yCoords.Length == rows)
            {
                hm.Rectangle = new CoordinateRect(xCoords.Min(), xCoords.Max(), yCoords.Min(), yCoords.Max());
            }

            if (payload.HeatmapYAxisIsLog10OfQuantity)
            {
                // 纵轴数据为 log10(物理量)；ScottPlot Heatmap/YAxis 在数据空间为线性划分（见 ScottPlot Heatmap / YAxisBase 源码），
                // 故在 log 频率轴下将行坐标换为 log10(Hz)，再用刻度格式化为 Hz（与 SP5 LogScaleTicks 思路一致）。
                var tickGen = new NumericAutomatic
                {
                    MinorTickGenerator = new LogMinorTickGenerator(),
                    LabelFormatter = FormatHeatmapLog10AxisTickLabel
                };
                plot.Axes.Left.TickGenerator = tickGen;
            }

            plot.Add.ColorBar(hm);
        }

        /// <summary>纵轴坐标为 log10(Hz) 时的刻度标签。</summary>
        private static string FormatHeatmapLog10AxisTickLabel(double log10Value)
        {
            var v = Math.Pow(10, log10Value);
            if (!double.IsFinite(v) || v <= 0)
                return string.Empty;
            if (v >= 1000)
                return (v / 1000).ToString("G4") + "k";
            if (v >= 1)
                return v.ToString("G4");
            return v.ToString("G3");
        }

        private static void RenderSegments(Plot plot, ChartDisplayPayload payload)
        {
            var seg = payload.SegmentLines;
            if (seg == null || seg.GetLength(1) != 4) return;
            var n = seg.GetLength(0);
            for (var i = 0; i < n; i++)
            {
                var line = plot.Add.Line(seg[i, 0], seg[i, 1], seg[i, 2], seg[i, 3]);
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
            if (horizontal) plot.Axes.Left.SetTicks(positions, labels);
            else plot.Axes.Bottom.SetTicks(positions, labels);
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
            if (donut) pie.DonutFraction = payload.DonutFraction > 0 ? payload.DonutFraction : 0.5;
            plot.Axes.Frameless();
            plot.HideGrid();
            plot.ShowLegend(Alignment.LowerRight);
        }

        private static void RenderRadar(Plot plot, ChartDisplayPayload payload)
        {
            var items = payload.Categories;
            if (items == null || items.Count < 3) return;
            var values = items.Select(c => c.Value).ToArray();
            var maxValues = payload.RadarAxisMaxValues ?? values.Select(v => Math.Max(Math.Ceiling(v * 1.2), 1.0)).ToArray();
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

        // ────────────────────────────── 辅助 ──────────────────────────────

        private static bool HasAnyScalarAnnotations(ChartDisplayPayload payload)
        {
            if (payload.ScalarAnnotations is { Count: > 0 })
                return true;
            if (payload.Series == null)
                return false;
            foreach (var s in payload.Series)
            {
                if (s.Data.ScalarAnnotations is { Count: > 0 })
                    return true;
            }

            return false;
        }

        /// <summary>单图叠加：根载荷标量 + 各可见系列的子 Data 标量。</summary>
        private static List<ChartScalarAnnotation>? CollectVisibleScalarAnnotations(
            ChartDisplayPayload payload,
            IReadOnlyList<SeriesVisibilityOption> options)
        {
            var list = new List<ChartScalarAnnotation>();
            if (payload.ScalarAnnotations is { Count: > 0 })
                list.AddRange(payload.ScalarAnnotations);
            var series = payload.Series;
            if (series == null)
                return list.Count > 0 ? list : null;
            for (var i = 0; i < series.Count; i++)
            {
                if (i < options.Count && !options[i].IsVisible)
                    continue;
                var ann = series[i].Data.ScalarAnnotations;
                if (ann == null || ann.Count == 0)
                    continue;
                list.AddRange(ann);
            }

            return list.Count > 0 ? list : null;
        }

        /// <summary>在数据坐标系左上角叠加标量文本（须在 AutoScale 之后调用）。</summary>
        private static void AddScalarAnnotations(Plot plot, IReadOnlyList<ChartScalarAnnotation>? annotations)
        {
            if (annotations == null || annotations.Count == 0)
                return;

            var r = plot.Axes.GetLimits();
            var dx = r.Right - r.Left;
            var dy = r.Top - r.Bottom;
            if (!double.IsFinite(dx) || dx <= 0)
                dx = 1;
            if (!double.IsFinite(dy) || dy <= 0)
                dy = 1;

            var x = r.Left + 0.02 * dx;
            var lineHeight = Math.Max(0.028 * dy, 1e-6);
            var yTop = r.Top - 0.03 * dy;

            for (var i = 0; i < annotations.Count; i++)
            {
                var a = annotations[i];
                var u = string.IsNullOrWhiteSpace(a.Unit) ? string.Empty : $" {a.Unit}";
                var line = $"{a.Name}: {a.Value:G6}{u}";
                var y = yTop - i * lineHeight;
                var txt = plot.Add.Text(line, x, y);
                txt.LabelFontSize = 11;
                txt.LabelBold = false;
                txt.Alignment = Alignment.UpperLeft;
            }
        }

        private static void AddHorizontalLimits(Plot plot, ChartDisplayPayload payload, TestTreeNodeItem item)
        {
            var lo = payload.HorizontalLimitLower ?? item.LowerLimit;
            var hi = payload.HorizontalLimitUpper ?? item.UpperLimit;
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

        private void ClearSubPlots()
        {
            SubPlotHost.Children.Clear();
        }

        private void ApplyItemPlotStyleToAllPlots()
        {
            var styleOptions = ScottPlotStyleHelper.CreateThemeStyleOptions();
            ScottPlotStyleHelper.ApplyToPlotAndSubplots(ItemPlot.Plot, ItemPlot.Multiplot, styleOptions);
        }
    }

    internal sealed class SeriesVisibilityOption
    {
        private bool _isVisible;
        public string Name { get; set; } = string.Empty;
        public bool IsVisible
        {
            get => _isVisible;
            set
            {
                if (_isVisible == value) return;
                _isVisible = value;
                Changed?.Invoke();
            }
        }

        public event Action? Changed;
    }
}
