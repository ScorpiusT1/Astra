using NVHDataBridge.Converters;
using NVHDataBridge.Models;

namespace Astra.Plugins.DataImport.Import
{
    /// <summary>TDMS → <see cref="NvhMemoryFile"/>（NVH 桥已提供双向转换）。</summary>
    public sealed class TdmsNvhImporter : INvhFormatImporter
    {
        public string FormatKey => "tdms";

        public string FileExtension => ".tdms";

        public bool CanImport(string filePath) =>
            !string.IsNullOrWhiteSpace(filePath) &&
            filePath.EndsWith(FileExtension, StringComparison.OrdinalIgnoreCase) &&
            File.Exists(filePath);

        public NvhMemoryFile Import(string filePath) =>
            NvhTdmsConverter.LoadFromTdms(filePath);
    }
}
