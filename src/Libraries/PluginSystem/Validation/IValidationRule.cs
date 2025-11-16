using Addins.Core.Models;

namespace Addins.Validation
{
    public interface IValidationRule
    {
        Task<ValidationResult> ValidateAsync(PluginDescriptor descriptor);
    }
}
