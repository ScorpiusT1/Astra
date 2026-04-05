using Astra.Core.Constants;
using NVHDataBridge.Models;
using System.Linq;

namespace Astra.Plugins.DataImport.Helpers
{
    internal static class DataImportNvhSampleUtil
    {
        public static bool TryExtractAsDoubleArray(
            NvhMemoryFile? file,
            string? groupName,
            string? channelName,
            out double[] samples)
        {
            samples = Array.Empty<double>();
            if (file == null)
                return false;

            if (!TryResolveGroup(file, groupName, out var group))
                return false;

            var channel = ResolveChannel(group, channelName);
            if (channel == null)
                return false;

            return TryCopyChannelToDoubles(channel, out samples);
        }

        public static bool TryGetWaveformIncrement(NvhMemoryFile? file, string? groupName, string? channelName, out double deltaTime)
        {
            deltaTime = 0;
            if (file == null)
                return false;
            if (!TryResolveGroup(file, groupName, out var group))
                return false;

            var channel = ResolveChannel(group, channelName);
            if (channel?.WfIncrement is { } inc && inc > 0)
            {
                deltaTime = inc;
                return true;
            }

            return false;
        }

        /// <summary>
        /// 从已解析的 <see cref="NvhMemoryChannelBase"/> 复制采样（多通道预览等，避免按名查找时误回落到首通道）。
        /// </summary>
        internal static bool TryExtractFromChannel(NvhMemoryChannelBase? channel, out double[] samples)
        {
            samples = Array.Empty<double>();
            if (channel == null)
                return false;
            return TryCopyChannelToDoubles(channel, out samples);
        }

        internal static double GetWaveformIncrementOrDefault(NvhMemoryChannelBase? channel, double fallbackSeconds)
        {
            if (channel?.WfIncrement is { } inc && inc > 0)
                return inc;
            return fallbackSeconds;
        }

        /// <summary>
        /// 解析通道：未指定或“组内首通道”占位符 → 组内第一个通道；
        /// 否则必须能在字典中按名命中，<b>不得</b>在未命中时回落到首通道（否则会误把多路数据都当成同一路）。
        /// </summary>
        private static NvhMemoryChannelBase? ResolveChannel(NvhMemoryGroup group, string? channelName)
        {
            if (string.IsNullOrWhiteSpace(channelName) ||
                string.Equals(
                    channelName.Trim(),
                    AstraSharedConstants.DesignTimeLabels.UseFirstChannelInGroup,
                    StringComparison.Ordinal))
            {
                return group.Channels.Values.FirstOrDefault();
            }

            return group.Channels.TryGetValue(channelName.Trim(), out var named) ? named : null;
        }

        private static bool TryCopyChannelToDoubles(NvhMemoryChannelBase channel, out double[] samples)
        {
            samples = Array.Empty<double>();

            if (channel.DataType == typeof(float))
            {
                var typed = (NvhMemoryChannel<float>)channel;
                var span = typed.PeekAll();
                if (span.Length == 0)
                    return false;
                samples = new double[span.Length];
                for (var i = 0; i < span.Length; i++)
                    samples[i] = span[i];
                return true;
            }

            if (channel.DataType == typeof(double))
            {
                var typed = (NvhMemoryChannel<double>)channel;
                var span = typed.PeekAll();
                if (span.Length == 0)
                    return false;
                samples = new double[span.Length];
                span.CopyTo(samples);
                return true;
            }

            return false;
        }

        private static bool TryResolveGroup(NvhMemoryFile file, string? groupName, out NvhMemoryGroup group)
        {
            group = null!;
            var g = string.IsNullOrWhiteSpace(groupName) ? AstraSharedConstants.DataGroups.Signal : groupName.Trim();
            if (file.TryGetGroup(g, out var found) && found != null)
            {
                group = found;
                return true;
            }
            var first = file.Groups.Values.FirstOrDefault();
            if (first == null) return false;
            group = first;
            return true;
        }
    }
}
