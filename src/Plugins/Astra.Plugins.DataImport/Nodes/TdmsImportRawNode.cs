using Astra.Plugins.DataImport.Import;

namespace Astra.Plugins.DataImport.Nodes
{
    /// <summary>导入 TDMS 为 Raw（虚拟采集「文件导入」），下游算法/滤波请选择同一采集卡显示名。</summary>
    public sealed class TdmsImportRawNode : FileImportRawNodeBase
    {
        public TdmsImportRawNode() : base("DataImport.TdmsToRaw", "TDMS 导入")
        {
        }

        protected override INvhFormatImporter? GetImporter() => new TdmsNvhImporter();

        protected override string ChartArtifactName => "DataImport.Tdms.Preview";

        protected override string ResultTag => "TDMS";
    }
}
