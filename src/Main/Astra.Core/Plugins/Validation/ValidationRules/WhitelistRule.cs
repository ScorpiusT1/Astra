using Astra.Core.Plugins.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Astra.Core.Plugins.Validation.ValidationRules
{
	public class WhitelistRule : IValidationRule
	{
		private readonly HashSet<string> _whitelist;

		public WhitelistRule(IEnumerable<string> whitelist)
		{
			_whitelist = new HashSet<string>(whitelist ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
		}

		public Task<ValidationResult> ValidateAsync(PluginDescriptor descriptor)
		{
			var result = new ValidationResult { IsValid = true };

			if (_whitelist.Count == 0) return Task.FromResult(result);

			if (!_whitelist.Contains(descriptor.Id) && !_whitelist.Contains(Path.GetFileName(descriptor.AssemblyPath)))
			{
				result.IsValid = false;
				result.Errors.Add($"Plugin not in whitelist: {descriptor.Id}");
			}

			return Task.FromResult(result);
		}
	}
}

