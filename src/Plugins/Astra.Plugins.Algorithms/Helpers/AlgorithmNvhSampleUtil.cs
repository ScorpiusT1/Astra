using NVHDataBridge.Models;
using System;
using System.Linq;

namespace Astra.Plugins.Algorithms.Helpers
{
    internal static class AlgorithmNvhSampleUtil
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

            var g = string.IsNullOrWhiteSpace(groupName) ? "Signal" : groupName.Trim();
            if (!file.TryGetGroup(g, out var group) || group == null)
                return false;

            NvhMemoryChannelBase? channel = null;
            if (!string.IsNullOrWhiteSpace(channelName) &&
                !string.Equals(channelName.Trim(), AlgorithmDesignTimeOptions.UseFirstChannelInGroupLabel, StringComparison.Ordinal) &&
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
            var g = string.IsNullOrWhiteSpace(groupName) ? "Signal" : groupName.Trim();
            if (!file.TryGetGroup(g, out var group) || group == null)
                return false;

            NvhMemoryChannelBase? channel = null;
            if (!string.IsNullOrWhiteSpace(channelName) &&
                !string.Equals(channelName.Trim(), AlgorithmDesignTimeOptions.UseFirstChannelInGroupLabel, StringComparison.Ordinal) &&
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
    }
}
