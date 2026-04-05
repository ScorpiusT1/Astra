using Astra.Core.Data;
using Astra.Core.Nodes.Models;
using Astra.Workflow.AlgorithmChannel.APIs;
using NVHDataBridge.Models;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Astra.Workflow.AlgorithmChannel.Helpers
{
    /// <summary>
    /// 从 <see cref="ITestDataBus"/> 加载振动/转速信号，并负责释放非托管 <see cref="Signal"/> / <see cref="Rpm"/> 缓冲区。
    /// </summary>
    public static class AlgorithmInputLoader
    {
        /// <summary>已加载的振动信号条目。</summary>
        public sealed class VibrationEntry
        {
            public required string Label { get; init; }
            public required NvhMemoryFile File { get; init; }
            public required Signal Signal { get; init; }
            public required Action Dispose { get; init; }

            /// <summary>截取后首样本在原始波形时间轴上的时刻（秒）；未截取时为 0。</summary>
            public double TimeAxisOriginSeconds { get; init; }
        }

        public static bool TryLoadVibration(
            NodeContext context,
            string nodeId,
            string? deviceDisplayName,
            string? channelName,
            out NvhMemoryFile file,
            out Signal signal,
            out Action disposeSignal,
            out string? error)
        {
            file = null!;
            signal = default;
            disposeSignal = static () => { };
            error = null;

            if (!AlgorithmRawArtifactHelper.TryResolveRawArtifactKey(context, nodeId, deviceDisplayName, out var key, out var err))
            {
                error = err;
                return false;
            }

            var bus = context.GetDataBus();
            if (bus == null || !bus.TryGet<NvhMemoryFile>(key, out file) || file == null)
            {
                error = "无法从数据总线读取 Raw 数据，请确认上游多采集已执行并成功写入。";
                return false;
            }

            string? ch = channelName;
            if (string.IsNullOrWhiteSpace(ch) ||
                string.Equals(ch.Trim(), AlgorithmDesignTimeOptions.UseFirstChannelInGroupLabel, StringComparison.Ordinal))
                ch = null;

            if (!AlgorithmNvhSampleUtil.TryExtractAsDoubleArray(file, AlgorithmRawArtifactHelper.NvhSignalGroupName, ch, out var samples) ||
                samples.Length == 0)
            {
                error = "无法读取振动通道样本或样本为空。";
                return false;
            }

            if (!AlgorithmNvhSampleUtil.TryGetWaveformIncrement(file, AlgorithmRawArtifactHelper.NvhSignalGroupName, ch, out var dt) || dt <= 0)
                dt = 1.0 / 25600.0;

            signal = new Signal(samples, dt);
            var ptr = signal.Samples;
            disposeSignal = () =>
            {
                if (ptr != IntPtr.Zero)
                    Marshal.FreeHGlobal(ptr);
            };
            return true;
        }

        /// <summary>
        /// 批量加载多设备多通道的振动信号。
        /// 内部逐个调用 <see cref="TryLoadVibration"/>，任一失败即中止并释放已加载的资源。
        /// </summary>
        public static bool TryLoadMultipleVibrations(
            NodeContext context,
            string nodeId,
            IReadOnlyList<(string DeviceName, string? ChannelName)> specs,
            out List<VibrationEntry> entries,
            out string? error,
            double analysisWindowStartSeconds = 0,
            double analysisWindowEndSeconds = 0)
        {
            entries = new List<VibrationEntry>();
            error = null;

            if (specs == null || specs.Count == 0)
            {
                error = "未指定输入源，请在节点上选择通道（格式：设备名/通道名），或依赖各采集卡首通道。";
                return false;
            }

            foreach (var (deviceName, channelName) in specs)
            {
                if (!TryLoadVibration(context, nodeId, deviceName, channelName,
                        out var file, out var signal, out var dispose, out var err))
                {
                    foreach (var prev in entries)
                        prev.Dispose();
                    entries.Clear();
                    error = $"加载 {deviceName}/{channelName ?? "首通道"} 失败: {err}";
                    return false;
                }

                var label = string.IsNullOrWhiteSpace(channelName)
                    ? deviceName
                    : $"{deviceName}/{channelName}";

                entries.Add(new VibrationEntry
                {
                    Label = label,
                    File = file,
                    Signal = signal,
                    Dispose = dispose,
                    TimeAxisOriginSeconds = 0
                });
            }

            ApplyAnalysisTimeWindow(entries, analysisWindowStartSeconds, analysisWindowEndSeconds);
            return true;
        }

        /// <summary>
        /// 与振动截取规则一致：仅当 <paramref name="startSec"/> &lt; <paramref name="endSec"/> 且按 <paramref name="deltaTime"/> 换算的样本区间非空时返回截取后的副本，否则返回原数组引用。
        /// </summary>
        public static double[] SliceSamplesForAnalysisWindow(double[] samples, double deltaTime, double startSec, double endSec)
        {
            if (samples == null || samples.Length == 0 || deltaTime <= 0)
                return samples ?? Array.Empty<double>();
            if (!TryGetSampleIndexRange(startSec, endSec, deltaTime, samples.Length, out var i0, out var i1))
                return samples;

            var len = i1 - i0;
            var slice = new double[len];
            Array.Copy(samples, i0, slice, 0, len);
            return slice;
        }

        /// <summary>
        /// 当 <paramref name="startSec"/> &lt; <paramref name="endSec"/> 时，将各通道振动信号截取为时间区间 [startSec, endSec]（秒，相对 t=0）对应的样本；否则不修改。
        /// 换算后区间为空时保留原信号。
        /// </summary>
        private static void ApplyAnalysisTimeWindow(List<VibrationEntry> entries, double startSec, double endSec)
        {
            if (entries.Count == 0)
                return;

            for (var i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                var s = e.Signal;
                var n = s.Length;
                if (n <= 0 || s.Samples == IntPtr.Zero)
                    continue;

                var dt = s.DeltaTime;
                if (!TryGetSampleIndexRange(startSec, endSec, dt, n, out var i0, out var i1))
                    continue;

                var full = new double[n];
                Marshal.Copy(s.Samples, full, 0, n);

                var len = i1 - i0;
                var slice = new double[len];
                Array.Copy(full, i0, slice, 0, len);

                var oldDispose = e.Dispose;
                oldDispose();

                var newSignal = new Signal(slice, dt, s.UnixTime);
                var newPtr = newSignal.Samples;
                var originSeconds = i0 * dt;
                entries[i] = new VibrationEntry
                {
                    Label = e.Label,
                    File = e.File,
                    Signal = newSignal,
                    TimeAxisOriginSeconds = originSeconds,
                    Dispose = () =>
                    {
                        if (newPtr != IntPtr.Zero)
                            Marshal.FreeHGlobal(newPtr);
                    }
                };
            }
        }

        /// <summary>
        /// 将时间窗 [startSec, endSec]（秒，左闭右闭）换算为样本下标 [i0, i1)（i1 为开区间）。不满足截取条件时返回 false。
        /// </summary>
        private static bool TryGetSampleIndexRange(double startSec, double endSec, double dt, int sampleCount, out int i0, out int i1)
        {
            i0 = 0;
            i1 = sampleCount;
            if (startSec >= endSec || sampleCount <= 0 || dt <= 0)
                return false;

            i0 = (int)Math.Ceiling(startSec / dt - 1e-12);
            i1 = (int)Math.Floor(endSec / dt + 1e-12) + 1;
            if (i0 < 0)
                i0 = 0;
            if (i1 > sampleCount)
                i1 = sampleCount;
            return i0 < i1;
        }

        /// <summary>释放一组 <see cref="VibrationEntry"/> 的非托管资源。</summary>
        public static void DisposeAll(IEnumerable<VibrationEntry>? entries)
        {
            if (entries == null) return;
            foreach (var e in entries)
                e.Dispose();
        }

        public static bool TryLoadRpm(
            NvhMemoryFile file,
            string? rpmChannelName,
            out Rpm rpm,
            out Action disposeRpm,
            out string? error,
            double analysisWindowStartSeconds = 0,
            double analysisWindowEndSeconds = 0)
        {
            rpm = default;
            disposeRpm = static () => { };
            error = null;

            if (string.IsNullOrWhiteSpace(rpmChannelName) ||
                string.Equals(rpmChannelName.Trim(), AlgorithmDesignTimeOptions.UseFirstChannelInGroupLabel, StringComparison.Ordinal))
            {
                error = "请指定转速通道名称。";
                return false;
            }

            var ch = rpmChannelName.Trim();
            if (!AlgorithmNvhSampleUtil.TryExtractAsDoubleArray(file, AlgorithmRawArtifactHelper.NvhSignalGroupName, ch, out var rpmValues) ||
                rpmValues.Length == 0)
            {
                error = "无法读取转速通道样本。";
                return false;
            }

            if (!AlgorithmNvhSampleUtil.TryGetWaveformIncrement(file, AlgorithmRawArtifactHelper.NvhSignalGroupName, ch, out var dt) || dt <= 0)
                dt = 1.0 / 25600.0;

            double timeOffset = 0;
            var valuesForRpm = rpmValues;
            if (TryGetSampleIndexRange(analysisWindowStartSeconds, analysisWindowEndSeconds, dt, rpmValues.Length, out var i0, out var i1))
            {
                var len = i1 - i0;
                valuesForRpm = new double[len];
                Array.Copy(rpmValues, i0, valuesForRpm, 0, len);
                timeOffset = i0 * dt;
            }

            rpm = new Rpm(valuesForRpm, dt, timeOffset);
            var p1 = rpm.RpmValues;
            var p2 = rpm.TimeValues;
            disposeRpm = () =>
            {
                if (p1 != IntPtr.Zero)
                    Marshal.FreeHGlobal(p1);
                if (p2 != IntPtr.Zero)
                    Marshal.FreeHGlobal(p2);
            };
            return true;
        }
    }
}
