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
        private static readonly ConcurrentDictionary<string, List<IDesignTimeScalarOutputProvider>> _scalarUpstreamProviders = new();
        private static readonly ConcurrentDictionary<string, List<string>> _channelOptionsCache = new();

        /// <summary>删除多节点/多边时合并通知，避免每条边触发一轮全图订阅回调导致卡顿。</summary>
        private static readonly object _graphBatchLock = new();
        private static int _graphBatchDepth;
        private static readonly HashSet<string> _pendingSourcesChangedNodeIds = new(StringComparer.Ordinal);
        private static readonly HashSet<string> _pendingUpstreamChannelProducerIds = new(StringComparer.Ordinal);

        private static readonly object _designTimeListenersLock = new();
        private static readonly Dictionary<string, List<OwnSourcesListenerEntry>> _ownSourcesListeners =
            new(StringComparer.Ordinal);
        private static readonly List<WeakDesignTimeListenerEntry> _upstreamChannelListeners = new();
        private static readonly List<WeakDesignTimeListenerEntry> _deviceChannelListeners = new();

        private sealed class OwnSourcesListenerEntry
        {
            public WeakReference<object> Owner = null!;
            public Action<string> Handler = null!;
        }

        private sealed class WeakDesignTimeListenerEntry
        {
            public WeakReference<object> Owner = null!;
            public Action<string> Handler = null!;
        }

        /// <summary>
        /// 仅当 <paramref name="changedNodeId"/> 等于本节点 <see cref="Node.Id"/> 时需要刷新时注册（按 Id 索引，避免每次 Sources 变更遍历全图算法/滤波实例）。
        /// <paramref name="handler"/> 内须通过弱引用持有订阅者，勿直接捕获 <c>this</c>。
        /// </summary>
        public static void RegisterOwnSourcesChanged(
            string listenerNodeId,
            object lifetimeOwner,
            Action<string> handler)
        {
            if (string.IsNullOrEmpty(listenerNodeId) || lifetimeOwner == null) return;
            lock (_designTimeListenersLock)
            {
                PruneDeadOwnSourcesListeners_NoLock(listenerNodeId);
                if (!_ownSourcesListeners.TryGetValue(listenerNodeId, out var list))
                {
                    list = new List<OwnSourcesListenerEntry>();
                    _ownSourcesListeners[listenerNodeId] = list;
                }

                list.Add(new OwnSourcesListenerEntry
                {
                    Owner = new WeakReference<object>(lifetimeOwner),
                    Handler = handler
                });
            }
        }

        /// <summary>注册上游通道选项变更监听；<paramref name="handler"/> 内须用弱引用访问节点。</summary>
        public static void RegisterUpstreamChannelOptionsListener(object lifetimeOwner, Action<string> handler)
        {
            if (lifetimeOwner == null) return;
            lock (_designTimeListenersLock)
            {
                PruneDeadWeakList_NoLock(_upstreamChannelListeners);
                _upstreamChannelListeners.Add(new WeakDesignTimeListenerEntry
                {
                    Owner = new WeakReference<object>(lifetimeOwner),
                    Handler = handler
                });
            }
        }

        /// <summary>注册采集设备显示名通道配置变更监听；<paramref name="handler"/> 内须用弱引用访问节点。</summary>
        public static void RegisterDeviceChannelOptionsListener(object lifetimeOwner, Action<string> handler)
        {
            if (lifetimeOwner == null) return;
            lock (_designTimeListenersLock)
            {
                PruneDeadWeakList_NoLock(_deviceChannelListeners);
                _deviceChannelListeners.Add(new WeakDesignTimeListenerEntry
                {
                    Owner = new WeakReference<object>(lifetimeOwner),
                    Handler = handler
                });
            }
        }

        /// <summary>节点从工作流移除时取消该实例的全部设计期订阅，避免克隆/反复打开属性面板堆积委托。</summary>
        public static void UnregisterDesignTimeListeners(object lifetimeOwner)
        {
            if (lifetimeOwner == null) return;
            lock (_designTimeListenersLock)
            {
                var keysToRemove = new List<string>();
                foreach (var kv in _ownSourcesListeners)
                {
                    kv.Value.RemoveAll(e =>
                        !e.Owner.TryGetTarget(out var t) || ReferenceEquals(t, lifetimeOwner));
                    if (kv.Value.Count == 0)
                        keysToRemove.Add(kv.Key);
                }

                foreach (var k in keysToRemove)
                    _ownSourcesListeners.Remove(k);

                _upstreamChannelListeners.RemoveAll(e =>
                    !e.Owner.TryGetTarget(out var t) || ReferenceEquals(t, lifetimeOwner));
                _deviceChannelListeners.RemoveAll(e =>
                    !e.Owner.TryGetTarget(out var t) || ReferenceEquals(t, lifetimeOwner));
            }
        }

        private static void PruneDeadOwnSourcesListeners_NoLock(string listenerNodeId)
        {
            if (!_ownSourcesListeners.TryGetValue(listenerNodeId, out var list))
                return;
            list.RemoveAll(e => !e.Owner.TryGetTarget(out _));
            if (list.Count == 0)
                _ownSourcesListeners.Remove(listenerNodeId);
        }

        private static void PruneDeadWeakList_NoLock(List<WeakDesignTimeListenerEntry> list) =>
            list.RemoveAll(e => !e.Owner.TryGetTarget(out _));

        private static void RaiseOwnSourcesChanged(string changedNodeId)
        {
            if (string.IsNullOrEmpty(changedNodeId)) return;
            List<Action<string>>? handlers = null;
            lock (_designTimeListenersLock)
            {
                if (!_ownSourcesListeners.TryGetValue(changedNodeId, out var list))
                    return;
                list.RemoveAll(e => !e.Owner.TryGetTarget(out _));
                if (list.Count == 0)
                {
                    _ownSourcesListeners.Remove(changedNodeId);
                    return;
                }

                handlers = new List<Action<string>>(list.Count);
                foreach (var e in list)
                    handlers.Add(e.Handler);
            }

            foreach (var h in handlers)
            {
                try
                {
                    h(changedNodeId);
                }
                catch
                {
                    // 设计期刷新不应拖垮画布；异常在调试期可在此处打断点
                }
            }
        }

        private static void RaiseUpstreamChannelOptionsChangedListeners(string producerNodeId)
        {
            List<Action<string>>? handlers = null;
            lock (_designTimeListenersLock)
            {
                PruneDeadWeakList_NoLock(_upstreamChannelListeners);
                if (_upstreamChannelListeners.Count == 0)
                    return;
                handlers = new List<Action<string>>(_upstreamChannelListeners.Count);
                foreach (var e in _upstreamChannelListeners)
                    handlers.Add(e.Handler);
            }

            foreach (var h in handlers)
            {
                try
                {
                    h(producerNodeId);
                }
                catch
                {
                }
            }
        }

        private static void RaiseDeviceChannelOptionsListeners(string deviceDisplayName)
        {
            List<Action<string>>? handlers = null;
            lock (_designTimeListenersLock)
            {
                PruneDeadWeakList_NoLock(_deviceChannelListeners);
                if (_deviceChannelListeners.Count == 0)
                    return;
                handlers = new List<Action<string>>(_deviceChannelListeners.Count);
                foreach (var e in _deviceChannelListeners)
                    handlers.Add(e.Handler);
            }

            foreach (var h in handlers)
            {
                try
                {
                    h(deviceDisplayName);
                }
                catch
                {
                }
            }
        }

        /// <summary>
        /// 与 <see cref="EndDesignTimeGraphMutationsBatch"/> 配对；可嵌套（如外层批量删除、内层其它操作）。
        /// </summary>
        public static void BeginDesignTimeGraphMutationsBatch()
        {
            lock (_graphBatchLock)
            {
                _graphBatchDepth++;
            }
        }

        /// <summary>
        /// 最外层 batch 结束时：按去重后的节点 Id 依次触发「本节点上游源已变」监听，再触发积压的上游通道选项通知。
        /// </summary>
        public static void EndDesignTimeGraphMutationsBatch()
        {
            List<string>? sourceIds = null;
            List<string>? upstreamIds = null;
            lock (_graphBatchLock)
            {
                _graphBatchDepth--;
                if (_graphBatchDepth < 0)
                    _graphBatchDepth = 0;
                if (_graphBatchDepth > 0)
                    return;
                if (_pendingSourcesChangedNodeIds.Count > 0)
                {
                    sourceIds = _pendingSourcesChangedNodeIds.ToList();
                    _pendingSourcesChangedNodeIds.Clear();
                }

                if (_pendingUpstreamChannelProducerIds.Count > 0)
                {
                    upstreamIds = _pendingUpstreamChannelProducerIds.ToList();
                    _pendingUpstreamChannelProducerIds.Clear();
                }
            }

            if (sourceIds == null && upstreamIds == null)
                return;

            // 批量恢复连线时会对大量 nodeId 依次 RaiseOwnSourcesChanged；算法/滤波等处理器内会
            // NotifyUpstreamChannelOptionsChanged。若此时已不在 batch 内，会对「全图」每个 Upstream 监听
            // 各跑一遍链式判定，复杂度≈O(刷新节点数×订阅者数)，界面会假死。此处用内层 batch 把
            // Notify 合并为一次去重后的广播。
            if (sourceIds != null)
            {
                lock (_graphBatchLock)
                {
                    _graphBatchDepth++;
                }

                try
                {
                    if (upstreamIds != null)
                    {
                        lock (_graphBatchLock)
                        {
                            foreach (var u in upstreamIds)
                                _pendingUpstreamChannelProducerIds.Add(u);
                        }
                    }

                    foreach (var id in sourceIds)
                        RaiseOwnSourcesChanged(id);
                }
                finally
                {
                    lock (_graphBatchLock)
                    {
                        _graphBatchDepth--;
                    }
                }

                List<string>? coalescedUpstream = null;
                lock (_graphBatchLock)
                {
                    if (_pendingUpstreamChannelProducerIds.Count > 0)
                    {
                        coalescedUpstream = _pendingUpstreamChannelProducerIds.ToList();
                        _pendingUpstreamChannelProducerIds.Clear();
                    }
                }

                if (coalescedUpstream != null)
                {
                    foreach (var id in coalescedUpstream)
                        RaiseUpstreamChannelOptionsChangedListeners(id);
                }
            }
            else if (upstreamIds != null)
            {
                foreach (var id in upstreamIds)
                    RaiseUpstreamChannelOptionsChangedListeners(id);
            }
        }

        public static void NotifyUpstreamChannelOptionsChanged(string producerNodeId)
        {
            if (string.IsNullOrEmpty(producerNodeId)) return;
            lock (_graphBatchLock)
            {
                if (_graphBatchDepth > 0)
                {
                    _pendingUpstreamChannelProducerIds.Add(producerNodeId);
                    return;
                }
            }

            RaiseUpstreamChannelOptionsChangedListeners(producerNodeId);
        }

        public static void NotifyDeviceChannelOptionsMayHaveChanged(string deviceDisplayName)
        {
            if (string.IsNullOrWhiteSpace(deviceDisplayName)) return;
            RaiseDeviceChannelOptionsListeners(deviceDisplayName.Trim());
        }

        /// <summary>
        /// 判断 <paramref name="producerNodeId"/> 是否落在 <paramref name="downstreamNodeId"/> 的上游链上
        /// （含直连与经算法/滤波等链式节点，以及登记的标量上游）。供克隆属性面板等场景：实例级 <c>_upstreamSources</c> 可能为空，以注册表为准。
        /// </summary>
        public static bool IsDownstreamAffectedByProducerChain(string downstreamNodeId, string producerNodeId)
        {
            if (string.IsNullOrEmpty(downstreamNodeId) || string.IsNullOrEmpty(producerNodeId))
                return false;
            var visited = new HashSet<string>(StringComparer.Ordinal);
            return IsDownstreamAffectedByProducerChainCore(downstreamNodeId, producerNodeId, visited);
        }

        private static bool IsDownstreamAffectedByProducerChainCore(
            string downstreamNodeId,
            string producerNodeId,
            HashSet<string> visited)
        {
            if (!visited.Add(downstreamNodeId))
                return false;

            if (_scalarUpstreamProviders.TryGetValue(downstreamNodeId, out var scalars) &&
                scalars.Any(p => string.Equals(p.ProviderNodeId, producerNodeId, StringComparison.Ordinal)))
                return true;

            if (!_sources.TryGetValue(downstreamNodeId, out var list))
                return false;

            foreach (var src in list)
            {
                if (src is not Node n || string.IsNullOrEmpty(n.Id))
                    continue;
                if (string.Equals(n.Id, producerNodeId, StringComparison.Ordinal))
                    return true;
                if (IsDownstreamAffectedByProducerChainCore(n.Id, producerNodeId, visited))
                    return true;
            }

            return false;
        }

        /// <summary>已登记的上游数据源（含链式透传）是否暴露指定设备显示名。</summary>
        public static bool RegisteredUpstreamExposesDevice(string downstreamNodeId, string deviceDisplayName)
        {
            if (string.IsNullOrEmpty(downstreamNodeId) || string.IsNullOrWhiteSpace(deviceDisplayName))
                return false;
            var target = deviceDisplayName.Trim();
            return GetDeviceNames(downstreamNodeId).Any(d =>
                string.Equals(d?.Trim(), target, StringComparison.OrdinalIgnoreCase));
        }

        public static void SetSources(string nodeId, IEnumerable<IDesignTimeDataSourceInfo> sources)
        {
            if (string.IsNullOrEmpty(nodeId)) return;
            _sources[nodeId] = sources.ToList();
            lock (_graphBatchLock)
            {
                if (_graphBatchDepth > 0)
                {
                    _pendingSourcesChangedNodeIds.Add(nodeId);
                    return;
                }
            }

            RaiseOwnSourcesChanged(nodeId);
        }

        public static void ClearSources(string nodeId)
        {
            if (string.IsNullOrEmpty(nodeId)) return;
            _sources.TryRemove(nodeId, out _);
            lock (_graphBatchLock)
            {
                if (_graphBatchDepth > 0)
                {
                    _pendingSourcesChangedNodeIds.Add(nodeId);
                    return;
                }
            }

            RaiseOwnSourcesChanged(nodeId);
        }

        /// <summary>缓存下游节点连入的、可产出标量输出键的上游节点（用于设计期下拉）。</summary>
        public static void SetScalarUpstreamProviders(string nodeId, IEnumerable<IDesignTimeScalarOutputProvider>? providers)
        {
            if (string.IsNullOrEmpty(nodeId)) return;
            _scalarUpstreamProviders[nodeId] = providers?.ToList() ?? new List<IDesignTimeScalarOutputProvider>();
            lock (_graphBatchLock)
            {
                if (_graphBatchDepth > 0)
                {
                    _pendingSourcesChangedNodeIds.Add(nodeId);
                    return;
                }
            }

            RaiseOwnSourcesChanged(nodeId);
        }

        /// <summary>合并所有上游提供者给出的可选标量输入键（已去重、排序）。</summary>
        public static IEnumerable<string> GetScalarInputKeyOptions(string nodeId)
        {
            if (string.IsNullOrEmpty(nodeId) || !_scalarUpstreamProviders.TryGetValue(nodeId, out var list))
                return Enumerable.Empty<string>();
            return list.SelectMany(p => p.EnumerateDesignTimeScalarInputKeys()).Distinct().OrderBy(s => s, StringComparer.Ordinal);
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

        /// <summary>
        /// 合并上游所有采集卡的通道为「设备显示名/通道名」，供仅选择通道、不再单独选采集卡的节点使用。
        /// </summary>
        public static IEnumerable<string> GetAllQualifiedChannelNames(string nodeId)
        {
            var devices = GetDeviceNames(nodeId).ToList();
            if (devices.Count == 0)
                return Enumerable.Empty<string>();
            return GetChannelNames(nodeId, devices);
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
