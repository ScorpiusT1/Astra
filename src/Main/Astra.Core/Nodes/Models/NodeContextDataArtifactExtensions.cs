using System;
using System.Collections.Generic;
using Astra.Core.Data;

namespace Astra.Core.Nodes.Models
{
    /// <summary>
    /// 节点上下文数据产物访问扩展方法。
    /// 统一委托给 <see cref="ITestDataBus"/>，不再直接操作底层 <see cref="IRawDataStore"/>。
    /// </summary>
    public static class NodeContextDataArtifactExtensions
    {
        /// <summary>
        /// 构建产物 Key（与 TestDataBus.BuildKey 格式一致）。
        /// 用于外部需要预测产物 Key 的场景（如 AcquisitionRawArtifactHelper 解析上游产物）。
        /// </summary>
        public static string BuildArtifactKey(
            this NodeContext context,
            string nodeId,
            DataArtifactCategory category,
            string artifactName)
        {
            var executionId = context?.ExecutionId
                ?? context?.GetMetadata<string>("ExecutionId", null)
                ?? "unknown-execution";

            var safeNodeId = string.IsNullOrWhiteSpace(nodeId) ? "unknown-node" : nodeId;
            var safeArtifactName = string.IsNullOrWhiteSpace(artifactName) ? "artifact" : artifactName;

            return $"{executionId}:{safeNodeId}:{category}:{safeArtifactName}";
        }

        /// <summary>
        /// 存储数据产物（写入 TestDataBus）。
        /// </summary>
        public static DataArtifactReference StoreArtifact(
            this NodeContext context,
            string nodeId,
            DataArtifactCategory category,
            string artifactName,
            object data,
            string displayName = null,
            string description = null,
            long? sizeBytes = null,
            Dictionary<string, object> preview = null,
            bool includeInTestReport = true)
        {
            if (data == null) return null;

            var bus = context?.GetDataBus();
            if (bus != null)
            {
                return bus.Publish(new DataEntry
                {
                    ProducerNodeId = nodeId ?? string.Empty,
                    Category = category,
                    ArtifactName = artifactName ?? "artifact",
                    Data = data,
                    DisplayName = displayName ?? artifactName,
                    Description = description,
                    IncludeInTestReport = includeInTestReport,
                    Preview = preview
                });
            }

            return null;
        }

        /// <summary>
        /// 从 TestDataBus 按 Key 读取产物数据。
        /// </summary>
        public static bool TryGetArtifact<T>(this NodeContext context, string artifactKey, out T data)
        {
            data = default;
            var bus = context?.GetDataBus();
            if (bus != null)
                return bus.TryGet(artifactKey, out data);
            return false;
        }

        /// <summary>
        /// 从 TestDataBus 按 Key 读取产物数据（返回 object）。
        /// </summary>
        public static bool TryGetArtifact(this NodeContext context, string artifactKey, out object data)
        {
            return TryGetArtifact<object>(context, artifactKey, out data);
        }
    }
}
