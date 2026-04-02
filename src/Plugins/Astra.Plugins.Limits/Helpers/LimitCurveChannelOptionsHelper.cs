using System;
using System.Collections.Generic;
using System.Linq;
using Astra.Core.Nodes.Models;

namespace Astra.Plugins.Limits.Helpers
{
    /// <summary>
    /// 主页曲线「通道」单选：优先 <c>GetAllQualifiedChannelNames</c>（直连数据源时）；
    /// 若仅连算法等标量上游，则从 <c>Scalar.xxx(通道标签)</c> 解析通道列表。
    /// </summary>
    internal static class LimitCurveChannelOptionsHelper
    {
        /// <summary>
        /// 旧版属性面板用 <c>设备/（默认：组内首通道）</c> 占位，缓存中可能残留；上游真实通道不应过滤。
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
        /// 解析当前可用的合格通道列表并写入实例缓存与静态缓存（供序列化/克隆一致）。
        /// </summary>
        public static void RefreshCachedChannelOptions(string nodeId, List<string> instanceCachedChannelOptions)
        {
            var fromRegistry = DesignTimeUpstreamRegistry.GetAllQualifiedChannelNames(nodeId).ToList();
            if (fromRegistry.Count > 0)
            {
                instanceCachedChannelOptions.Clear();
                instanceCachedChannelOptions.AddRange(fromRegistry);
                DesignTimeUpstreamRegistry.CacheChannelOptions(nodeId, instanceCachedChannelOptions);
                return;
            }

            var fromScalar = LimitScalarKeyChannelExtractor.ExtractQualifiedChannelLabels(
                DesignTimeUpstreamRegistry.GetScalarInputKeyOptions(nodeId));
            if (fromScalar.Count > 0)
            {
                instanceCachedChannelOptions.Clear();
                instanceCachedChannelOptions.AddRange(fromScalar);
                DesignTimeUpstreamRegistry.CacheChannelOptions(nodeId, instanceCachedChannelOptions);
            }
        }

        /// <summary>
        /// 构建下拉项：直连数据源 → 注册表通道；否则从标量键解析；再无则读缓存。
        /// </summary>
        public static List<string> BuildOptions(
            string nodeId,
            List<string> instanceCachedChannelOptions,
            string? persistedCurveChannelName)
        {
            var list = new List<string> { LimitsDesignTimeOptions.UnselectedLabel };
            var comparer = StringComparer.Ordinal;

            var fromRegistry = DesignTimeUpstreamRegistry.GetAllQualifiedChannelNames(nodeId).ToList();
            List<string> qualified;
            if (fromRegistry.Count > 0)
            {
                qualified = fromRegistry;
            }
            else
            {
                qualified = LimitScalarKeyChannelExtractor.ExtractQualifiedChannelLabels(
                    DesignTimeUpstreamRegistry.GetScalarInputKeyOptions(nodeId));
            }

            if (qualified.Count > 0)
            {
                instanceCachedChannelOptions.Clear();
                instanceCachedChannelOptions.AddRange(qualified);
                DesignTimeUpstreamRegistry.CacheChannelOptions(nodeId, qualified);
                foreach (var q in qualified)
                {
                    if (!list.Contains(q, comparer))
                        list.Add(q);
                }
            }
            else
            {
                foreach (var q in FilterCachedForDisplay(DesignTimeUpstreamRegistry.GetCachedChannelOptions(nodeId)))
                {
                    if (!list.Contains(q, comparer))
                        list.Add(q);
                }

                foreach (var q in FilterCachedForDisplay(instanceCachedChannelOptions))
                {
                    if (!list.Contains(q, comparer))
                        list.Add(q);
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
