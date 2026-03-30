using Astra.Plugins.DataImport.Import;

namespace Astra.Plugins.DataImport.Nodes
{
    /// <summary>导入 WAV 为 Raw（首通道；多声道取每帧第一路）。</summary>
    public sealed class WavImportRawNode : FileImportRawNodeBase
    {
        public WavImportRawNode() : base("DataImport.WavToRaw", "WAV 导入")
        {
        }

        protected override INvhFormatImporter? GetImporter() => new WavNvhImporter();

        protected override string ChartArtifactName => "DataImport.Wav.Preview";

        protected override string ResultTag => "WAV";
    }
}
