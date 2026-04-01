using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Astra.Core.Nodes.Models
{
    /// <summary>
    /// 静态注册表：按节点 ID 缓存上游 <see cref="IDesignTimeDataSourceInfo"/> 列表。
    /// 解决属性编辑器使用克隆节点（不同实例）导致实例级 _upstreamSources 无法共享的问题。
    /// 同时维护一份序列化安全的通道选项缓存，确保重启后通道下拉框仍有数据。
    /// </summary>
    public static class DesignTimeUpstreamRegistry
    {
        private static readonly ConcurrentDictionary<string, List<IDesignTimeDataSourceInfo>> _sources = new();
        private static readonly ConcurrentDictionary<string, List<string>> _channelOptionsCache = new();

        /// <summary>当某个节点的上游数据源变更时触发，参数为节点 ID。</summary>
        public static event Action<string>? SourcesChanged;

        public static void SetSources(string nodeId, IEnumerable<IDesignTimeDataSourceInfo> sources)
        {
            if (string.IsNullOrEmpty(nodeId)) return;
            _sources[nodeId] = sources.ToList();
            SourcesChanged?.Invoke(nodeId);
        }

        public static void ClearSources(string nodeId)
        {
            if (string.IsNullOrEmpty(nodeId)) return;
            _sources.TryRemove(nodeId, out _);
            SourcesChanged?.Invoke(nodeId);
        }

        public static IEnumerable<string> GetDeviceNames(string nodeId)
        {
            if (string.IsNullOrEmpty(nodeId) || !_sources.TryGetValue(nodeId, out var list))
                return Enumerable.Empty<string>();
            return list.SelectMany(s => s.GetAvailableDeviceDisplayNames()).Distinct().ToList();
        }

        public static IEnumerable<string> GetChannelNames(string nodeId, IEnumerable<string> deviceNames)
        {
            if (string.IsNullOrEmpty(nodeId) || !_sources.TryGetValue(nodeId, out var list))
                return Enumerable.Empty<string>();
            return deviceNames
                .SelectMany(d => list.SelectMany(s => s.GetAvailableChannelNames(d)).Select(ch => $"{d}/{ch}"))
                .Distinct()
                .ToList();
        }

        public static IEnumerable<string> GetChannelNamesForDevice(string nodeId, string deviceName)
        {
            if (string.IsNullOrEmpty(nodeId) || string.IsNullOrEmpty(deviceName) || !_sources.TryGetValue(nodeId, out var list))
                return Enumerable.Empty<string>();
            return list.SelectMany(s => s.GetAvailableChannelNames(deviceName)).Distinct().ToList();
        }

        // ===== 通道选项持久化缓存 =====

        /// <summary>
        /// 将已解析的通道选项存入静态缓存（供克隆节点、反序列化后使用）。
        /// </summary>
        public static void CacheChannelOptions(string nodeId, List<string> options)
        {
            if (string.IsNullOrEmpty(nodeId) || options == null) return;
            _channelOptionsCache[nodeId] = options;
        }

        /// <summary>
        /// 从静态缓存获取通道选项；缓存来自 <see cref="CacheChannelOptions"/> 或节点 [OnDeserialized]。
        /// </summary>
        public static List<string> GetCachedChannelOptions(string nodeId)
        {
            if (!string.IsNullOrEmpty(nodeId) && _channelOptionsCache.TryGetValue(nodeId, out var cached))
                return cached;
            return new List<string>();
        }

        public static void ClearChannelOptionsCache(string nodeId)
        {
            if (!string.IsNullOrEmpty(nodeId))
                _channelOptionsCache.TryRemove(nodeId, out _);
        }
    }
}
