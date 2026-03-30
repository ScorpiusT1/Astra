using NVHDataBridge.Models;

namespace Astra.Plugins.DataImport.Import
{
    /// <summary>WAV → <see cref="NvhMemoryFile"/>（写入 Signal 组单通道）。</summary>
    public sealed class WavNvhImporter : INvhFormatImporter
    {
        public string FormatKey => "wav";

        public string FileExtension => ".wav";

        public bool CanImport(string filePath) =>
            !string.IsNullOrWhiteSpace(filePath) &&
            filePath.EndsWith(FileExtension, StringComparison.OrdinalIgnoreCase) &&
            File.Exists(filePath);

        public NvhMemoryFile Import(string filePath) =>
            WavToNvhMemoryFile.FromFile(filePath);
    }
}
