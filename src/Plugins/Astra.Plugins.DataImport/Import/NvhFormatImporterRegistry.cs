namespace Astra.Plugins.DataImport.Import
{
    /// <summary>已注册的格式导入器；扩展时在此追加实例。</summary>
    public static class NvhFormatImporterRegistry
    {
        private static readonly IReadOnlyList<INvhFormatImporter> All =
        [
            new TdmsNvhImporter(),
            new WavNvhImporter(),
        ];

        public static IReadOnlyList<INvhFormatImporter> Importers => All;

        public static INvhFormatImporter? FindForPath(string path)
        {
            foreach (var imp in All)
            {
                if (imp.CanImport(path))
                    return imp;
            }

            return null;
        }
    }
}
