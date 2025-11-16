using Addins.Core.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Addins.Validation
{
    public class AssemblyExistsRule : IValidationRule
    {
        public Task<ValidationResult> ValidateAsync(PluginDescriptor descriptor)
        {
            var result = new ValidationResult();

            if (!File.Exists(descriptor.AssemblyPath))
            {
                result.IsValid = false;
                result.Errors.Add($"Assembly not found: {descriptor.AssemblyPath}");
            }
            else
            {
                result.IsValid = true;
            }

            return Task.FromResult(result);
        }
    }
}
