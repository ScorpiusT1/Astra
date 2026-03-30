using Astra.Core.Constants;
using Astra.Plugins.DataImport.Helpers;
using NVHDataBridge.IO.WAV;
using NVHDataBridge.Models;

namespace Astra.Plugins.DataImport.Conversion
{
    /// <summary>将 NVH 内存文件中的波形导出为单声道 IEEE float WAV（32-bit）。</summary>
    public static class NvhToWavExporter
    {
        public static void ExportSignalChannel(
            NvhMemoryFile file,
            string outputPath,
            string? channelKey = null)
        {
            if (string.IsNullOrWhiteSpace(outputPath))
                throw new ArgumentException("输出路径不能为空", nameof(outputPath));

            if (!DataImportNvhSampleUtil.TryExtractAsDoubleArray(
                    file,
                    AstraSharedConstants.DataGroups.Signal,
                    channelKey,
                    out var samples) ||
                samples.Length == 0)
                throw new InvalidOperationException("无法从 Signal 组读取通道样本。");

            if (!DataImportNvhSampleUtil.TryGetWaveformIncrement(
                    file,
                    AstraSharedConstants.DataGroups.Signal,
                    channelKey,
                    out var dt) ||
                dt <= 0)
            {
                dt = 1.0 / 48000.0;
            }

            var rate = (int)Math.Clamp(Math.Round(1.0 / dt), 1, int.MaxValue);
            var floats = new float[samples.Length];
            for (var i = 0; i < samples.Length; i++)
                floats[i] = (float)samples[i];

            var dir = Path.GetDirectoryName(Path.GetFullPath(outputPath));
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            using var w = new WavWriter(outputPath, rate, 1, 32);
            w.WriteSamples(floats);
        }
    }
}
