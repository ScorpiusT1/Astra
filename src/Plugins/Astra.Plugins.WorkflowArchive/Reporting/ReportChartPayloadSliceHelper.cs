using Astra.UI.Abstractions.Nodes;

namespace Astra.Plugins.WorkflowArchive.Reporting
{
    /// <summary>
    /// 将多子图（<see cref="ChartLayoutMode.SubPlots"/>）载荷中的某一子系列展开为根级单图载荷，供报告逐张 PNG 渲染。
    /// </summary>
    internal static class ReportChartPayloadSliceHelper
    {
        public static ChartDisplayPayload BuildSubPlotSlice(ChartDisplayPayload parent, int index)
        {
            if (parent.Series == null || index < 0 || index >= parent.Series.Count)
                return parent;

            var inner = parent.Series[index].Data.Clone();

            static string PickAxis(string innerVal, string parentVal) =>
                string.IsNullOrWhiteSpace(innerVal) ? parentVal : innerVal;

            return new ChartDisplayPayload
            {
                Kind = inner.Kind,
                BottomAxisLabel = PickAxis(inner.BottomAxisLabel, parent.BottomAxisLabel),
                BottomAxisUnit = PickAxis(inner.BottomAxisUnit, parent.BottomAxisUnit),
                LeftAxisLabel = PickAxis(inner.LeftAxisLabel, parent.LeftAxisLabel),
                LeftAxisUnit = PickAxis(inner.LeftAxisUnit, parent.LeftAxisUnit),
                SignalY = inner.SignalY,
                SamplePeriod = inner.SamplePeriod,
                X = inner.X,
                Y = inner.Y,
                HeatmapZ = inner.HeatmapZ,
                HeatmapXCoordinates = inner.HeatmapXCoordinates,
                HeatmapYCoordinates = inner.HeatmapYCoordinates,
                HeatmapYAxisIsLog10OfQuantity = inner.HeatmapYAxisIsLog10OfQuantity,
                SegmentLines = inner.SegmentLines,
                HorizontalLimitLower = inner.HorizontalLimitLower ?? parent.HorizontalLimitLower,
                HorizontalLimitUpper = inner.HorizontalLimitUpper ?? parent.HorizontalLimitUpper,
                Categories = inner.Categories,
                BarGroups = inner.BarGroups,
                DonutFraction = inner.DonutFraction,
                ExplodeFraction = inner.ExplodeFraction,
                RadarAxisMaxValues = inner.RadarAxisMaxValues,
                Series = null,
                LayoutMode = ChartLayoutMode.SinglePlot,
                ScalarAnnotations = inner.ScalarAnnotations ?? parent.ScalarAnnotations
            };
        }
    }
}
