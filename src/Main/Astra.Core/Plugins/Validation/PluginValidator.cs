using Astra.Core.Plugins.Models;

namespace Astra.Core.Plugins.Validation
{
    /// <summary>
    /// 插件验证器：按顺序执行一系列 <see cref="IValidationRule"/> 并汇总结果。
    /// </summary>
    public class PluginValidator : IPluginValidator
    {
        private readonly List<IValidationRule> _rules = new();

        /// <summary>
        /// 默认构造，注册基础规则（存在/依赖/版本）。
        /// </summary>
        public PluginValidator()
        {
            _rules.Add(new AssemblyExistsRule());
            _rules.Add(new DependencyValidRule());
            _rules.Add(new VersionValidRule());
        }

        /// <summary>
        /// 动态添加一条验证规则。
        /// </summary>
        public void AddRule(IValidationRule rule)
        {
            _rules.Add(rule);
        }

        /// <summary>
        /// 对指定描述符执行所有规则验证并返回汇总结果。
        /// </summary>
        public async Task<ValidationResult> ValidateAsync(PluginDescriptor descriptor)
        {
            var result = new ValidationResult { IsValid = true };

            foreach (var rule in _rules)
            {
                var ruleResult = await rule.ValidateAsync(descriptor);
                if (!ruleResult.IsValid)
                {
                    result.IsValid = false;
                    result.Errors.AddRange(ruleResult.Errors);
                }
            }

            return result;
        }
    }
}
