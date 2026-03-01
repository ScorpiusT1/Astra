namespace Astra.Core.Configuration.Abstractions
{
    /// <summary>
    /// 支持克隆的配置接口。克隆结果拥有新的 ConfigId，原始数据不变。
    /// </summary>
    public interface IClonableConfig : IConfig
    {
        /// <summary>以新的随机 ConfigId 克隆当前配置。</summary>
        IConfig Clone();

        /// <summary>以指定 ConfigId 克隆当前配置。</summary>
        IConfig CloneWithId(string newConfigId);
    }
}
