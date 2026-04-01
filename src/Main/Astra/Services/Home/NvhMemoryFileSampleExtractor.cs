using NVHDataBridge.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Astra.Services.Home
{
    /// <summary>
    /// 从 <see cref="NvhMemoryFile"/> 提取双精度曲线（不消费样本，使用 Peek）。
    /// </summary>
    internal static class NvhMemoryFileSampleExtractor
    {
        private const string DefaultGroupName = "Signal";

        public static bool TryExtractAsDoubleArray(
            NvhMemoryFile? file,
            string? groupName,
            string? channelName,
            out double[] samples)
        {
            return TryExtractAsDoubleArray(file, groupName, channelName, out samples, out _);
        }

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

            var g = string.IsNullOrWhiteSpace(groupName) ? DefaultGroupName : groupName.Trim();
            if (!file.TryGetGroup(g, out var group) || group == null)
            {
                group = file.Groups.Values.FirstOrDefault();
                if (group == null)
                    return false;
            }

            NvhMemoryChannelBase? channel = null;
            if (!string.IsNullOrWhiteSpace(channelName) &&
                group.Channels.TryGetValue(channelName.Trim(), out var named))
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

            return TryReadChannelAsDouble(channel, out samples);
        }

        /// <summary>
        /// 遍历 <paramref name="file"/> 中所有组的所有通道，返回每个通道的提取结果。
        /// GroupName 优先取 group 的 "DeviceName" 属性（即采集卡名称），否则回退到组键名。
        /// </summary>
        public static List<NvhChannelExtraction> ExtractAllChannels(NvhMemoryFile? file)
        {
            var results = new List<NvhChannelExtraction>();
            if (file == null)
                return results;

            foreach (var groupKvp in file.Groups)
            {
                var group = groupKvp.Value;
                var deviceName = group.Properties.Get<string>("DeviceName");
                var displayGroupName = string.IsNullOrWhiteSpace(deviceName) ? groupKvp.Key : deviceName;

                foreach (var channelKvp in group.Channels)
                {
                    var channel = channelKvp.Value;

                    if (!TryReadChannelAsDouble(channel, out var samples) || samples.Length == 0)
                        continue;

                    var wfInc = channel.WfIncrement is { } inc && inc > 0 ? inc : 0;

                    results.Add(new NvhChannelExtraction
                    {
                        GroupName = displayGroupName,
                        ChannelName = channelKvp.Key,
                        Samples = samples,
                        WfIncrement = wfInc
                    });
                }
            }

            return results;
        }

        private static bool TryReadChannelAsDouble(NvhMemoryChannelBase channel, out double[] samples)
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
    }

    /// <summary>
    /// 从 NvhMemoryFile 提取的单个通道结果。
    /// </summary>
    internal sealed class NvhChannelExtraction
    {
        public string GroupName { get; init; } = string.Empty;
        public string ChannelName { get; init; } = string.Empty;
        public double[] Samples { get; init; } = Array.Empty<double>();
        public double WfIncrement { get; init; }
    }
}
