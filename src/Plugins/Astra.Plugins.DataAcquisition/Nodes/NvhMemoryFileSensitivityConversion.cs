using Astra.Plugins.DataAcquisition.Configs;
using Astra.Plugins.DataAcquisition.Devices;
using NVHDataBridge.Models;
using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace Astra.Plugins.DataAcquisition.Nodes
{
    /// <summary>
    /// 将 NVH Signal 组首通道的原始量按 <see cref="DAQChannelConfig.ConvertToPhysical"/> 转为物理量，
    /// 并生成新的 <see cref="NvhMemoryFile"/> 供主页图表与 Raw 存储使用。
    /// </summary>
    internal static class NvhMemoryFileSensitivityConversion
    {
        /// <summary>
        /// 尝试按通道绑定的传感器灵敏度生成物理量副本；失败时调用方应继续使用原始文件。
        /// </summary>
        /// <param name="source">采集产生的内存文件</param>
        /// <param name="device">同一采集设备（用于解析通道配置）</param>
        /// <param name="converted">新文件，保留源文件的组名和通道结构</param>
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

            if (!TryGetFirstGroup(source, out var group))
            {
                return false;
            }

            var sourceGroupName = group.Name;
            var outFile = new NvhMemoryFile();
            var outGroup = outFile.GetOrCreateGroup(sourceGroupName);
            var anyConverted = false;

            foreach (var channelEntry in group.Channels)
            {
                var channel = channelEntry.Value;
                var channelName = channel.Name;
                if (string.IsNullOrWhiteSpace(channelName))
                {
                    continue;
                }

                var trimmedName = channelName.Trim();
                var channelConfig = FindChannelConfig(device, trimmedName);

                if (channel.DataType == typeof(float))
                {
                    var typed = (NvhMemoryChannel<float>)channel;
                    var span = typed.PeekAll();
                    if (span.Length == 0)
                    {
                        continue;
                    }

                    var outChannel = outGroup.CreateChannel<float>(
                        trimmedName,
                        ringBufferSize: 0,
                        initialCapacity: Math.Max(span.Length, 4096),
                        estimatedTotalSamples: span.Length);

                    if (channelConfig == null || !channelConfig.HasSensor || channelConfig.Sensor == null)
                    {
                        outChannel.WriteSamples(span);
                    }
                    else if (channelConfig.TryGetAffinePhysicalTransform(out var linearScale, out var linearOffset))
                    {
                        if (string.IsNullOrWhiteSpace(yAxisPhysicalUnit))
                        {
                            yAxisPhysicalUnit = GetDisplayPhysicalUnit(channelConfig.Sensor);
                        }

                        var buffer = GC.AllocateUninitializedArray<float>(span.Length);
                        ConvertFloatAffine(span, buffer, (float)linearScale, (float)linearOffset);
                        outChannel.WriteSamples(buffer);
                    }
                    else
                    {
                        continue;
                    }

                    CopyWfIncrementAndFlush(channel, outChannel);
                    anyConverted = true;
                    continue;
                }

                if (channel.DataType == typeof(double))
                {
                    var typed = (NvhMemoryChannel<double>)channel;
                    var span = typed.PeekAll();
                    if (span.Length == 0)
                    {
                        continue;
                    }

                    var outChannel = outGroup.CreateChannel<float>(
                        trimmedName,
                        ringBufferSize: 0,
                        initialCapacity: Math.Max(span.Length, 4096),
                        estimatedTotalSamples: span.Length);

                    if (channelConfig == null || !channelConfig.HasSensor || channelConfig.Sensor == null)
                    {
                        var buffer = GC.AllocateUninitializedArray<float>(span.Length);
                        ConvertDoubleToFloat(span, buffer);
                        outChannel.WriteSamples(buffer);
                    }
                    else if (channelConfig.TryGetAffinePhysicalTransform(out var linearScale, out var linearOffset))
                    {
                        if (string.IsNullOrWhiteSpace(yAxisPhysicalUnit))
                        {
                            yAxisPhysicalUnit = GetDisplayPhysicalUnit(channelConfig.Sensor);
                        }

                        var buffer = GC.AllocateUninitializedArray<float>(span.Length);
                        ConvertDoubleAffineToFloat(span, buffer, linearScale, linearOffset);
                        outChannel.WriteSamples(buffer);
                    }
                    else
                    {
                        continue;
                    }

                    CopyWfIncrementAndFlush(channel, outChannel);
                    anyConverted = true;
                }
            }

            foreach (var prop in group.Properties.Entries)
            {
                outGroup.Properties.Set(prop.Key, prop.Value);
            }

            if (!anyConverted)
            {
                converted = null;
                return false;
            }

            converted = outFile;
            return true;
        }

        private static bool TryGetFirstGroup(NvhMemoryFile source, out NvhMemoryGroup? group)
        {
            foreach (var g in source.Groups.Values)
            {
                group = g;
                return true;
            }

            group = null;
            return false;
        }

        private static void CopyWfIncrementAndFlush(NvhMemoryChannelBase source, NvhMemoryChannel<float> dest)
        {
            if (source.WfIncrement is { } inc && inc > 0)
            {
                dest.WfIncrement = inc;
            }

            dest.FlushTotalSamplesToProperties();
        }

        /// <summary>
        /// output[i] = input[i] * scale + offset，SIMD 主路径。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ConvertFloatAffine(ReadOnlySpan<float> input, Span<float> output, float scale, float offset)
        {
            var n = input.Length;
            var i = 0;
            if (Vector.IsHardwareAccelerated && n >= Vector<float>.Count)
            {
                var vScale = new Vector<float>(scale);
                var vOffset = new Vector<float>(offset);
                var vs = Vector<float>.Count;
                for (; i <= n - vs; i += vs)
                {
                    var v = new Vector<float>(input.Slice(i, vs));
                    var r = v * vScale + vOffset;
                    r.CopyTo(output.Slice(i));
                }
            }

            for (; i < n; i++)
            {
                output[i] = input[i] * scale + offset;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ConvertDoubleToFloat(ReadOnlySpan<double> input, Span<float> output)
        {
            for (var i = 0; i < input.Length; i++)
            {
                output[i] = (float)input[i];
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ConvertDoubleAffineToFloat(ReadOnlySpan<double> input, Span<float> output, double scale, double offset)
        {
            var n = input.Length;
            var i = 0;
            if (Vector.IsHardwareAccelerated && n >= Vector<double>.Count)
            {
                var vScale = new Vector<double>(scale);
                var vOffset = new Vector<double>(offset);
                var vs = Vector<double>.Count;
                Span<double> tmp = stackalloc double[Vector<double>.Count];
                for (; i <= n - vs; i += vs)
                {
                    var v = new Vector<double>(input.Slice(i, vs));
                    var r = v * vScale + vOffset;
                    r.CopyTo(tmp);
                    for (var j = 0; j < vs; j++)
                    {
                        output[i + j] = (float)tmp[j];
                    }
                }
            }

            for (; i < n; i++)
            {
                output[i] = (float)(input[i] * scale + offset);
            }
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
    }
}
