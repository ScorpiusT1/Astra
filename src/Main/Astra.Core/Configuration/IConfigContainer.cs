namespace Astra.Core.Configuration
{
    /// <summary>
    /// 配置容器接口 - 用于包装配置集合的外层对象
    /// </summary>
    public interface IConfigContainer<T> where T : class, IConfig
    {
        /// <summary>
        /// 配置集合
        /// </summary>
        List<T> Configs { get; set; }
    }
}
