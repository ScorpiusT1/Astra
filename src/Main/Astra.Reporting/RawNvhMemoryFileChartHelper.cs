using System;
using System.Collections.Generic;
using Astra.Core.Constants;
using Astra.UI.Abstractions.Nodes;
using NVHDataBridge.Models;

namespace Astra.Reporting
{
    /// <summary>
    /// 从总线 Raw 中的 <see cref="NvhMemoryFile"/> 提取可渲染通道（与主页 Hydrator 逻辑对齐，供报告收集与 PNG 渲染）。
    /// </summary>
    internal static class RawNvhMemoryFileChartHelper
    {
        private const string DefaultGroupName = "Signal";

        internal readonly struct ChannelSeries
        {
            public string DisplayName { get; init; }
            public double[] Samples { get; init; }
            public double WfIncrement { get; init; }
            /// <summary>时间轴单位（如 TDMS <c>wf_xunit_string</c> / <c>wf_xunit</c>）。</summary>
            public string TimeAxisUnit { get; init; }
            /// <summary>幅值轴单位（如 <c>unit_string</c> / <c>wf_yunit</c>）。</summary>
            public string AmplitudeAxisUnit { get; init; }
        }

        /// <summary>
        /// 提取所有可转为双精度样本的非空通道；顺序稳定，用于多通道拆成多张报告图。
        /// </summary>
        public static List<ChannelSeries> ExtractChannelSeries(NvhMemoryFile? file)
        {
            var results = new List<ChannelSeries>();
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

                    var wfInc = channel.WfIncrement is { } inc && inc > 0 ? inc : 0.0;
                    var name = $"{displayGroupName}/{channelKvp.Key}";
                    results.Add(new ChannelSeries
                    {
                        DisplayName = name,
                        Samples = samples,
                        WfIncrement = wfInc,
                        TimeAxisUnit = ReadTimeAxisUnit(channel),
                        AmplitudeAxisUnit = ReadAmplitudeAxisUnit(channel)
                    });
                }
            }

            return results;
        }

        private static string ReadTimeAxisUnit(NvhMemoryChannelBase channel)
        {
            var u = channel.Properties.Get<string>("wf_xunit_string", string.Empty)?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(u))
                u = channel.Properties.Get<string>("wf_xunit", string.Empty)?.Trim() ?? string.Empty;
            return u;
        }

        private static string ReadAmplitudeAxisUnit(NvhMemoryChannelBase channel)
        {
            var u = channel.Properties.Get<string>("unit_string", string.Empty)?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(u))
                u = channel.Properties.Get<string>("wf_yunit", string.Empty)?.Trim() ?? string.Empty;
            return u;
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

        /// <summary>
        /// 构建单通道 <see cref="ChartDisplayPayload"/>（报告渲染用）。
        /// </summary>
        public static ChartDisplayPayload BuildSignalPayload(ChannelSeries ch)
        {
            var period = ch.WfIncrement > 0 ? ch.WfIncrement : 1.0;
            var xu = ch.TimeAxisUnit;
            if (string.IsNullOrEmpty(xu))
                xu = AstraSharedConstants.DataAcquisitionDefaults.CodeDefinedChartXAxisUnit;

            return new ChartDisplayPayload
            {
                Kind = ChartPayloadKind.Signal1D,
                SignalY = ch.Samples,
                SamplePeriod = period,
                BottomAxisLabel = AstraSharedConstants.DataAcquisitionDefaults.CodeDefinedChartXAxisLabel,
                BottomAxisUnit = xu,
                LeftAxisLabel = AstraSharedConstants.DataAcquisitionDefaults.CodeDefinedChartYAxisLabel,
                LeftAxisUnit = ch.AmplitudeAxisUnit?.Trim() ?? string.Empty
            };
        }
    }
}
