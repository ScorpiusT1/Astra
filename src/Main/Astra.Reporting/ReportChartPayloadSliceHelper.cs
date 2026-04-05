using Astra.UI.Abstractions.Nodes;

namespace Astra.Reporting
{
    /// <summary>
    /// 图表载荷切片辅助类：把 <see cref="ChartLayoutMode.SubPlots"/> 布局下的某一子系列克隆为独立的
    /// <see cref="ChartLayoutMode.SinglePlot"/> <see cref="ChartDisplayPayload"/>，便于报告引擎按子图分别调用
    /// <see cref="ReportChartRenderer"/>。
    /// </summary>
    internal static class ReportChartPayloadSliceHelper
    {
        /// <summary>
        /// 从父级子图载荷中取出指定索引的子系列，克隆为根级 <see cref="ChartLayoutMode.SinglePlot"/> 载荷，供报告逐张 PNG 渲染。
        /// </summary>
        /// <param name="parent">布局为 <see cref="ChartLayoutMode.SubPlots"/> 且含 <see cref="ChartDisplayPayload.Series"/> 的父载荷。</param>
        /// <param name="index">子系列在 <see cref="ChartDisplayPayload.Series"/> 中的从零开始索引。</param>
        /// <returns>
        /// 索引有效时返回新的单图载荷（轴标签在子项为空时继承父项，上下限等择优合并）；
        /// 若系列缺失或索引越界则原样返回 <paramref name="parent"/>。
        /// </returns>
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
