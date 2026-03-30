using System.Collections.Generic;
using Astra.Core.Nodes.Models;

namespace Astra.Core.Data
{
    /// <summary>
    /// 便捷扩展方法：让节点开发者一行代码完成各种类型数据的发布与消费。
    /// </summary>
    public static class TestDataBusExtensions
    {
        /// <summary>发布原始采集数据（大对象走零拷贝路径）。</summary>
        public static DataArtifactReference PublishRawData(
            this ITestDataBus bus,
            string producerNodeId,
            string artifactName,
            object rawData,
            string? displayName = null,
            string? deviceId = null)
        {
            return bus.Publish(new DataEntry
            {
                ProducerNodeId = producerNodeId,
                Category = DataArtifactCategory.Raw,
                ArtifactName = artifactName,
                Data = rawData,
                DisplayName = displayName ?? artifactName,
                Tag = deviceId,
                Preview = deviceId != null
                    ? new Dictionary<string, object> { ["DeviceId"] = deviceId }
                    : null
            });
        }

        /// <summary>发布算法结果（频谱、阶次谱、ChartDisplayPayload 等）。</summary>
        public static DataArtifactReference PublishAlgorithmResult(
            this ITestDataBus bus,
            string producerNodeId,
            string algorithmName,
            object result,
            string? tag = null,
            Dictionary<string, object>? parameters = null)
        {
            return bus.Publish(new DataEntry
            {
                ProducerNodeId = producerNodeId,
                Category = DataArtifactCategory.Algorithm,
                ArtifactName = algorithmName,
                Data = result,
                DisplayName = algorithmName,
                Tag = tag,
                Preview = parameters
            });
        }

        /// <summary>发布标量值（温度、转速、RMS、Pass/Fail 数值等）。</summary>
        public static DataArtifactReference PublishScalar<T>(
            this ITestDataBus bus,
            string producerNodeId,
            string name,
            T value,
            string? unit = null,
            string? tag = null) where T : struct
        {
            return bus.Publish(new DataEntry
            {
                ProducerNodeId = producerNodeId,
                Category = DataArtifactCategory.Scalar,
                ArtifactName = name,
                Data = value,
                DisplayName = name,
                Tag = tag,
                Preview = new Dictionary<string, object>
                {
                    ["Value"] = value!,
                    ["Unit"] = unit ?? string.Empty,
                    ["Type"] = typeof(T).Name
                }
            });
        }

        /// <summary>发布文本数据（日志、JSON 报告、备注等）。</summary>
        public static DataArtifactReference PublishText(
            this ITestDataBus bus,
            string producerNodeId,
            string name,
            string text,
            string contentType = "text/plain",
            string? tag = null)
        {
            return bus.Publish(new DataEntry
            {
                ProducerNodeId = producerNodeId,
                Category = DataArtifactCategory.Text,
                ArtifactName = name,
                Data = text,
                DisplayName = name,
                Tag = tag,
                Preview = new Dictionary<string, object>
                {
                    ["ContentType"] = contentType,
                    ["Length"] = text?.Length ?? 0
                }
            });
        }

        /// <summary>批量消费指定 Category 下的所有数据。</summary>
        public static IReadOnlyList<T> ConsumeAll<T>(
            this ITestDataBus bus,
            string consumerNodeId,
            DataArtifactCategory category,
            string? tag = null)
        {
            var refs = bus.Query(category: category, tag: tag);
            var results = new List<T>();
            foreach (var r in refs)
            {
                if (bus.TryGet<T>(r.Key, out var data))
                {
                    bus.MarkConsumed(r.Key, consumerNodeId);
                    results.Add(data);
                }
            }

            return results;
        }
    }
}
