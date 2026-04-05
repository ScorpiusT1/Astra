using Astra.Core.Constants;
using Astra.Core.Data;
using Astra.Core.Nodes.Models;

namespace Astra.Plugins.Limits.Helpers
{
    /// <summary>
    /// 根据采集卡显示名解析 Raw 工件键（用于曲线逐点卡控等必须读时域样本的场景）。BFS 委托给 <see cref="RawDataPipelineResolver"/>。
    /// 主页/报告中的图表展示在可能时改由 <see cref="LimitNodeShared"/> 优先转发上游 <see cref="NodeUiOutputKeys.ChartArtifactKey"/>（如算法节点的 <see cref="ChartDisplayPayload"/>）。
    /// </summary>
    internal static class LimitCurveArtifactResolver
    {
        public const string NvhSignalGroupName = AstraSharedConstants.DataGroups.Signal;

        public static bool TryResolveRawArtifactKey(
            NodeContext context,
            string limitsNodeId,
            string? configuredDeviceDisplayName,
            out string artifactKey,
            out string error)
        {
            artifactKey = string.Empty;
            error = string.Empty;

            var wf = context.ParentWorkFlow;
            if (wf == null)
            {
                error = "缺少父工作流上下文，无法解析曲线数据";
                return false;
            }

            var deviceName = configuredDeviceDisplayName?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(deviceName) ||
                string.Equals(deviceName, LimitsDesignTimeOptions.UnselectedLabel, StringComparison.Ordinal))
            {
                error = "无法确定采集卡，请从下拉选择「设备」或「设备/通道」";
                return false;
            }

            if (!AcquisitionDeviceCatalog.TryGetDeviceIdByDisplayName(deviceName, out var deviceId))
            {
                error = $"找不到采集卡设备: {deviceName}";
                return false;
            }

            if (!RawDataPipelineResolver.TryFindUpstreamRawProducerNodeId(wf, limitsNodeId, deviceName, out var producerNodeId))
            {
                error = $"上游未找到采集卡「{deviceName}」对应的 Raw（多采集、数字滤波或文件导入），请检查连线";
                return false;
            }

            artifactKey = context.BuildArtifactKey(producerNodeId, DataArtifactCategory.Raw, $"{deviceId}:raw");
            return true;
        }
    }
}
