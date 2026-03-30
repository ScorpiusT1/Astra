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
        string leftAxisLabel)
    {
        return new ChartDisplayPayload
        {
            Kind = ChartPayloadKind.Heatmap,
            HeatmapZ = z,
            HeatmapXCoordinates = xCoordinates,
            HeatmapYCoordinates = yCoordinates,
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
}
