namespace Astra.Core.Configuration
{
    /// <summary>
    /// 配置事件服务接口
    /// </summary>
    public interface IConfigurationEventService
    {
        void Subscribe<T>(Action<T, ConfigChangeType> callback) where T : class, IConfig;
        void Unsubscribe<T>(Action<T, ConfigChangeType> callback) where T : class, IConfig;
        void Publish<T>(T config, ConfigChangeType changeType) where T : class, IConfig;
        void Publish(IConfig config, ConfigChangeType changeType);
    }
}

