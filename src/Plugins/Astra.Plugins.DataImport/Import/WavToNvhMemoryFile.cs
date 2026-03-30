using Astra.Core.Constants;
using NVHDataBridge.IO.WAV;
using NVHDataBridge.Models;

namespace Astra.Plugins.DataImport.Import
{
    /// <summary>WAV 单文件 → NVH 内存模型（首通道；多声道取每帧第一路）。</summary>
    internal static class WavToNvhMemoryFile
    {
        public static NvhMemoryFile FromFile(string path)
        {
            using var reader = WavReader.Open(path);
            var all = reader.ReadAllSamples();
            var ch = reader.Channels;
            var rate = reader.SampleRate;
            if (rate <= 0)
                throw new InvalidOperationException("无效的 WAV 采样率。");

            float[] mono;
            if (ch <= 1)
            {
                mono = all;
            }
            else
            {
                var frames = all.Length / ch;
                mono = new float[frames];
                for (var i = 0; i < frames; i++)
                    mono[i] = all[i * ch];
            }

            var file = new NvhMemoryFile();
            file.Properties.Set("source", "wav");
            file.Properties.Set("path", path);

            var g = file.GetOrCreateGroup(AstraSharedConstants.DataGroups.Signal);
            var channel = g.CreateChannel<float>(
                "Wav",
                ringBufferSize: 0,
                initialCapacity: Math.Max(mono.Length, 4096),
                estimatedTotalSamples: mono.Length);
            channel.WriteSamples(mono);
            channel.WfIncrement = 1.0 / rate;
            channel.FlushTotalSamplesToProperties();
            return file;
        }
    }
}
