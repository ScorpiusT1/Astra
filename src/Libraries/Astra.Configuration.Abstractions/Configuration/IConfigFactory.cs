namespace Astra.Core.Configuration
{
    /// <summary>
    /// 配置工厂接口
    /// </summary>
    public interface IConfigFactory
    {
        IConfig Create(string configId);
    }

    public interface IConfigFactory<T> : IConfigFactory where T : class, IConfig
    {
        new T Create(string configId);
    }
}

