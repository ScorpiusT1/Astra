namespace Astra.Core.Configuration
{
    /// <summary>
    /// 配置提供者选项
    /// </summary>
    public class ConfigProviderOptions<T> where T : class, IConfig
    {
        /// <summary>
        /// 默认集合文件名（不带扩展名）
        /// </summary>
        public string DefaultCollectionFileName { get; set; } = typeof(T).Name;

        /// <summary>
        /// 是否自动扫描目录下所有 JSON 文件
        /// </summary>
        public bool AutoSearchAllFiles { get; set; } = true;

        /// <summary>
        /// 容器属性名（默认：Configs）
        /// </summary>
        public string ContainerPropertyName { get; set; } = "Configs";

        /// <summary>
        /// 容器类型（可选）
        /// </summary>
        public Type? ContainerType { get; set; }

        /// <summary>
        /// 默认文件格式（Auto 表示自动检测）
        /// </summary>
        public ConfigStorageFormat DefaultFormat { get; set; } = ConfigStorageFormat.Auto;

        /// <summary>
        /// 新建配置时使用的格式
        /// </summary>
        public ConfigStorageFormat NewConfigFormat { get; set; } = ConfigStorageFormat.Container;
    }
}

