using Astra.Core.Plugins.Models;

namespace Astra.Core.Plugins.Validation
{
    /// <summary>
    /// 验证规则：针对单个 <see cref="PluginDescriptor"/> 的原子校验单元。
    /// </summary>
    public interface IValidationRule
    {
        /// <summary>
        /// 执行规则校验并返回结果。
        /// </summary>
        Task<ValidationResult> ValidateAsync(PluginDescriptor descriptor);
    }
}
