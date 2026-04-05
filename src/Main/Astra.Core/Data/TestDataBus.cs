using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Astra.Core.Nodes.Models;
using Astra.Core.Reporting;

namespace Astra.Core.Data
{
    /// <summary>
    /// 默认 <see cref="ITestDataBus"/> 实现。
    /// 底层委托给同一个 <see cref="IRawDataStore"/>，仅在上层维护轻量注册表与血缘字典。
    /// </summary>
    public sealed class TestDataBus : ITestDataBus
    {
        private readonly IRawDataStore _store;
        private readonly ConcurrentDictionary<string, DataArtifactReference> _registry = new(StringComparer.Ordinal);
        private readonly ConcurrentDictionary<string, ConcurrentBag<string>> _lineage = new(StringComparer.Ordinal);

        public string ExecutionId { get; }

        public TestDataBus(string executionId, IRawDataStore store)
        {
            ExecutionId = executionId ?? throw new ArgumentNullException(nameof(executionId));
            _store = store ?? throw new ArgumentNullException(nameof(store));
        }

        public DataArtifactReference Publish(DataEntry entry)
        {
            if (entry == null) throw new ArgumentNullException(nameof(entry));

            var key = BuildKey(entry);

            if (entry.Data != null)
            {
                _store.Set(key, entry.Data);
            }

            var preview = entry.Preview != null
                ? new Dictionary<string, object>(entry.Preview)
                : new Dictionary<string, object>();

            if (!string.IsNullOrEmpty(entry.Tag))
                preview["__Tag"] = entry.Tag;

            preview["__ProducerNodeId"] = entry.ProducerNodeId ?? string.Empty;

            if (!preview.ContainsKey(ReportIncludeKeys.IncludeInReport))
                preview[ReportIncludeKeys.IncludeInReport] = entry.IncludeInTestReport;

            var reference = new DataArtifactReference
            {
                Key = key,
                Category = entry.Category,
                DataType = entry.Data?.GetType().FullName ?? "null",
                DisplayName = entry.DisplayName ?? entry.ArtifactName,
                Description = entry.Description,
                Preview = preview,
                CreatedAt = DateTime.UtcNow
            };

            _registry[key] = reference;
            return reference;
        }

        public bool TryGet<T>(string artifactKey, out T data)
        {
            data = default!;
            if (string.IsNullOrEmpty(artifactKey))
                return false;

            if (!_store.TryGet(artifactKey, out var raw))
                return false;

            if (raw is T typed)
            {
                data = typed;
                return true;
            }

            return false;
        }

        public IReadOnlyList<DataArtifactReference> Query(
            DataArtifactCategory? category = null,
            string? producerNodeId = null,
            string? tag = null)
        {
            var q = _registry.Values.AsEnumerable();

            if (category.HasValue)
                q = q.Where(r => r.Category == category.Value);

            if (!string.IsNullOrEmpty(producerNodeId))
                q = q.Where(r => GetPreviewString(r, "__ProducerNodeId") == producerNodeId);

            if (!string.IsNullOrEmpty(tag))
                q = q.Where(r => GetPreviewString(r, "__Tag") == tag);

            return q.OrderBy(r => r.CreatedAt).ToList();
        }

        public DataArtifactReference? FindLatest(DataArtifactCategory category, string? tag = null)
        {
            var list = Query(category, tag: tag);
            return list.Count > 0 ? list[list.Count - 1] : null;
        }

        public void MarkConsumed(string artifactKey, string consumerNodeId)
        {
            if (string.IsNullOrEmpty(artifactKey) || string.IsNullOrEmpty(consumerNodeId))
                return;

            var bag = _lineage.GetOrAdd(artifactKey, _ => new ConcurrentBag<string>());
            bag.Add(consumerNodeId);
        }

        public DataPipelineSnapshot CreateSnapshot()
        {
            return new DataPipelineSnapshot
            {
                ExecutionId = ExecutionId,
                Artifacts = _registry.Values.OrderBy(r => r.CreatedAt).ToList(),
                Lineage = _lineage.ToDictionary(
                    kv => kv.Key,
                    kv => (IReadOnlyList<string>)kv.Value.Distinct().ToList(),
                    StringComparer.Ordinal),
                CreatedAt = DateTime.UtcNow
            };
        }

        /// <inheritdoc />
        public void Clear()
        {
            _registry.Clear();
            _lineage.Clear();
            _store.RemoveByPrefix($"{ExecutionId}:");
        }

        private string BuildKey(DataEntry entry)
        {
            var safeNodeId = string.IsNullOrWhiteSpace(entry.ProducerNodeId) ? "unknown-node" : entry.ProducerNodeId;
            var safeName = string.IsNullOrWhiteSpace(entry.ArtifactName) ? "artifact" : entry.ArtifactName;
            return $"{ExecutionId}:{safeNodeId}:{entry.Category}:{safeName}";
        }

        private static string? GetPreviewString(DataArtifactReference r, string key)
        {
            if (r.Preview != null && r.Preview.TryGetValue(key, out var v))
                return v?.ToString();
            return null;
        }
    }
}
