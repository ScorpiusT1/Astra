using NVHDataBridge.Models;
using System.Collections.Generic;
using System.Linq;

namespace Astra.Plugins.DataProcessing.Helpers
{
    internal static class DataProcessingNvhSampleUtil
    {
        /// <summary>列出 Signal 组内可用 IIR 写回的 float/double 通道名（有序）。</summary>
        public static List<string> ListFilterableChannelNames(NvhMemoryFile? file, string? groupName)
        {
            var list = new List<string>();
            if (file == null)
                return list;
            if (!TryResolveGroup(file, groupName, out var group))
                return list;
            foreach (var kv in group.Channels)
            {
                var ch = kv.Value;
                if (ch == null || string.IsNullOrEmpty(kv.Key))
                    continue;
                var dt = ch.DataType;
                if (dt == typeof(float) || dt == typeof(double))
                    list.Add(kv.Key);
            }

            return list;
        }

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

            NvhMemoryChannelBase? channel = null;
            if (!string.IsNullOrWhiteSpace(channelName) &&
                !string.Equals(channelName.Trim(), DataProcessingDesignTimeOptions.UseFirstChannelInGroupLabel, StringComparison.Ordinal) &&
                group.Channels.TryGetValue(channelName.Trim(), out var named))
            {
                channel = named;
            }
            else
            {
                channel = group.Channels.Values.FirstOrDefault();
            }

            if (channel == null)
                return false;

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

        public static bool TryGetWaveformIncrement(NvhMemoryFile? file, string? groupName, string? channelName, out double deltaTime)
        {
            deltaTime = 0;
            if (file == null)
                return false;
            if (!TryResolveGroup(file, groupName, out var group))
                return false;

            NvhMemoryChannelBase? channel = null;
            if (!string.IsNullOrWhiteSpace(channelName) &&
                !string.Equals(channelName.Trim(), DataProcessingDesignTimeOptions.UseFirstChannelInGroupLabel, StringComparison.Ordinal) &&
                group.Channels.TryGetValue(channelName.Trim(), out var named))
                channel = named;
            else
                channel = group.Channels.Values.FirstOrDefault();

            if (channel?.WfIncrement is { } inc && inc > 0)
            {
                deltaTime = inc;
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
