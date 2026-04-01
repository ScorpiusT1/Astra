using Astra.Core.Data;
using Astra.Core.Nodes.Models;
using Astra.UI.Abstractions.Nodes;
using System.Collections.Generic;
using System.Linq;

namespace Astra.Plugins.Algorithms.Helpers
{
    /// <summary>
    /// 将算法结果写入 <see cref="ITestDataBus"/> 并填充 <see cref="NodeUiOutputKeys"/>。
    /// </summary>
    internal static class AlgorithmResultPublisher
    {
        public static ExecutionResult SuccessWithChart(
            NodeContext context,
            string producerNodeId,
            string artifactName,
            ChartDisplayPayload chart,
            string? tag = null,
            string message = "完成")
        {
            var bus = context.GetDataBus();
            if (bus == null)
                return ExecutionResult.Failed("测试数据总线不可用，请确认工作流由引擎正常启动。");

            var r = bus.PublishAlgorithmResult(producerNodeId, artifactName, chart, tag: tag);
            return ExecutionResult.Successful(message)
                .WithOutput(NodeUiOutputKeys.HasChartData, true)
                .WithOutput(NodeUiOutputKeys.ChartArtifactKey, r.Key)
                .WithOutput(NodeUiOutputKeys.ChartXAxisLabel, chart.BottomAxisLabel ?? string.Empty)
                .WithOutput(NodeUiOutputKeys.ChartXAxisUnit, chart.BottomAxisUnit ?? string.Empty)
                .WithOutput(NodeUiOutputKeys.ChartYAxisLabel, chart.LeftAxisLabel ?? string.Empty)
                .WithOutput(NodeUiOutputKeys.ChartYAxisUnit, chart.LeftAxisUnit ?? string.Empty);
        }

        /// <summary>
        /// 发布带多 Series 的合并 Chart：将多个 (SeriesName, Chart) 组装成单个
        /// <see cref="ChartDisplayPayload"/> 并写入 <see cref="ITestDataBus"/>。
        /// 单条目时退化为普通 <see cref="SuccessWithChart"/>。
        /// </summary>
        public static ExecutionResult SuccessWithMultiChart(
            NodeContext context,
            string producerNodeId,
            string artifactName,
            IReadOnlyList<(string SeriesName, ChartDisplayPayload Chart)> charts,
            ChartLayoutMode? layoutOverride = null,
            string? tag = null,
            string message = "完成")
        {
            if (charts == null || charts.Count == 0)
                return ExecutionResult.Failed("无可发布的图表数据。");

            if (charts.Count == 1)
                return SuccessWithChart(context, producerNodeId, artifactName, charts[0].Chart, tag, message);

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

            return SuccessWithChart(context, producerNodeId, artifactName, merged, tag, message);
        }

        public static ExecutionResult SuccessWithChartAndScalars(
            NodeContext context,
            string producerNodeId,
            string artifactName,
            ChartDisplayPayload chart,
            IReadOnlyList<(string Name, double Value, string Unit)> scalars,
            string? tag = null,
            string message = "完成")
        {
            var result = SuccessWithChart(context, producerNodeId, artifactName, chart, tag, message);
            var bus = context.GetDataBus();
            if (bus == null)
                return result;

            foreach (var (name, value, unit) in scalars)
            {
                bus.PublishScalar(producerNodeId, name, value, unit: unit, tag: tag ?? artifactName);
            }

            return result;
        }

        /// <summary>
        /// 多 Series 图表 + 标量值。
        /// </summary>
        public static ExecutionResult SuccessWithMultiChartAndScalars(
            NodeContext context,
            string producerNodeId,
            string artifactName,
            IReadOnlyList<(string SeriesName, ChartDisplayPayload Chart)> charts,
            IReadOnlyList<(string Name, double Value, string Unit)> scalars,
            ChartLayoutMode? layoutOverride = null,
            string? tag = null,
            string message = "完成")
        {
            var result = SuccessWithMultiChart(context, producerNodeId, artifactName, charts, layoutOverride, tag, message);
            var bus = context.GetDataBus();
            if (bus == null)
                return result;

            foreach (var (name, value, unit) in scalars)
                bus.PublishScalar(producerNodeId, name, value, unit: unit, tag: tag ?? artifactName);

            return result;
        }

        public static ExecutionResult SuccessScalarsOnly(
            NodeContext context,
            string producerNodeId,
            IReadOnlyList<(string Name, double Value, string Unit)> scalars,
            string tag,
            string message = "完成")
        {
            var bus = context.GetDataBus();
            if (bus == null)
                return ExecutionResult.Failed("测试数据总线不可用");

            foreach (var (name, value, unit) in scalars)
                bus.PublishScalar(producerNodeId, name, value, unit: unit, tag: tag);

            return ExecutionResult.Successful(message)
                .WithOutput(NodeUiOutputKeys.HasChartData, false);
        }
    }
}
