namespace Astra.Core.Configuration.Providers
{
    /// <summary>
    /// 配置元数据 — Provider 内部用于维护文件索引
    /// </summary>
    public class ConfigMetadata
    {
        public string ConfigId { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public ConfigStorageMode StorageMode { get; set; }
    }
}
