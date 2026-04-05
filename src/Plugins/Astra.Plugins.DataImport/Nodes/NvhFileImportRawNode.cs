using Astra.Plugins.DataImport.Import;

namespace Astra.Plugins.DataImport.Nodes
{
    /// <summary>
    /// 从磁盘导入 TDMS、WAV 等为 NVH Raw（虚拟采集「文件导入」）。
    /// TDMS 走 <see cref="TdmsNvhImporter"/>；其它扩展名由 <see cref="NvhFormatImporterRegistry"/> 解析（含 WAV）。
    /// </summary>
    public class NvhFileImportRawNode : FileImportRawNodeBase
    {
        public NvhFileImportRawNode() : base("DataImport.FileToRaw", "文件导入")
        {
        }

        protected override INvhFormatImporter? GetImporter() => PrimaryImporter;

        /// <summary>执行时优先尝试的导入器；路径不匹配时回落到注册表。</summary>
        protected virtual INvhFormatImporter? PrimaryImporter => new TdmsNvhImporter();

        protected override string ChartArtifactName => PreviewArtifactName;

        protected virtual string PreviewArtifactName => "DataImport.File.Preview";

        protected override string ResultTag => NodeResultTag;

        protected virtual string NodeResultTag => "文件导入";
    }

    /// <summary>兼容旧版工作流 / 序列化 $type；新图请使用 <see cref="NvhFileImportRawNode"/>。</summary>
    public sealed class TdmsImportRawNode : NvhFileImportRawNode
    {
        public TdmsImportRawNode()
        {
            NodeType = "DataImport.TdmsToRaw";
            Name = "TDMS 导入";
        }

        protected override INvhFormatImporter? PrimaryImporter => new TdmsNvhImporter();

        protected override string PreviewArtifactName => "DataImport.Tdms.Preview";

        protected override string NodeResultTag => "TDMS";
    }

    /// <summary>兼容旧版工作流 / 序列化 $type；新图请使用 <see cref="NvhFileImportRawNode"/>。</summary>
    public sealed class WavImportRawNode : NvhFileImportRawNode
    {
        public WavImportRawNode()
        {
            NodeType = "DataImport.WavToRaw";
            Name = "WAV 导入";
        }

        protected override INvhFormatImporter? PrimaryImporter => new WavNvhImporter();

        protected override string PreviewArtifactName => "DataImport.Wav.Preview";

        protected override string NodeResultTag => "WAV";
    }
}
