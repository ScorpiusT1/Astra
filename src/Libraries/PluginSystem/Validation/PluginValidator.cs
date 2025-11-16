using Addins.Core.Models;

namespace Addins.Validation
{
    public class PluginValidator : IPluginValidator
    {
        private readonly List<IValidationRule> _rules = new();

        public PluginValidator()
        {
            _rules.Add(new AssemblyExistsRule());
            _rules.Add(new DependencyValidRule());
            _rules.Add(new VersionValidRule());
        }

        public void AddRule(IValidationRule rule)
        {
            _rules.Add(rule);
        }

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
