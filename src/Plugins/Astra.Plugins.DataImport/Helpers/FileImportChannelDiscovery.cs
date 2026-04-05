using Astra.Core.Constants;
using Astra.Plugins.DataImport.Import;
using NVHDataBridge.IO.TDMS;
using NVHDataBridge.IO.WAV;
using NVHDataBridge.Models;
using NationalInstrumentsGroup = NationalInstruments.Tdms.Group;

namespace Astra.Plugins.DataImport.Helpers
{
    /// <summary>
    /// 文件导入：通道名发现（尽量避免整文件导入）。
    /// WAV 仅读头；TDMS 用 <see cref="TdmsReader"/> 只遍历结构；其它格式回退 <see cref="INvhFormatImporter.Import"/>。
    /// </summary>
    internal static class FileImportChannelDiscovery
    {
        /// <summary>按 <paramref name="paths"/> 顺序返回每文件的通道名列表。</summary>
        public static List<(string Path, List<string> Channels)> DiscoverOrdered(
            IReadOnlyList<string> paths,
            int maxDegreeOfParallelism,
            CancellationToken cancellationToken)
        {
            if (paths.Count == 0)
                return new List<(string, List<string>)>();

            if (paths.Count == 1)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var p = paths[0];
                return new List<(string, List<string>)> { (p, DiscoverChannelNames(p)) };
            }

            var d = Math.Clamp(maxDegreeOfParallelism, 1, paths.Count);
            var options = new ParallelOptions
            {
                MaxDegreeOfParallelism = d,
                CancellationToken = cancellationToken
            };

            var rows = new (string Path, List<string> Channels)?[paths.Count];
            Parallel.For(0, paths.Count, options, i =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var path = paths[i];
                List<string> list;
                try
                {
                    list = DiscoverChannelNames(path);
                }
                catch
                {
                    list = new List<string>();
                }

                rows[i] = (path, list);
            });

            return rows.Select(r => r!.Value).ToList();
        }

        public static List<string> DiscoverChannelNames(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return new List<string>();

            var ext = Path.GetExtension(path)?.ToLowerInvariant();
            var baseName = Path.GetFileNameWithoutExtension(path) ?? "Data";

            if (ext == ".wav")
                return DiscoverWavChannels(path, baseName);

            if (ext == ".tdms")
                return DiscoverTdmsChannels(path, baseName);

            var importer = NvhFormatImporterRegistry.FindForPath(path);
            if (importer == null)
                return new List<string> { baseName };

            try
            {
                var file = importer.Import(path);
                return ExtractChannelNamesFromNvhFile(file, baseName);
            }
            catch
            {
                return new List<string> { baseName };
            }
        }

        private static List<string> DiscoverWavChannels(string path, string baseName)
        {
            try
            {
                using var reader = WavReader.Open(path);
                var cnt = reader.Channels;
                return cnt <= 1
                    ? new List<string> { baseName }
                    : Enumerable.Range(1, cnt).Select(i => $"{baseName}_Ch{i}").ToList();
            }
            catch
            {
                return new List<string> { baseName };
            }
        }

        /// <summary>仅打开 TDMS 结构，不加载通道采样数据。</summary>
        private static List<string> DiscoverTdmsChannels(string path, string baseName)
        {
            try
            {
                using var reader = TdmsReader.Open(path);
                NationalInstrumentsGroup? group = null;
                if (reader.TryGetGroup(AstraSharedConstants.DataGroups.Signal, out var signalGroup))
                    group = signalGroup;
                else
                    group = reader.Groups.FirstOrDefault();

                if (group == null)
                    return new List<string> { baseName };

                var names = group.Channels.Keys
                    .Where(k => !string.IsNullOrEmpty(k))
                    .ToList();
                if (names.Count == 0)
                {
                    var count = Math.Max(group.Channels.Count, 1);
                    return Enumerable.Range(1, count).Select(i => $"{baseName}_Ch{i}").ToList();
                }

                return names;
            }
            catch
            {
                return new List<string> { baseName };
            }
        }

        public static List<string> ExtractChannelNamesFromNvhFile(NvhMemoryFile file, string baseName)
        {
            NvhMemoryGroup? group = null;
            file.TryGetGroup(AstraSharedConstants.DataGroups.Signal, out group);
            group ??= file.Groups.Values.FirstOrDefault();
            if (group == null)
                return new List<string> { baseName };

            var names = group.Channels.Keys.Where(k => !string.IsNullOrEmpty(k)).ToList();
            if (names.Count == 0)
            {
                var count = Math.Max(group.Channels.Count, 1);
                return Enumerable.Range(1, count).Select(i => $"{baseName}_Ch{i}").ToList();
            }

            return names;
        }
    }
}
