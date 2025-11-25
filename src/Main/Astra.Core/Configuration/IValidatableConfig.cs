namespace Astra.Core.Configuration
{
    // ==================== 验证接口定义 ====================

    /// <summary>
    /// 可验证配置接口
    /// </summary>
    public interface IValidatableConfig
    {
        /// <summary>
        /// 验证配置
        /// </summary>
        /// <returns>验证结果</returns>
        ValidationResult Validate();
    }
}
