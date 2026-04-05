using Astra.Core.Constants;
using Astra.Core.Data;
using Astra.Core.Nodes.Models;

namespace Astra.Plugins.DataProcessing.Helpers
{
    /// <summary>
    /// 解析上游 Raw 生产者。BFS 逻辑委托给 <see cref="RawDataPipelineResolver"/>。
    /// </summary>
    internal static class DataProcessingRawArtifactHelper
    {
        public const string NvhSignalGroupName = AstraSharedConstants.DataGroups.Signal;

        public static bool TryResolveRawArtifactKey(
            NodeContext context,
            string consumerNodeId,
            string? configuredDeviceDisplayName,
            out string artifactKey,
            out string error)
        {
            artifactKey = string.Empty;
            error = string.Empty;

            var wf = context.ParentWorkFlow;
            if (wf == null)
            {
                error = "缺少父工作流上下文，无法解析采集数据";
                return false;
            }

            var deviceName = configuredDeviceDisplayName?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(deviceName) ||
                string.Equals(deviceName, DataProcessingDesignTimeOptions.UnselectedLabel, StringComparison.Ordinal))
            {
                error = "请选择采集卡";
                return false;
            }

            if (!AcquisitionDeviceCatalog.TryGetDeviceIdByDisplayName(deviceName, out var deviceId))
            {
                error = $"找不到采集卡设备: {deviceName}";
                return false;
            }

            if (!RawDataPipelineResolver.TryFindUpstreamRawProducerNodeId(wf, consumerNodeId, deviceName, out var producerNodeId))
            {
                error = $"上游未找到采集卡「{deviceName}」对应的 Raw（多采集、数字滤波或文件导入节点）";
                return false;
            }

            artifactKey = context.BuildArtifactKey(producerNodeId, DataArtifactCategory.Raw, $"{deviceId}:raw");
            return true;
        }
    }
}
