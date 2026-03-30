using Astra.Core.Data;
using Astra.Core.Nodes.Models;
using Astra.UI.Abstractions.Nodes;

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
