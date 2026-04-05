using System;
using System.Collections.Generic;
using System.Linq;
using Astra.Core.Nodes.Models;

namespace Astra.Plugins.Limits.Helpers
{
    /// <summary>
    /// 与算法插件 <c>AlgorithmNodeBase.ChannelNameOptions</c> 一致的分层解析：
    /// 优先实时上游「设备/通道」；无则仅连标量上游时从标量键括号内解析；再退回静态缓存、节点序列化缓存，最后退回当前已保存的通道名。
    /// </summary>
    internal static class LimitCurveChannelOptionsHelper
    {
        /// <summary>
        /// 当前能从上游实时解析到的「设备/通道」或标量括号内通道标签（与算法节点从注册表取通道列表一致，并扩展标量-only 连线）。
        /// </summary>
        public static List<string> GetLiveQualifiedCurveChannels(string nodeId)
        {
            if (string.IsNullOrEmpty(nodeId))
                return new List<string>();
            var fromRegistry = DesignTimeUpstreamRegistry.GetAllQualifiedChannelNames(nodeId).ToList();
            if (fromRegistry.Count > 0)
                return fromRegistry;
            return LimitScalarKeyChannelExtractor.ExtractQualifiedChannelLabels(
                DesignTimeUpstreamRegistry.GetScalarInputKeyOptions(nodeId));
        }

        /// <summary>
        /// 旧版属性面板用 <c>设备/（默认：组内首通道）</c> 占位，缓存中可能残留；展示时过滤。
        /// </summary>
        private static bool IsStaleFirstChannelPlaceholder(string? q)
        {
            if (string.IsNullOrWhiteSpace(q))
                return true;
            if (!QualifiedChannelHelper.TrySplit(q.Trim(), out _, out var ch))
                return false;
            return string.Equals(ch.Trim(), LimitsDesignTimeOptions.UseFirstChannelInGroupLabel, StringComparison.Ordinal);
        }

        private static IEnumerable<string> FilterCachedForDisplay(IEnumerable<string> src) =>
            src.Where(q => !IsStaleFirstChannelPlaceholder(q)).Distinct(StringComparer.Ordinal);

        /// <summary>
        /// 与 <c>AlgorithmNodeBase.RefreshAndCacheChannelOptions</c> 一致：仅当实时上游能解析出通道时刷新缓存。
        /// </summary>
        public static void RefreshCachedChannelOptions(string nodeId, List<string> instanceCachedChannelOptions)
        {
            var qualified = GetLiveQualifiedCurveChannels(nodeId);
            if (qualified.Count == 0)
                return;

            instanceCachedChannelOptions.Clear();
            instanceCachedChannelOptions.AddRange(qualified);
            DesignTimeUpstreamRegistry.CacheChannelOptions(nodeId, instanceCachedChannelOptions);
        }

        /// <summary>
        /// 构建下拉项：首项为「未选」；其余与算法节点通道选项同源的分层回退，并保证当前已选通道在列表中（便于 Combo 绑定）。
        /// </summary>
        public static List<string> BuildOptions(
            string nodeId,
            List<string> instanceCachedChannelOptions,
            string? persistedCurveChannelName)
        {
            var list = new List<string> { LimitsDesignTimeOptions.UnselectedLabel };
            var comparer = StringComparer.Ordinal;

            var qualified = GetLiveQualifiedCurveChannels(nodeId);
            if (qualified.Count > 0)
            {
                instanceCachedChannelOptions.Clear();
                instanceCachedChannelOptions.AddRange(qualified);
                DesignTimeUpstreamRegistry.CacheChannelOptions(nodeId, instanceCachedChannelOptions);
                foreach (var q in qualified)
                {
                    if (!list.Contains(q, comparer))
                        list.Add(q);
                }
            }
            else
            {
                var fromStatic = FilterCachedForDisplay(DesignTimeUpstreamRegistry.GetCachedChannelOptions(nodeId)).ToList();
                if (fromStatic.Count > 0)
                {
                    foreach (var q in fromStatic)
                    {
                        if (!list.Contains(q, comparer))
                            list.Add(q);
                    }
                }
                else
                {
                    var fromInstance = FilterCachedForDisplay(instanceCachedChannelOptions ?? new List<string>()).ToList();
                    if (fromInstance.Count > 0)
                    {
                        foreach (var q in fromInstance)
                        {
                            if (!list.Contains(q, comparer))
                                list.Add(q);
                        }
                    }
                    else
                    {
                        var saved = persistedCurveChannelName?.Trim() ?? string.Empty;
                        if (saved.Length > 0 &&
                            !string.Equals(saved, LimitsDesignTimeOptions.UnselectedLabel, StringComparison.Ordinal) &&
                            !IsStaleFirstChannelPlaceholder(saved) &&
                            !list.Contains(saved, comparer))
                            list.Add(saved);
                    }
                }
            }

            var persisted = persistedCurveChannelName?.Trim() ?? string.Empty;
            if (persisted.Length > 0 &&
                !string.Equals(persisted, LimitsDesignTimeOptions.UnselectedLabel, StringComparison.Ordinal) &&
                !list.Contains(persisted, comparer))
                list.Add(persisted);

            return list;
        }
    }
}
