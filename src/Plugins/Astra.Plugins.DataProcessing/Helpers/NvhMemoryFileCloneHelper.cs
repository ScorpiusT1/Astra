using NVHDataBridge.Models;

namespace Astra.Plugins.DataProcessing.Helpers
{
    /// <summary>
    /// 深拷贝 <see cref="NvhMemoryFile"/>，并替换指定通道的样本（其余通道原样复制）。
    /// </summary>
    internal static class NvhMemoryFileCloneHelper
    {
        public static NvhMemoryFile CloneReplacingChannelSamples(
            NvhMemoryFile source,
            string groupName,
            string channelKey,
            double[] newSamplesDouble)
        {
            if (newSamplesDouble.Length == 0)
                throw new ArgumentException("Filtered samples cannot be empty.", nameof(newSamplesDouble));

            var dest = new NvhMemoryFile();
            foreach (var kv in source.Properties.Entries)
                dest.Properties.Set(kv.Key, kv.Value);

            foreach (var srcGroupPair in source.Groups)
            {
                var srcGroup = srcGroupPair.Value;
                var destGroup = dest.GetOrCreateGroup(srcGroup.Name);
                foreach (var gkv in srcGroup.Properties.Entries)
                    destGroup.Properties.Set(gkv.Key, gkv.Value);

                foreach (var chPair in srcGroup.Channels)
                {
                    var ch = chPair.Value;
                    var name = ch.Name;
                    if (string.Equals(srcGroup.Name, groupName, StringComparison.Ordinal) &&
                        string.Equals(name, channelKey, StringComparison.Ordinal))
                    {
                        if (ch.DataType == typeof(float))
                        {
                            var nc = destGroup.CreateChannel<float>(name, ringBufferSize: 0, initialCapacity: newSamplesDouble.Length + 1024);
                            CopyChannelMeta(ch, nc);
                            var tmp = new float[newSamplesDouble.Length];
                            for (var i = 0; i < tmp.Length; i++)
                                tmp[i] = (float)newSamplesDouble[i];
                            nc.WriteSamples(tmp);
                            nc.FlushTotalSamplesToProperties();
                        }
                        else if (ch.DataType == typeof(double))
                        {
                            var nc = destGroup.CreateChannel<double>(name, ringBufferSize: 0, initialCapacity: newSamplesDouble.Length + 1024);
                            CopyChannelMeta(ch, nc);
                            nc.WriteSamples(newSamplesDouble);
                            nc.FlushTotalSamplesToProperties();
                        }
                        else
                            throw new NotSupportedException($"通道 {name} 类型 {ch.DataType.Name} 不支持滤波写回。");
                    }
                    else
                    {
                        CopyChannelWhole(destGroup, ch);
                    }
                }
            }

            return dest;
        }

        private static void CopyChannelMeta(NvhMemoryChannelBase src, NvhMemoryChannelBase dest)
        {
            foreach (var kv in src.Properties.Entries)
                dest.Properties.Set(kv.Key, kv.Value);
        }

        private static void CopyChannelWhole(NvhMemoryGroup destGroup, NvhMemoryChannelBase ch)
        {
            if (ch.DataType == typeof(float))
            {
                var s = (NvhMemoryChannel<float>)ch;
                var span = s.PeekAll();
                var nc = destGroup.CreateChannel<float>(ch.Name, ringBufferSize: 0, initialCapacity: Math.Max(1024, span.Length));
                CopyChannelMeta(ch, nc);
                if (span.Length > 0)
                    nc.WriteSamples(span);
                nc.FlushTotalSamplesToProperties();
            }
            else if (ch.DataType == typeof(double))
            {
                var s = (NvhMemoryChannel<double>)ch;
                var span = s.PeekAll();
                var nc = destGroup.CreateChannel<double>(ch.Name, ringBufferSize: 0, initialCapacity: Math.Max(1024, span.Length));
                CopyChannelMeta(ch, nc);
                if (span.Length > 0)
                    nc.WriteSamples(span);
                nc.FlushTotalSamplesToProperties();
            }
            else
                throw new NotSupportedException($"通道 {ch.Name} 类型 {ch.DataType.Name} 无法复制。");
        }
    }
}
