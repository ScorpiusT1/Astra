namespace Astra.Plugins.DataImport.Helpers
{
    /// <summary>文件导入面板中磁盘 I/O 并行度上限（避免机械盘/网络盘上同时打开过多 TDMS）。</summary>
    internal static class FileImportIoParallelism
    {
        /// <summary>同时读取的文件数上限。</summary>
        public const int MaxDegreeOfParallelism = 4;

        public static int EffectiveDegree =>
            Math.Min(MaxDegreeOfParallelism, Math.Max(1, Environment.ProcessorCount));
    }
}
