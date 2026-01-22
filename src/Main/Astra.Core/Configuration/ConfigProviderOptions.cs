namespace Astra.Core.Configuration
{
    /// <summary>
    /// 配置提供者选项
    /// </summary>
    public class ConfigProviderOptions<T> where T : class, IConfig
    {
        /// <summary>
        /// 默认存储格式
        /// </summary>
        public ConfigStorageFormat DefaultFormat { get; set; } = ConfigStorageFormat.Auto;

        /// <summary>
        /// 容器类型（当使用Container格式时）
        /// </summary>
        public Type? ContainerType { get; set; }

        /// <summary>
        /// 容器属性名（默认为"Configs"）
        /// </summary>
        public string ContainerPropertyName { get; set; } = "Configs";

        /// <summary>
        /// 默认集合文件名（约定：{配置类型名}.json）
        /// 例如：DataAcquisitionConfig.json, SensorConfig.json
        /// </summary>
        public string DefaultCollectionFileName { get; set; } = "Configs";

        /// <summary>
        /// 是否自动搜索所有文件
        /// </summary>
        public bool AutoSearchAllFiles { get; set; } = false;

        /// <summary>
        /// 新建配置时的默认格式
        /// </summary>
        public ConfigStorageFormat NewConfigFormat { get; set; } = ConfigStorageFormat.Container;

        /// <summary>
        /// 配置目录（可选，如果指定则覆盖约定规则）
        /// 如果不指定，将使用 ConfigPathString.GetConfigDirectory() 的约定规则
        /// </summary>
        public string? ConfigDirectory { get; set; }

        /// <summary>
        /// 配置文件名（可选，如果指定则覆盖约定规则）
        /// 如果不指定，将使用 DefaultCollectionFileName 或 {配置类型名}.json
        /// </summary>
        public string? ConfigFileName { get; set; }
    }
}
