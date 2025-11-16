using Astra.Core.Plugins.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Astra.Core.Plugins.Dependencies
{
    /// <summary>
    /// 依赖冲突解析器
    /// </summary>
    public class ConflictResolver
    {
        private readonly VersionResolver _versionResolver;

        public ConflictResolver()
        {
            _versionResolver = new VersionResolver();
        }

        /// <summary>
        /// 解析依赖冲突
        /// </summary>
        public Dictionary<string, PluginDescriptor> ResolveConflicts(
            List<PluginDescriptor> allPlugins,
            ConflictResolutionStrategy strategy = ConflictResolutionStrategy.HighestVersion)
        {
            var resolved = new Dictionary<string, PluginDescriptor>();
            var grouped = allPlugins.GroupBy(p => p.Id);

            foreach (var group in grouped)
            {
                if (group.Count() == 1)
                {
                    resolved[group.Key] = group.First();
                    continue;
                }

                // 多个版本存在冲突
                var selected = strategy switch
                {
                    ConflictResolutionStrategy.HighestVersion => group.OrderByDescending(p => p.Version).First(),
                    ConflictResolutionStrategy.LowestVersion => group.OrderBy(p => p.Version).First(),
                    ConflictResolutionStrategy.MostDependents => SelectMostDepended(group.ToList(), allPlugins),
                    ConflictResolutionStrategy.Explicit => throw new InvalidOperationException("Explicit strategy requires manual selection"),
                    _ => group.First()
                };

                resolved[group.Key] = selected;
            }

            return resolved;
        }

        /// <summary>
        /// 检测冲突
        /// </summary>
        public List<DependencyConflict> DetectConflicts(List<PluginDescriptor> plugins)
        {
            var conflicts = new List<DependencyConflict>();

            // 按 ID 分组
            var grouped = plugins.GroupBy(p => p.Id);

            foreach (var group in grouped)
            {
                if (group.Count() > 1)
                {
                    conflicts.Add(new DependencyConflict
                    {
                        PluginId = group.Key,
                        ConflictingVersions = group.Select(p => p.Version).ToList(),
                        ConflictType = ConflictType.MultipleVersions
                    });
                }
            }

            // 检查依赖版本冲突
            foreach (var plugin in plugins)
            {
                foreach (var dep in plugin.Dependencies)
                {
                    var availableVersions = plugins
                        .Where(p => p.Id == dep.PluginId)
                        .Select(p => p.Version)
                        .ToList();

                    var compatibleVersions = availableVersions
                        .Where(v => dep.VersionRange.IsInRange(v))
                        .ToList();

                    if (!compatibleVersions.Any() && availableVersions.Any())
                    {
                        conflicts.Add(new DependencyConflict
                        {
                            PluginId = plugin.Id,
                            ConflictingVersions = availableVersions,
                            ConflictType = ConflictType.IncompatibleVersion,
                            DependencyId = dep.PluginId,
                            RequiredVersionRange = dep.VersionRange
                        });
                    }
                }
            }

            return conflicts;
        }

        private PluginDescriptor SelectMostDepended(List<PluginDescriptor> candidates, List<PluginDescriptor> allPlugins)
        {
            var dependencyCounts = candidates.ToDictionary(
                c => c,
                c => allPlugins.Count(p => p.Dependencies.Any(d => d.PluginId == c.Id && d.VersionRange.IsInRange(c.Version)))
            );

            return dependencyCounts.OrderByDescending(kvp => kvp.Value).First().Key;
        }
    }

    /// <summary>
    /// 冲突解析策略
    /// </summary>
    public enum ConflictResolutionStrategy
    {
        HighestVersion,      // 选择最高版本
        LowestVersion,       // 选择最低版本
        MostDependents,      // 选择被依赖最多的版本
        Explicit             // 需要显式指定
    }

    /// <summary>
    /// 依赖冲突信息
    /// </summary>
    public class DependencyConflict
    {
        public string PluginId { get; set; }
        public List<Version> ConflictingVersions { get; set; }
        public ConflictType ConflictType { get; set; }
        public string DependencyId { get; set; }
        public VersionRange RequiredVersionRange { get; set; }

        public override string ToString()
        {
            return ConflictType switch
            {
                ConflictType.MultipleVersions =>
                    $"Multiple versions of {PluginId}: {string.Join(", ", ConflictingVersions)}",
                ConflictType.IncompatibleVersion =>
                    $"{PluginId} requires {DependencyId} {RequiredVersionRange.MinVersion}-{RequiredVersionRange.MaxVersion}, " +
                    $"but available: {string.Join(", ", ConflictingVersions)}",
                _ => $"Unknown conflict for {PluginId}"
            };
        }
    }

    public enum ConflictType
    {
        MultipleVersions,
        IncompatibleVersion,
        MissingDependency
    }
}
