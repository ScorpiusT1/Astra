using Astra.Core.Data;
using Astra.Core.Nodes.Models;
using Astra.UI.Abstractions.Nodes;
using System.Collections.Generic;
using System.Linq;

namespace Astra.Plugins.DataImport.Helpers
{
    internal static class DataImportResultPublisher
    {
        public static ExecutionResult SuccessWithChart(
            NodeContext context,
            string producerNodeId,
            string artifactName,
            ChartDisplayPayload chart,
            string? tag = null,
            string message = "完成",
            bool includeInTestReport = true)
        {
            var bus = context.GetDataBus();
            if (bus == null)
                return ExecutionResult.Failed("测试数据总线不可用，请确认工作流由引擎正常启动。");

            var r = bus.PublishAlgorithmResult(producerNodeId, artifactName, chart, tag: tag, includeInTestReport: includeInTestReport);
            return ExecutionResult.Successful(message)
                .WithOutput(NodeUiOutputKeys.HasChartData, true)
                .WithOutput(NodeUiOutputKeys.ChartArtifactKey, r.Key)
                .WithOutput(NodeUiOutputKeys.ChartXAxisLabel, chart.BottomAxisLabel ?? string.Empty)
                .WithOutput(NodeUiOutputKeys.ChartXAxisUnit, chart.BottomAxisUnit ?? string.Empty)
                .WithOutput(NodeUiOutputKeys.ChartYAxisLabel, chart.LeftAxisLabel ?? string.Empty)
                .WithOutput(NodeUiOutputKeys.ChartYAxisUnit, chart.LeftAxisUnit ?? string.Empty);
        }

        /// <summary>多文件导入预览：多路 Signal 合并为单载荷多 Series；单条时退化为 <see cref="SuccessWithChart"/>。</summary>
        public static ExecutionResult SuccessWithMultiChart(
            NodeContext context,
            string producerNodeId,
            string artifactName,
            IReadOnlyList<(string SeriesName, ChartDisplayPayload Chart)> charts,
            ChartLayoutMode? layoutOverride = null,
            string? tag = null,
            string message = "完成",
            bool includeInTestReport = true)
        {
            if (charts == null || charts.Count == 0)
                return ExecutionResult.Failed("无可发布的图表数据。");

            if (charts.Count == 1)
                return SuccessWithChart(context, producerNodeId, artifactName, charts[0].Chart, tag, message, includeInTestReport);

            var series = charts.Select(c => new ChartSeriesEntry
            {
                Name = c.SeriesName,
                IsVisibleByDefault = true,
                Data = c.Chart
            }).ToList();

            var merged = new ChartDisplayPayload
            {
                Kind = series[0].Data.Kind,
                Series = series,
                LayoutMode = layoutOverride ?? ChartDisplayPayload.InferDefaultLayout(series),
                BottomAxisLabel = series[0].Data.BottomAxisLabel,
                BottomAxisUnit = series[0].Data.BottomAxisUnit,
                LeftAxisLabel = series[0].Data.LeftAxisLabel,
                LeftAxisUnit = series[0].Data.LeftAxisUnit
            };

            return SuccessWithChart(context, producerNodeId, artifactName, merged, tag, message, includeInTestReport);
        }
    }
}
