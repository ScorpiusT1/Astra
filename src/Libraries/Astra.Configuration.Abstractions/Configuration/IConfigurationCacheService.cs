namespace Astra.Core.Configuration
{
    /// <summary>
    /// 配置缓存服务接口
    /// </summary>
    public interface IConfigurationCacheService
    {
        bool TryGet<T>(string configId, out T config) where T : class, IConfig;
        void Set<T>(T config) where T : class, IConfig;
        void Remove<T>(string configId) where T : class, IConfig;
        void Remove(IConfig config);
        void Clear();
        void Clear<T>() where T : class, IConfig;
    }
}

