namespace Astra.Core.Configuration
{
    /// <summary>
    /// 配置提供者选项（简化版 - 去除标识符提取器）
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
        /// 默认集合文件名
        /// </summary>
        public string DefaultCollectionFileName { get; set; } = "Configs";

        /// <summary>
        /// 是否自动搜索所有文件
        /// </summary>
        public bool AutoSearchAllFiles { get; set; } = true;

        /// <summary>
        /// 新建配置时的默认格式
        /// </summary>
        public ConfigStorageFormat NewConfigFormat { get; set; } = ConfigStorageFormat.Container;
    }
}
