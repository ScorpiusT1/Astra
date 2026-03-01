namespace Astra.Core.Configuration.Abstractions
{
    /// <summary>
    /// 配置容器接口 - 用于包装配置集合的外层对象
    /// </summary>
    public interface IConfigContainer<T> where T : class, IConfig
    {
        List<T> Configs { get; set; }
    }
}
