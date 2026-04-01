using Astra.Core.Data;
using Astra.Core.Nodes.Models;
using Astra.Plugins.Algorithms.APIs;
using NVHDataBridge.Models;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Astra.Plugins.Algorithms.Helpers
{
    /// <summary>
    /// 从 <see cref="ITestDataBus"/> 加载振动/转速信号，并负责释放非托管 <see cref="Signal"/> / <see cref="Rpm"/> 缓冲区。
    /// </summary>
    internal static class AlgorithmInputLoader
    {
        /// <summary>已加载的振动信号条目。</summary>
        internal sealed class VibrationEntry
        {
            public required string Label { get; init; }
            public required NvhMemoryFile File { get; init; }
            public required Signal Signal { get; init; }
            public required Action Dispose { get; init; }
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
            out string? error)
        {
            entries = new List<VibrationEntry>();
            error = null;

            if (specs == null || specs.Count == 0)
            {
                error = "未指定输入源，请至少选择一个采集卡。";
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
                    Dispose = dispose
                });
            }

            return true;
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
            out string? error)
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

            rpm = new Rpm(rpmValues, dt);
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
