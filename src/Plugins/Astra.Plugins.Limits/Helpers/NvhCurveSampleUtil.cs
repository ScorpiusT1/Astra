using NVHDataBridge.Models;
using System;
using System.Linq;

namespace Astra.Plugins.Limits.Helpers
{
    internal static class NvhCurveSampleUtil
    {
        public static bool TryExtractAsDoubleArray(
            NvhMemoryFile? file,
            string? groupName,
            string? channelName,
            out double[] samples)
        {
            return TryExtractAsDoubleArray(file, groupName, channelName, out samples, out _);
        }

        /// <summary>与 <see cref="TryExtractAsDoubleArray(NvhMemoryFile?, string?, string?, out double[])"/> 相同，并返回波形增量（用于图表横轴）。</summary>
        public static bool TryExtractAsDoubleArray(
            NvhMemoryFile? file,
            string? groupName,
            string? channelName,
            out double[] samples,
            out double wfIncrement)
        {
            samples = Array.Empty<double>();
            wfIncrement = 0;
            if (file == null)
            {
                return false;
            }

            if (!TryResolveGroup(file, groupName, out var group))
            {
                return false;
            }

            NvhMemoryChannelBase? channel = null;
            if (!string.IsNullOrWhiteSpace(channelName) && group.Channels.TryGetValue(channelName.Trim(), out var named))
            {
                channel = named;
            }
            else
            {
                channel = group.Channels.Values.FirstOrDefault();
            }

            if (channel == null)
            {
                return false;
            }

            if (channel.WfIncrement is { } inc && inc > 0)
            {
                wfIncrement = inc;
            }

            if (channel.DataType == typeof(float))
            {
                var typed = (NvhMemoryChannel<float>)channel;
                var span = typed.PeekAll();
                if (span.Length == 0)
                {
                    return false;
                }

                samples = new double[span.Length];
                for (var i = 0; i < span.Length; i++)
                {
                    samples[i] = span[i];
                }

                return true;
            }

            if (channel.DataType == typeof(double))
            {
                var typed = (NvhMemoryChannel<double>)channel;
                var span = typed.PeekAll();
                if (span.Length == 0)
                {
                    return false;
                }

                samples = new double[span.Length];
                span.CopyTo(samples);
                return true;
            }

            return false;
        }

        private static bool TryResolveGroup(NvhMemoryFile file, string? groupName, out NvhMemoryGroup group)
        {
            group = null!;
            var g = string.IsNullOrWhiteSpace(groupName) ? "Signal" : groupName.Trim();
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
