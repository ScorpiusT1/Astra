using Astra.Plugins.DataAcquisition.Configs;
using Astra.Plugins.DataAcquisition.Devices;
using Astra.Core.Constants;
using NVHDataBridge.Models;
using System;
using System.Linq;

namespace Astra.Plugins.DataAcquisition.Nodes
{
    /// <summary>
    /// 将 NVH Signal 组首通道的原始量按 <see cref="DAQChannelConfig.ConvertToPhysical"/> 转为物理量，
    /// 并生成新的 <see cref="NvhMemoryFile"/> 供主页图表与 Raw 存储使用。
    /// </summary>
    internal static class NvhMemoryFileSensitivityConversion
    {
        private const string SignalGroupName = AstraSharedConstants.DataGroups.Signal;

        /// <summary>
        /// 尝试按通道绑定的传感器灵敏度生成物理量副本；失败时调用方应继续使用原始文件。
        /// </summary>
        /// <param name="source">采集产生的内存文件</param>
        /// <param name="device">同一采集设备（用于解析通道配置）</param>
        /// <param name="converted">新文件，仅含 Signal 组下单通道（float）</param>
        /// <param name="yAxisPhysicalUnit">用于图表纵轴单位的字符串（<see cref="SensorConfig.PhysicalUnit"/> 或根据灵敏度推断）</param>
        public static bool TryCreatePhysicalChannelCopy(
            NvhMemoryFile? source,
            DataAcquisitionDeviceBase device,
            out NvhMemoryFile? converted,
            out string yAxisPhysicalUnit)
        {
            converted = null;
            yAxisPhysicalUnit = string.Empty;
            if (source == null)
            {
                return false;
            }

            if (!source.TryGetGroup(SignalGroupName, out var group) || group == null)
            {
                return false;
            }

            var firstChannel = group.Channels.Values.FirstOrDefault();
            if (firstChannel == null)
            {
                return false;
            }

            var channelName = firstChannel.Name;
            if (string.IsNullOrWhiteSpace(channelName))
            {
                return false;
            }

            var channelConfig = FindChannelConfig(device, channelName.Trim());
            if (channelConfig == null || !channelConfig.HasSensor || channelConfig.Sensor == null)
            {
                return false;
            }

            if (!TryExtractSamplesAsDoubles(firstChannel, out var rawSamples) || rawSamples.Length == 0)
            {
                return false;
            }

            var physical = new double[rawSamples.Length];
            for (var i = 0; i < rawSamples.Length; i++)
            {
                physical[i] = channelConfig.ConvertToPhysical(rawSamples[i]);
            }

            yAxisPhysicalUnit = GetDisplayPhysicalUnit(channelConfig.Sensor);

            var outFile = new NvhMemoryFile();
            var outGroup = outFile.GetOrCreateGroup(SignalGroupName);
            var outChannel = outGroup.CreateChannel<float>(channelName.Trim(), ringBufferSize: 0, initialCapacity: Math.Max(physical.Length, 4096), estimatedTotalSamples: physical.Length);
            var buffer = new float[physical.Length];
            for (var i = 0; i < physical.Length; i++)
            {
                buffer[i] = (float)physical[i];
            }

            outChannel.WriteSamples(buffer);
            outChannel.FlushTotalSamplesToProperties();
            converted = outFile;
            return true;
        }

        private static DAQChannelConfig? FindChannelConfig(DataAcquisitionDeviceBase device, string channelName)
        {
            if (device.CurrentConfig is not DataAcquisitionConfig dac || dac.Channels == null)
            {
                return null;
            }

            foreach (var c in dac.Channels)
            {
                if (c == null || !c.Enabled)
                {
                    continue;
                }

                var n = string.IsNullOrWhiteSpace(c.ChannelName) ? $"Channel{c.ChannelId}" : c.ChannelName.Trim();
                if (string.Equals(n, channelName, StringComparison.OrdinalIgnoreCase))
                {
                    return c;
                }
            }

            return null;
        }

        private static string GetDisplayPhysicalUnit(SensorConfig sensor)
        {
            if (!string.IsNullOrWhiteSpace(sensor.PhysicalUnit))
            {
                return sensor.PhysicalUnit.Trim();
            }

            return SensorConfig.GetPhysicalUnitFromSensitivityUnit(sensor.SensitivityUnit, sensor.SensorType) ?? string.Empty;
        }

        private static bool TryExtractSamplesAsDoubles(NvhMemoryChannelBase channel, out double[] samples)
        {
            samples = Array.Empty<double>();
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
