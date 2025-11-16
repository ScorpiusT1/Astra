using Astra.Core.Plugins.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Astra.Core.Plugins.Dependencies
{
    /// <summary>
    /// 版本冲突解析器
    /// </summary>
    public class VersionResolver
    {
        public enum ConflictStrategy
        {
            HighestVersion,
            LowestVersion,
            Strict,
            Compatible
        }

        public ConflictStrategy Strategy { get; set; } = ConflictStrategy.HighestVersion;

        public PluginDescriptor ResolveConflict(
            List<PluginDescriptor> candidates,
            List<DependencyInfo> requirements)
        {
            if (candidates == null || !candidates.Any())
                throw new ArgumentException("No candidates provided");

            var compatible = candidates
                .Where(c => requirements.All(r => r.VersionRange.IsInRange(c.Version)))
                .ToList();

            if (!compatible.Any())
            {
                if (Strategy == ConflictStrategy.Strict)
                {
                    throw new InvalidOperationException(
                        $"No compatible version found for requirements: " +
                        $"{string.Join(", ", requirements.Select(r => r.PluginId))}");
                }
                compatible = candidates;
            }

            return Strategy switch
            {
                ConflictStrategy.HighestVersion => compatible.OrderByDescending(c => c.Version).First(),
                ConflictStrategy.LowestVersion => compatible.OrderBy(c => c.Version).First(),
                ConflictStrategy.Compatible => FindMostCompatible(compatible, requirements),
                _ => compatible.First()
            };
        }

        private PluginDescriptor FindMostCompatible(
            List<PluginDescriptor> candidates,
            List<DependencyInfo> requirements)
        {
            return candidates
                .OrderByDescending(c => requirements.Count(r => r.VersionRange.IsInRange(c.Version)))
                .ThenByDescending(c => c.Version)
                .First();
        }
    }
}
