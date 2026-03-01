namespace Astra.Core.Configuration.Abstractions
{
    /// <summary>
    /// 可验证配置接口 — 实现此接口的配置在保存前会自动触发验证。
    /// </summary>
    public interface IValidatableConfig
    {
        ValidationResult Validate();
    }
}
