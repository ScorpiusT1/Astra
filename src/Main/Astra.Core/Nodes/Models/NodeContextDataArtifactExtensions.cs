using System;
using System.Collections.Generic;

namespace Astra.Core.Nodes.Models
{
    /// <summary>
    /// 节点上下文数据产物访问扩展方法。
    /// </summary>
    public static class NodeContextDataArtifactExtensions
    {
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

        public static DataArtifactReference StoreArtifact(
            this NodeContext context,
            string nodeId,
            DataArtifactCategory category,
            string artifactName,
            object data,
            string displayName = null,
            string description = null,
            long? sizeBytes = null,
            Dictionary<string, object> preview = null)
        {
            var store = context.GetRawDataStore();
            if (store == null || data == null)
            {
                return null;
            }

            var key = context.BuildArtifactKey(nodeId, category, artifactName);
            store.Set(key, data);

            return new DataArtifactReference
            {
                Key = key,
                Category = category,
                DataType = data.GetType().FullName,
                DisplayName = displayName ?? artifactName,
                Description = description,
                SizeBytes = sizeBytes,
                Preview = preview ?? new Dictionary<string, object>(),
                CreatedAt = DateTime.UtcNow
            };
        }
    }
}
