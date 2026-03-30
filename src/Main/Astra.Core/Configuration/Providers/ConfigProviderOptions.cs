namespace Astra.Core.Configuration.Providers
{
    /// <summary>
    /// 配置提供者选项
    /// </summary>
    public class ConfigProviderOptions<T> where T : class, IConfig
    {
        /// <summary>默认存储格式</summary>
        public ConfigStorageFormat DefaultFormat { get; set; } = ConfigStorageFormat.Auto;

        /// <summary>容器类型（当使用Container格式时）</summary>
        public Type? ContainerType { get; set; }

        /// <summary>容器属性名（默认为"Configs"）</summary>
        public string ContainerPropertyName { get; set; } = "Configs";

        /// <summary>多条配置共存时使用的默认集合文件名（不含扩展名）。</summary>
        public string DefaultCollectionFileName { get; set; } =
            Helpers.ConfigFileNameHelper.GetDefaultCollectionFileName(typeof(T));

        /// <summary>是否自动搜索所有文件</summary>
        public bool AutoSearchAllFiles { get; set; } = false;

        /// <summary>新建配置时的默认格式</summary>
        public ConfigStorageFormat NewConfigFormat { get; set; } = ConfigStorageFormat.Container;
    }
}
