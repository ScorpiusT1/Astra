using Addins.Core.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Addins.Validation
{
    public class VersionValidRule : IValidationRule
    {
        public Task<ValidationResult> ValidateAsync(PluginDescriptor descriptor)
        {
            var result = new ValidationResult { IsValid = true };

            if (descriptor.Version == null)
            {
                result.IsValid = false;
                result.Errors.Add("Plugin version is required");
            }

            return Task.FromResult(result);
        }
    }
}
