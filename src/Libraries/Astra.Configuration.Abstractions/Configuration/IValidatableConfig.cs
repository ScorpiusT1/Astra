namespace Astra.Core.Configuration
{
    /// <summary>
    /// 可验证配置接口
    /// </summary>
    public interface IValidatableConfig
    {
        ValidationResult Validate();
    }
}

