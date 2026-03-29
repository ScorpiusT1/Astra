using Astra.Core.Nodes.Models;
using System;
using System.Collections.Concurrent;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace Astra.Engine.Execution.WorkFlowEngine.Management
{
    /// <summary>
    /// 基于内存的原始数据存储实现（支持TTL和容量上限）。
    /// </summary>
    public class InMemoryRawDataStore : IRawDataStore
    {
        private readonly ConcurrentDictionary<string, RawDataEntry> _data = new ConcurrentDictionary<string, RawDataEntry>();
        private readonly ConcurrentQueue<string> _insertionOrder = new ConcurrentQueue<string>();
        private readonly TimeSpan _defaultTtl;
        private readonly int _maxItems;
        private readonly long _maxBytes;
        private int _cleanupGuard;
        private long _currentBytes;
        private long _hitCount;
        private long _missCount;

        public InMemoryRawDataStore(TimeSpan? defaultTtl = null, int maxItems = 2000, long maxBytes = 128 * 1024 * 1024)
        {
            _defaultTtl = defaultTtl ?? TimeSpan.FromMinutes(10);
            _maxItems = maxItems > 0 ? maxItems : 2000;
            _maxBytes = maxBytes > 0 ? maxBytes : 128 * 1024 * 1024;
        }

        public void Set(string key, object value)
        {
            Set(key, value, null);
        }

        /// <summary>
        /// 写入原始数据（可指定单条TTL）。
        /// </summary>
        public void Set(string key, object value, TimeSpan? ttl)
        {
            var effectiveTtl = ttl ?? _defaultTtl;
            var now = DateTimeOffset.UtcNow;
            var estimatedBytes = EstimateSizeBytes(value);
            var entry = new RawDataEntry(
                value,
                now,
                effectiveTtl > TimeSpan.Zero ? now.Add(effectiveTtl) : DateTimeOffset.MaxValue,
                estimatedBytes);

            if (_data.TryGetValue(key, out var oldEntry))
            {
                _data[key] = entry;
                Interlocked.Add(ref _currentBytes, entry.EstimatedSizeBytes - oldEntry.EstimatedSizeBytes);
            }
            else
            {
                _data[key] = entry;
                Interlocked.Add(ref _currentBytes, entry.EstimatedSizeBytes);
            }

            _insertionOrder.Enqueue(key);
            CleanupIfNeeded();
        }

        public bool TryGet(string key, out object value)
        {
            value = null;
            if (!_data.TryGetValue(key, out var entry))
            {
                Interlocked.Increment(ref _missCount);
                return false;
            }

            if (entry.IsExpired(DateTimeOffset.UtcNow))
            {
                TryRemoveAndAdjust(key);
                Interlocked.Increment(ref _missCount);
                return false;
            }

            value = entry.Value;
            Interlocked.Increment(ref _hitCount);
            return true;
        }

        public bool Remove(string key)
        {
            return TryRemoveAndAdjust(key);
        }

        public int RemoveByPrefix(string keyPrefix)
        {
            if (string.IsNullOrWhiteSpace(keyPrefix))
            {
                return 0;
            }

            int removed = 0;
            foreach (var key in _data.Keys)
            {
                if (key != null && key.StartsWith(keyPrefix, StringComparison.Ordinal) && TryRemoveAndAdjust(key))
                {
                    removed++;
                }
            }

            return removed;
        }

        public RawDataStoreStats GetStats()
        {
            return new RawDataStoreStats
            {
                ItemCount = _data.Count,
                EstimatedBytes = Interlocked.Read(ref _currentBytes),
                MaxBytes = _maxBytes,
                MaxItems = _maxItems,
                HitCount = Interlocked.Read(ref _hitCount),
                MissCount = Interlocked.Read(ref _missCount)
            };
        }

        public bool TrySnapshotByPrefix(string prefix, out IReadOnlyList<KeyValuePair<string, object>> items)
        {
            items = Array.Empty<KeyValuePair<string, object>>();
            if (string.IsNullOrWhiteSpace(prefix))
            {
                return false;
            }

            var now = DateTimeOffset.UtcNow;
            var list = new List<KeyValuePair<string, object>>();
            foreach (var kvp in _data)
            {
                if (kvp.Key == null || !kvp.Key.StartsWith(prefix, StringComparison.Ordinal))
                {
                    continue;
                }

                if (kvp.Value.IsExpired(now))
                {
                    continue;
                }

                list.Add(new KeyValuePair<string, object>(kvp.Key, kvp.Value.Value));
            }

            items = list;
            return true;
        }

        private void CleanupIfNeeded()
        {
            if (_data.Count <= _maxItems && Interlocked.Read(ref _currentBytes) <= _maxBytes)
            {
                TryCleanupExpiredFast();
                return;
            }

            if (Interlocked.Exchange(ref _cleanupGuard, 1) == 1)
            {
                return;
            }

            try
            {
                var now = DateTimeOffset.UtcNow;

                // 先清过期项
                foreach (var kvp in _data)
                {
                    if (kvp.Value.IsExpired(now))
                    {
                        TryRemoveAndAdjust(kvp.Key);
                    }
                }

                // 再按先入先出裁剪到容量上限（条目数和字节数双阈值）
                while ((_data.Count > _maxItems || Interlocked.Read(ref _currentBytes) > _maxBytes)
                       && _insertionOrder.TryDequeue(out var oldestKey))
                {
                    TryRemoveAndAdjust(oldestKey);
                }
            }
            finally
            {
                Interlocked.Exchange(ref _cleanupGuard, 0);
            }
        }

        private void TryCleanupExpiredFast()
        {
            if (Interlocked.Exchange(ref _cleanupGuard, 1) == 1)
            {
                return;
            }

            try
            {
                var now = DateTimeOffset.UtcNow;
                foreach (var kvp in _data)
                {
                    if (kvp.Value.IsExpired(now))
                    {
                        TryRemoveAndAdjust(kvp.Key);
                    }
                }
            }
            finally
            {
                Interlocked.Exchange(ref _cleanupGuard, 0);
            }
        }

        private sealed class RawDataEntry
        {
            public RawDataEntry(object value, DateTimeOffset createdAt, DateTimeOffset expiresAt, long estimatedSizeBytes)
            {
                Value = value;
                CreatedAt = createdAt;
                ExpiresAt = expiresAt;
                EstimatedSizeBytes = estimatedSizeBytes;
            }

            public object Value { get; }
            public DateTimeOffset CreatedAt { get; }
            public DateTimeOffset ExpiresAt { get; }
            public long EstimatedSizeBytes { get; }

            public bool IsExpired(DateTimeOffset now) => now >= ExpiresAt;
        }

        private bool TryRemoveAndAdjust(string key)
        {
            if (_data.TryRemove(key, out var removed))
            {
                Interlocked.Add(ref _currentBytes, -removed.EstimatedSizeBytes);
                return true;
            }

            return false;
        }

        private static long EstimateSizeBytes(object value)
        {
            if (value == null) return 0;

            switch (value)
            {
                case byte[] bytes:
                    return bytes.LongLength;
                case string str:
                    return Encoding.UTF8.GetByteCount(str);
                case Array array:
                    return EstimateArraySize(array);
                case bool _:
                    return 1;
                case char _:
                    return 2;
                case short _:
                case ushort _:
                    return 2;
                case int _:
                case uint _:
                case float _:
                    return 4;
                case long _:
                case ulong _:
                case double _:
                case DateTime _:
                case DateTimeOffset _:
                    return 8;
                case decimal _:
                case Guid _:
                    return 16;
                case RawDataReference _:
                    return 128;
                case DataArtifactReference _:
                    return 192;
                case IDictionary dict:
                    return 128 + dict.Count * 64;
                case ICollection collection:
                    return 64 + collection.Count * 32;
                default:
                    // 兜底估算，避免复杂对象无法计量
                    return 1024;
            }
        }

        private static long EstimateArraySize(Array array)
        {
            var elementType = array.GetType().GetElementType();
            if (elementType == null) return 1024;

            long elementSize;
            if (elementType == typeof(byte)) elementSize = 1;
            else if (elementType == typeof(short) || elementType == typeof(ushort)) elementSize = 2;
            else if (elementType == typeof(int) || elementType == typeof(uint) || elementType == typeof(float)) elementSize = 4;
            else if (elementType == typeof(long) || elementType == typeof(ulong) || elementType == typeof(double)) elementSize = 8;
            else if (elementType == typeof(decimal) || elementType == typeof(Guid)) elementSize = 16;
            else elementSize = 16;

            try
            {
                return checked(array.LongLength * elementSize);
            }
            catch
            {
                return long.MaxValue / 2;
            }
        }
    }
}
