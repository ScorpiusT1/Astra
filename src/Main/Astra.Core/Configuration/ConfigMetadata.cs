namespace Astra.Core.Configuration
{
    /// <summary>
    /// 配置元数据
    /// </summary>
    public class ConfigMetadata
    {
        public string ConfigId { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public ConfigStorageMode StorageMode { get; set; }
    }
}
