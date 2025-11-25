namespace Astra.Core.Configuration
{
    /// <summary>
    /// 配置存储格式
    /// </summary>
    public enum ConfigStorageFormat
    {
        /// <summary>
        /// 自动检测
        /// </summary>
        Auto,

        /// <summary>
        /// 单个对象: { "configId": "xxx", ... }
        /// </summary>
        SingleObject,

        /// <summary>
        /// 对象数组: [{ ... }, { ... }]
        /// </summary>
        Array,

        /// <summary>
        /// 容器对象: { "Configs": [{ ... }, { ... }] }
        /// </summary>
        Container
    }
}
