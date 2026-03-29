using NVHDataBridge.Models;
using System;
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
            samples = Array.Empty<double>();
            if (file == null)
            {
                return false;
            }

            var g = string.IsNullOrWhiteSpace(groupName) ? DefaultGroupName : groupName.Trim();
            if (!file.TryGetGroup(g, out var group) || group == null)
            {
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
    }
}
