namespace Astra.UI.Abstractions.Nodes;

/// <summary>
/// 便于算法节点构造 <see cref="ChartDisplayPayload"/>（与 Raw 存储或 <see cref="NodeUiOutputKeys.ChartPayloadSnapshot"/> 配合使用）。
/// </summary>
public static class ChartDisplayPayloadFactory
{
    public static ChartDisplayPayload Signal1D(
        double[] y,
        double samplePeriod = 1.0,
        string bottomAxisLabel = "样本",
        string leftAxisLabel = "数值",
        double? horizontalLower = null,
        double? horizontalUpper = null)
    {
        return new ChartDisplayPayload
        {
            Kind = ChartPayloadKind.Signal1D,
            SignalY = y,
            SamplePeriod = samplePeriod > 0 ? samplePeriod : 1.0,
            BottomAxisLabel = bottomAxisLabel ?? string.Empty,
            LeftAxisLabel = leftAxisLabel ?? string.Empty,
            HorizontalLimitLower = horizontalLower,
            HorizontalLimitUpper = horizontalUpper
        };
    }

    public static ChartDisplayPayload XYLine(
        double[] x,
        double[] y,
        string bottomAxisLabel,
        string leftAxisLabel,
        double? horizontalLower = null,
        double? horizontalUpper = null)
    {
        return new ChartDisplayPayload
        {
            Kind = ChartPayloadKind.XYLine,
            X = x,
            Y = y,
            BottomAxisLabel = bottomAxisLabel ?? string.Empty,
            LeftAxisLabel = leftAxisLabel ?? string.Empty,
            HorizontalLimitLower = horizontalLower,
            HorizontalLimitUpper = horizontalUpper
        };
    }

    public static ChartDisplayPayload XYScatter(
        double[] x,
        double[] y,
        string bottomAxisLabel,
        string leftAxisLabel,
        double? horizontalLower = null,
        double? horizontalUpper = null)
    {
        return new ChartDisplayPayload
        {
            Kind = ChartPayloadKind.XYScatter,
            X = x,
            Y = y,
            BottomAxisLabel = bottomAxisLabel ?? string.Empty,
            LeftAxisLabel = leftAxisLabel ?? string.Empty,
            HorizontalLimitLower = horizontalLower,
            HorizontalLimitUpper = horizontalUpper
        };
    }

    /// <summary>
    /// <paramref name="z"/> 维度 [row, col]，行对应 <paramref name="yCoordinates"/>，列对应 <paramref name="xCoordinates"/>。
    /// </summary>
    public static ChartDisplayPayload Heatmap(
        double[,] z,
        double[] xCoordinates,
        double[] yCoordinates,
        string bottomAxisLabel,
        string leftAxisLabel,
        bool heatmapYAxisIsLog10OfQuantity = false)
    {
        return new ChartDisplayPayload
        {
            Kind = ChartPayloadKind.Heatmap,
            HeatmapZ = z,
            HeatmapXCoordinates = xCoordinates,
            HeatmapYCoordinates = yCoordinates,
            HeatmapYAxisIsLog10OfQuantity = heatmapYAxisIsLog10OfQuantity,
            BottomAxisLabel = bottomAxisLabel ?? string.Empty,
            LeftAxisLabel = leftAxisLabel ?? string.Empty
        };
    }

    /// <summary>
    /// 多段线段，每行 x0,y0,x1,y1。
    /// </summary>
    public static ChartDisplayPayload Segments(
        double[,] segmentLinesX0Y0X1Y1,
        string bottomAxisLabel,
        string leftAxisLabel,
        double? horizontalLower = null,
        double? horizontalUpper = null)
    {
        return new ChartDisplayPayload
        {
            Kind = ChartPayloadKind.LineSegments,
            SegmentLines = segmentLinesX0Y0X1Y1,
            BottomAxisLabel = bottomAxisLabel ?? string.Empty,
            LeftAxisLabel = leftAxisLabel ?? string.Empty,
            HorizontalLimitLower = horizontalLower,
            HorizontalLimitUpper = horizontalUpper
        };
    }

    /// <summary>
    /// 折线图：按横轴顺序用线段依次连接各点 (x[i], y[i])，与 <see cref="XYLine"/> 等价，
    /// 适用于倍频程等「中心频率—幅值」折线展示。
    /// </summary>
    public static ChartDisplayPayload Polyline(
        double[] x,
        double[] y,
        string bottomAxisLabel,
        string leftAxisLabel,
        double? horizontalLower = null,
        double? horizontalUpper = null)
    {
        return XYLine(x, y, bottomAxisLabel, leftAxisLabel, horizontalLower, horizontalUpper);
    }

    /// <summary>
    /// 在每个横坐标 <paramref name="x"/> 处绘制从 <paramref name="yBase"/> 到 <paramref name="y"/> 的竖线段（茎状图），
    /// 底层为 <see cref="ChartPayloadKind.LineSegments"/>；适合倍频程用线段代替柱状图时的竖线效果。
    /// </summary>
    /// <param name="yBase">竖线起点纵坐标，一般为 0。</param>
    public static ChartDisplayPayload VerticalLineSegments(
        double[] x,
        double[] y,
        double yBase = 0,
        string bottomAxisLabel = "",
        string leftAxisLabel = "",
        double? horizontalLower = null,
        double? horizontalUpper = null)
    {
        if (x == null || y == null || x.Length != y.Length || x.Length == 0)
        {
            return Segments(new double[0, 4], bottomAxisLabel, leftAxisLabel, horizontalLower, horizontalUpper);
        }

        var n = x.Length;
        var seg = new double[n, 4];
        for (var i = 0; i < n; i++)
        {
            seg[i, 0] = x[i];
            seg[i, 1] = yBase;
            seg[i, 2] = x[i];
            seg[i, 3] = y[i];
        }

        return Segments(seg, bottomAxisLabel, leftAxisLabel, horizontalLower, horizontalUpper);
    }

    /// <summary>垂直柱状图。</summary>
    public static ChartDisplayPayload Bar(
        string[] labels,
        double[] values,
        string bottomAxisLabel = "",
        string leftAxisLabel = "",
        string[]? colors = null,
        double? horizontalLower = null,
        double? horizontalUpper = null)
    {
        return new ChartDisplayPayload
        {
            Kind = ChartPayloadKind.Bar,
            Categories = BuildCategories(labels, values, colors),
            BottomAxisLabel = bottomAxisLabel ?? string.Empty,
            LeftAxisLabel = leftAxisLabel ?? string.Empty,
            HorizontalLimitLower = horizontalLower,
            HorizontalLimitUpper = horizontalUpper
        };
    }

    /// <summary>水平条形图。</summary>
    public static ChartDisplayPayload HorizontalBar(
        string[] labels,
        double[] values,
        string bottomAxisLabel = "",
        string leftAxisLabel = "",
        string[]? colors = null)
    {
        return new ChartDisplayPayload
        {
            Kind = ChartPayloadKind.HorizontalBar,
            Categories = BuildCategories(labels, values, colors),
            BottomAxisLabel = bottomAxisLabel ?? string.Empty,
            LeftAxisLabel = leftAxisLabel ?? string.Empty
        };
    }

    /// <summary>分组柱状图。</summary>
    public static ChartDisplayPayload GroupedBar(
        string[] categoryLabels,
        List<ChartBarSeries> series,
        string bottomAxisLabel = "",
        string leftAxisLabel = "")
    {
        return new ChartDisplayPayload
        {
            Kind = ChartPayloadKind.GroupedBar,
            Categories = categoryLabels.Select(l => new ChartCategoryItem { Label = l }).ToList(),
            BarGroups = series,
            BottomAxisLabel = bottomAxisLabel ?? string.Empty,
            LeftAxisLabel = leftAxisLabel ?? string.Empty
        };
    }

    /// <summary>堆叠柱状图。</summary>
    public static ChartDisplayPayload StackedBar(
        string[] categoryLabels,
        List<ChartBarSeries> series,
        string bottomAxisLabel = "",
        string leftAxisLabel = "")
    {
        return new ChartDisplayPayload
        {
            Kind = ChartPayloadKind.StackedBar,
            Categories = categoryLabels.Select(l => new ChartCategoryItem { Label = l }).ToList(),
            BarGroups = series,
            BottomAxisLabel = bottomAxisLabel ?? string.Empty,
            LeftAxisLabel = leftAxisLabel ?? string.Empty
        };
    }

    /// <summary>饼图。</summary>
    public static ChartDisplayPayload Pie(
        string[] labels,
        double[] values,
        string[]? colors = null,
        double explodeFraction = 0.05)
    {
        return new ChartDisplayPayload
        {
            Kind = ChartPayloadKind.Pie,
            Categories = BuildCategories(labels, values, colors),
            ExplodeFraction = explodeFraction
        };
    }

    /// <summary>环形图。</summary>
    public static ChartDisplayPayload Donut(
        string[] labels,
        double[] values,
        string[]? colors = null,
        double donutFraction = 0.5)
    {
        return new ChartDisplayPayload
        {
            Kind = ChartPayloadKind.Donut,
            Categories = BuildCategories(labels, values, colors),
            DonutFraction = donutFraction > 0 ? donutFraction : 0.5
        };
    }

    /// <summary>雷达图。</summary>
    public static ChartDisplayPayload Radar(
        string[] axisLabels,
        double[] values,
        double[]? axisMaxValues = null,
        string leftAxisLabel = "")
    {
        return new ChartDisplayPayload
        {
            Kind = ChartPayloadKind.Radar,
            Categories = BuildCategories(axisLabels, values, null),
            RadarAxisMaxValues = axisMaxValues,
            LeftAxisLabel = leftAxisLabel ?? string.Empty
        };
    }

    private static List<ChartCategoryItem> BuildCategories(string[] labels, double[] values, string[]? colors)
    {
        var list = new List<ChartCategoryItem>();
        var count = Math.Min(labels?.Length ?? 0, values?.Length ?? 0);
        for (int i = 0; i < count; i++)
        {
            list.Add(new ChartCategoryItem
            {
                Label = labels![i] ?? string.Empty,
                Value = values![i],
                Color = colors != null && i < colors.Length ? colors[i] : null
            });
        }
        return list;
    }
}
