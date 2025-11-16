using Addins.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Addins.Validation.ValidationRules
{
    /// <summary>
    /// 依赖关系验证器
    /// </summary>
    public class DependencyValidator : IValidationRule
    {
        private readonly Dictionary<string, PluginDescriptor> _availablePlugins;

        public DependencyValidator(IEnumerable<PluginDescriptor> availablePlugins = null)
        {
            _availablePlugins = availablePlugins?.ToDictionary(p => p.Id) ?? new Dictionary<string, PluginDescriptor>();
        }

        public Task<ValidationResult> ValidateAsync(PluginDescriptor descriptor)
        {
            var result = new ValidationResult { IsValid = true };

            foreach (var dependency in descriptor.Dependencies)
            {
                // 检查必需依赖是否存在
                if (!dependency.IsOptional && !_availablePlugins.ContainsKey(dependency.PluginId))
                {
                    result.IsValid = false;
                    result.Errors.Add($"Required dependency not found: {dependency.PluginId}");
                    continue;
                }

                // 检查版本兼容性
                if (_availablePlugins.TryGetValue(dependency.PluginId, out var availablePlugin))
                {
                    if (!dependency.VersionRange.IsInRange(availablePlugin.Version))
                    {
                        result.IsValid = false;
                        result.Errors.Add(
                            $"Dependency version mismatch: {dependency.PluginId} " +
                            $"(required: {dependency.VersionRange.MinVersion}-{dependency.VersionRange.MaxVersion}, " +
                            $"found: {availablePlugin.Version})");
                    }
                }
            }

            // 检查循环依赖
            if (HasCircularDependency(descriptor, new HashSet<string>()))
            {
                result.IsValid = false;
                result.Errors.Add("Circular dependency detected");
            }

            return Task.FromResult(result);
        }

        private bool HasCircularDependency(PluginDescriptor plugin, HashSet<string> visited)
        {
            if (visited.Contains(plugin.Id))
                return true;

            visited.Add(plugin.Id);

            foreach (var dep in plugin.Dependencies)
            {
                if (_availablePlugins.TryGetValue(dep.PluginId, out var depPlugin))
                {
                    if (HasCircularDependency(depPlugin, new HashSet<string>(visited)))
                        return true;
                }
            }

            return false;
        }
    }
}
