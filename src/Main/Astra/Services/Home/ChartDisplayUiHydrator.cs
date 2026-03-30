using Astra.Core.Nodes.Models;
using Astra.Core.Nodes.Ui;
using Astra.UI.Abstractions.Nodes;
using NVHDataBridge.Models;

namespace Astra.Services.Home
{
    /// <summary>
    /// 节点完成时：将内联 <see cref="NodeUiOutputKeys.ChartPayloadSnapshot"/>、Raw 中的 <see cref="ChartDisplayPayload"/>，
    /// 或 NVH 波形，写入图表缓存。
    /// </summary>
    public sealed class ChartDisplayUiHydrator : INodeExecutionUiHydrator
    {
        private readonly IChartDisplayDataCache _cache;

        public ChartDisplayUiHydrator(IChartDisplayDataCache cache)
        {
            _cache = cache;
        }

        public void OnNodeExecutionCompleted(NodeContext? context, string nodeId, ExecutionResult? result)
        {
            if (context == null || string.IsNullOrWhiteSpace(nodeId) || result?.OutputData == null)
            {
                return;
            }

            if (result.OutputData.TryGetValue(NodeUiOutputKeys.ChartPayloadSnapshot, out var snap) &&
                snap is ChartDisplayPayload inlineSnapshot)
            {
                _cache.SetPayload(nodeId, ChartDisplayPayload.MergeAxisMetadata(inlineSnapshot, result.OutputData));
                return;
            }

            if (!result.OutputData.TryGetValue(NodeUiOutputKeys.HasChartData, out var hasObj) ||
                hasObj is not bool hasChart ||
                !hasChart)
            {
                return;
            }

            if (!result.OutputData.TryGetValue(NodeUiOutputKeys.ChartArtifactKey, out var keyObj) ||
                keyObj is not string artifactKey ||
                string.IsNullOrWhiteSpace(artifactKey))
            {
                return;
            }

            if (!context.TryGetArtifact(artifactKey.Trim(), out var raw) || raw == null)
            {
                return;
            }

            if (raw is ChartDisplayPayload payloadFromStore)
            {
                _cache.SetPayload(nodeId, ChartDisplayPayload.MergeAxisMetadata(payloadFromStore, result.OutputData));
                return;
            }

            if (raw is not NvhMemoryFile file)
            {
                return;
            }

            if (!NvhMemoryFileSampleExtractor.TryExtractAsDoubleArray(file, "Signal", null, out var samples) ||
                samples.Length == 0)
            {
                return;
            }

            var nvhPayload = new ChartDisplayPayload
            {
                Kind = ChartPayloadKind.Signal1D,
                SignalY = samples,
                SamplePeriod = 1.0,
                BottomAxisLabel = "样本",
                LeftAxisLabel = "数值"
            };
            _cache.SetPayload(nodeId, ChartDisplayPayload.MergeAxisMetadata(nvhPayload, result.OutputData));
        }
    }
}
