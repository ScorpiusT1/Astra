using NVHDataBridge.Models;
using System;
using System.Linq;

namespace Astra.Services.WorkflowArchive
{
    internal static class NvhArchiveSampleUtil
    {
        public const string DefaultSignalGroupName = "Signal";

        public static bool TryExtractAsDoubleArray(
            NvhMemoryFile? file,
            string? groupName,
            string? channelName,
            out double[] samples)
        {
            samples = Array.Empty<double>();
            if (file == null)
            {
                return false;
            }

            var g = string.IsNullOrWhiteSpace(groupName) ? DefaultSignalGroupName : groupName.Trim();
            if (!file.TryGetGroup(g, out var group) || group == null)
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

        /// <summary>
        /// 从单通道元数据解析 WAV 采样率（Hz）：优先 <c>SampleRate</c>，否则由 <see cref="NvhMemoryChannelBase.WfIncrement"/> 倒数得到。
        /// </summary>
        public static bool TryGetChannelSampleRateHz(NvhMemoryChannelBase channel, out int sampleRateHz)
        {
            sampleRateHz = 0;
            if (channel.Properties.TryGet<double>("SampleRate", out var sr) && sr > 0)
            {
                sampleRateHz = (int)Math.Round(sr);
                return sampleRateHz > 0;
            }

            if (channel.WfIncrement is { } inc && inc > 0)
            {
                sampleRateHz = (int)Math.Round(1.0 / inc);
                return sampleRateHz > 0;
            }

            return false;
        }

        public static bool TryGetFirstChannelSampleRateHz(NvhMemoryFile file, string groupName, out int sampleRateHz)
        {
            sampleRateHz = 0;
            if (!file.TryGetGroup(groupName, out var group) || group == null)
            {
                group = file.Groups.Values.FirstOrDefault();
            }

            if (group == null)
            {
                return false;
            }

            foreach (var ch in group.Channels.Values)
            {
                if (TryGetChannelSampleRateHz(ch, out sampleRateHz))
                    return true;
            }

            return false;
        }
    }
}
