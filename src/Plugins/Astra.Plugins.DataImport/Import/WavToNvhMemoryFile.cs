using Astra.Core.Constants;
using NVHDataBridge.IO.WAV;
using NVHDataBridge.Models;

namespace Astra.Plugins.DataImport.Import
{
    /// <summary>WAV 文件 → NVH 内存模型（支持多声道，每声道一个通道，通道名自动生成）。</summary>
    internal static class WavToNvhMemoryFile
    {
        public static NvhMemoryFile FromFile(string path)
        {
            using var reader = WavReader.Open(path);
            var all = reader.ReadAllSamples();
            var channelCount = reader.Channels;
            var rate = reader.SampleRate;
            if (rate <= 0)
                throw new InvalidOperationException("无效的 WAV 采样率。");

            var baseName = Path.GetFileNameWithoutExtension(path) ?? "Wav";
            var frames = channelCount > 0 ? all.Length / channelCount : all.Length;

            var file = new NvhMemoryFile();
            file.Properties.Set("source", "wav");
            file.Properties.Set("path", path);

            var g = file.GetOrCreateGroup(AstraSharedConstants.DataGroups.Signal);
            var dt = 1.0 / rate;

            for (var chIdx = 0; chIdx < Math.Max(channelCount, 1); chIdx++)
            {
                var chName = channelCount <= 1
                    ? baseName
                    : $"{baseName}_Ch{chIdx + 1}";

                float[] samples;
                if (channelCount <= 1)
                {
                    samples = all;
                }
                else
                {
                    samples = new float[frames];
                    for (var i = 0; i < frames; i++)
                        samples[i] = all[i * channelCount + chIdx];
                }

                var channel = g.CreateChannel<float>(
                    chName,
                    ringBufferSize: 0,
                    initialCapacity: Math.Max(samples.Length, 4096),
                    estimatedTotalSamples: samples.Length);
                channel.WriteSamples(samples);
                channel.WfIncrement = dt;
                channel.FlushTotalSamplesToProperties();
            }

            return file;
        }
    }
}
