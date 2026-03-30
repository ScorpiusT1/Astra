using Astra.Core.Data;
using Astra.Core.Nodes.Models;
using Astra.UI.Abstractions.Nodes;

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
    }
}
