using Astra.Core.Constants;
using Astra.UI.Abstractions.Nodes;

namespace Astra.Reporting
{
    /// <summary>
    /// 报告中「原始数据」一维波形图的轴约定：横轴为时间，纵轴为幅值；单位沿用载荷或通道元数据，时间轴无单位时默认秒。
    /// </summary>
    internal static class ReportRawWaveformChartAxisHelper
    {
        /// <summary>
        /// 对 Raw 分层下的 <see cref="ChartPayloadKind.Signal1D"/> 载荷统一轴标题，并补全默认时间单位；Y 轴单位保留载荷中已有值。
        /// </summary>
        public static ChartDisplayPayload ApplySignal1DRawReportAxes(ChartDisplayPayload payload)
        {
            if (payload.Kind != ChartPayloadKind.Signal1D)
                return payload;

            var b = payload.Clone();
            var xu = b.BottomAxisUnit?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(xu))
                xu = AstraSharedConstants.DataAcquisitionDefaults.CodeDefinedChartXAxisUnit;

            var yu = b.LeftAxisUnit?.Trim() ?? string.Empty;

            return new ChartDisplayPayload
            {
                Kind = b.Kind,
                BottomAxisLabel = AstraSharedConstants.DataAcquisitionDefaults.CodeDefinedChartXAxisLabel,
                BottomAxisUnit = xu,
                LeftAxisLabel = AstraSharedConstants.DataAcquisitionDefaults.CodeDefinedChartYAxisLabel,
                LeftAxisUnit = yu,
                SignalY = b.SignalY,
                SamplePeriod = b.SamplePeriod,
                X = b.X,
                Y = b.Y,
                HeatmapZ = b.HeatmapZ,
                HeatmapXCoordinates = b.HeatmapXCoordinates,
                HeatmapYCoordinates = b.HeatmapYCoordinates,
                HeatmapYAxisIsLog10OfQuantity = b.HeatmapYAxisIsLog10OfQuantity,
                SegmentLines = b.SegmentLines,
                HorizontalLimitLower = b.HorizontalLimitLower,
                HorizontalLimitUpper = b.HorizontalLimitUpper,
                Categories = b.Categories,
                BarGroups = b.BarGroups,
                DonutFraction = b.DonutFraction,
                ExplodeFraction = b.ExplodeFraction,
                RadarAxisMaxValues = b.RadarAxisMaxValues,
                Series = b.Series,
                LayoutMode = b.LayoutMode,
                ScalarAnnotations = b.ScalarAnnotations
            };
        }
    }
}
