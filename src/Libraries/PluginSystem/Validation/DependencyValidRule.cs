using Addins.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Addins.Validation
{
    public class DependencyValidRule : IValidationRule
    {
        public Task<ValidationResult> ValidateAsync(PluginDescriptor descriptor)
        {
            var result = new ValidationResult { IsValid = true };

            foreach (var dep in descriptor.Dependencies.Where(d => !d.IsOptional))
            {
                if (string.IsNullOrWhiteSpace(dep.PluginId))
                {
                    result.IsValid = false;
                    result.Errors.Add("Required dependency has no ID");
                }
            }

            return Task.FromResult(result);
        }
    }
}
