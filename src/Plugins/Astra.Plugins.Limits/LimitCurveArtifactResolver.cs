using Astra.Core.Nodes.Models;
using Astra.Core.Constants;
using Astra.Plugins.DataAcquisition.Nodes;
using Astra.Plugins.DataAcquisition.Providers;
using System.Linq;

namespace Astra.Plugins.Limits
{
    /// <summary>
    /// 根据采集卡显示名 + 上游多采集节点，解析 Raw 中 NVH 产物的键（与 MultiDataAcquisitionNode 写入规则一致）。
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
                error = "请选择采集卡";
                return false;
            }

            if (!DataAcquisitionCardProvider.TryGetDeviceIdByDisplayName(deviceName, out var deviceId))
            {
                error = $"找不到采集卡设备: {deviceName}";
                return false;
            }

            if (!TryFindUpstreamMultiDataAcquisitionNodeId(wf, limitsNodeId, deviceName, out var mdaqNodeId))
            {
                error = $"上游未找到包含采集卡「{deviceName}」的多采集节点，请检查连线与多采集节点中的采集卡列表";
                return false;
            }

            artifactKey = context.BuildArtifactKey(mdaqNodeId, DataArtifactCategory.Raw, $"{deviceId}:raw");
            return true;
        }

        private static bool TryFindUpstreamMultiDataAcquisitionNodeId(
            WorkFlowNode workflow,
            string limitNodeId,
            string deviceDisplayName,
            out string mdaqNodeId)
        {
            mdaqNodeId = string.Empty;
            var visited = new HashSet<string>(StringComparer.Ordinal);
            var queue = new Queue<string>();

            foreach (var c in workflow.Connections ?? Enumerable.Empty<Connection>())
            {
                if (c.TargetNodeId == limitNodeId)
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
