using Astra.Core.Nodes.Models;
using Astra.Plugins.DataAcquisition.Nodes;
using Astra.Plugins.DataAcquisition.Providers;
using System.Linq;

namespace Astra.Plugins.AudioPlayer
{
    /// <summary>
    /// 与 Limits 插件中 <c>LimitCurveArtifactResolver</c> 相同规则：按采集卡显示名找到上游多采集节点并拼出 Raw 工件键。
    /// </summary>
    internal static class AcquisitionRawArtifactHelper
    {
        public const string NvhSignalGroupName = "Signal";

        public static bool TryResolveRawArtifactKey(
            NodeContext context,
            string playbackNodeId,
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
                string.Equals(deviceName, AudioPlayerDesignTimeOptions.UnselectedLabel, StringComparison.Ordinal))
            {
                error = "请选择采集卡";
                return false;
            }

            if (!DataAcquisitionCardProvider.TryGetDeviceIdByDisplayName(deviceName, out var deviceId))
            {
                error = $"找不到采集卡设备: {deviceName}";
                return false;
            }

            if (!TryFindUpstreamMultiDataAcquisitionNodeId(wf, playbackNodeId, deviceName, out var mdaqNodeId))
            {
                error = $"上游未找到包含采集卡「{deviceName}」的多采集节点，请将本节点接在多采集节点之后并确认多采集节点中已勾选该卡";
                return false;
            }

            artifactKey = context.BuildArtifactKey(mdaqNodeId, DataArtifactCategory.Raw, $"{deviceId}:raw");
            return true;
        }

        private static bool TryFindUpstreamMultiDataAcquisitionNodeId(
            WorkFlowNode workflow,
            string targetNodeId,
            string deviceDisplayName,
            out string mdaqNodeId)
        {
            mdaqNodeId = string.Empty;
            var visited = new HashSet<string>(StringComparer.Ordinal);
            var queue = new Queue<string>();

            foreach (var c in workflow.Connections ?? Enumerable.Empty<Connection>())
            {
                if (c.TargetNodeId == targetNodeId)
                {
                    queue.Enqueue(c.SourceNodeId);
                }
            }

            while (queue.Count > 0)
            {
                var nid = queue.Dequeue();
                if (!visited.Add(nid))
                {
                    continue;
                }

                var node = workflow.Nodes?.FirstOrDefault(n => n.Id == nid);
                if (node is MultiDataAcquisitionNode mdq)
                {
                    if (mdq.DataAcquisitionDeviceNames != null &&
                        mdq.DataAcquisitionDeviceNames.Any(x =>
                            string.Equals(x?.Trim(), deviceDisplayName, StringComparison.OrdinalIgnoreCase)))
                    {
                        mdaqNodeId = mdq.Id;
                        return true;
                    }
                }

                foreach (var c in workflow.Connections ?? Enumerable.Empty<Connection>())
                {
                    if (c.TargetNodeId == nid)
                    {
                        queue.Enqueue(c.SourceNodeId);
                    }
                }
            }

            return false;
        }
    }
}
