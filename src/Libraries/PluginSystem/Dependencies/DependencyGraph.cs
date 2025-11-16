using Addins.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Addins.Dependencies
{
    /// <summary>
    /// 依赖图 - 用于插件依赖关系管理和拓扑排序
    /// </summary>
    public class DependencyGraph
    {
        private readonly Dictionary<string, PluginDescriptor> _plugins = new();
        private readonly Dictionary<string, List<string>> _dependencies = new();
        private readonly Dictionary<string, List<string>> _dependents = new();

        /// <summary>
        /// 添加插件到图中
        /// </summary>
        public void AddPlugin(PluginDescriptor plugin)
        {
            _plugins[plugin.Id] = plugin;
            _dependencies[plugin.Id] = new List<string>();
            _dependents[plugin.Id] = new List<string>();
        }

        /// <summary>
        /// 添加依赖关系
        /// </summary>
        public void AddDependency(string pluginId, string dependencyId)
        {
            if (!_dependencies.ContainsKey(pluginId))
                _dependencies[pluginId] = new List<string>();

            if (!_dependents.ContainsKey(dependencyId))
                _dependents[dependencyId] = new List<string>();

            if (!_dependencies[pluginId].Contains(dependencyId))
                _dependencies[pluginId].Add(dependencyId);

            if (!_dependents[dependencyId].Contains(pluginId))
                _dependents[dependencyId].Add(pluginId);
        }

        /// <summary>
        /// 拓扑排序 - 返回按依赖顺序排列的插件列表
        /// </summary>
        public List<PluginDescriptor> TopologicalSort()
        {
            var result = new List<PluginDescriptor>();
            var visited = new HashSet<string>();
            var visiting = new HashSet<string>();

            foreach (var pluginId in _plugins.Keys)
            {
                if (!visited.Contains(pluginId))
                {
                    Visit(pluginId, visited, visiting, result);
                }
            }

            return result;
        }

        /// <summary>
        /// 检查是否存在循环依赖
        /// </summary>
        public bool HasCycle()
        {
            var visited = new HashSet<string>();
            var visiting = new HashSet<string>();

            foreach (var pluginId in _plugins.Keys)
            {
                if (!visited.Contains(pluginId))
                {
                    if (HasCycleDFS(pluginId, visited, visiting))
                        return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 获取插件的直接依赖
        /// </summary>
        public List<PluginDescriptor> GetDependencies(string pluginId)
        {
            if (!_dependencies.TryGetValue(pluginId, out var dependencyIds))
                return new List<PluginDescriptor>();

            return dependencyIds
                .Where(id => _plugins.ContainsKey(id))
                .Select(id => _plugins[id])
                .ToList();
        }

        /// <summary>
        /// 获取依赖此插件的所有插件
        /// </summary>
        public List<PluginDescriptor> GetDependents(string pluginId)
        {
            if (!_dependents.TryGetValue(pluginId, out var dependentIds))
                return new List<PluginDescriptor>();

            return dependentIds
                .Where(id => _plugins.ContainsKey(id))
                .Select(id => _plugins[id])
                .ToList();
        }

        /// <summary>
        /// 获取所有插件
        /// </summary>
        public List<PluginDescriptor> GetAllPlugins()
        {
            return _plugins.Values.ToList();
        }

        /// <summary>
        /// 移除插件及其所有依赖关系
        /// </summary>
        public void RemovePlugin(string pluginId)
        {
            if (!_plugins.ContainsKey(pluginId))
                return;

            // 移除所有依赖此插件的依赖关系
            if (_dependents.TryGetValue(pluginId, out var dependents))
            {
                foreach (var dependent in dependents)
                {
                    _dependencies[dependent]?.Remove(pluginId);
                }
            }

            // 移除此插件的所有依赖关系
            if (_dependencies.TryGetValue(pluginId, out var dependencies))
            {
                foreach (var dependency in dependencies)
                {
                    _dependents[dependency]?.Remove(pluginId);
                }
            }

            _plugins.Remove(pluginId);
            _dependencies.Remove(pluginId);
            _dependents.Remove(pluginId);
        }

        private void Visit(string pluginId, HashSet<string> visited, HashSet<string> visiting, List<PluginDescriptor> result)
        {
            if (visiting.Contains(pluginId))
                throw new InvalidOperationException($"Circular dependency detected involving plugin: {pluginId}");

            if (visited.Contains(pluginId))
                return;

            visiting.Add(pluginId);

            if (_dependencies.TryGetValue(pluginId, out var dependencies))
            {
                foreach (var dependency in dependencies)
                {
                    Visit(dependency, visited, visiting, result);
                }
            }

            visiting.Remove(pluginId);
            visited.Add(pluginId);

            if (_plugins.TryGetValue(pluginId, out var plugin))
            {
                result.Add(plugin);
            }
        }

        private bool HasCycleDFS(string pluginId, HashSet<string> visited, HashSet<string> visiting)
        {
            if (visiting.Contains(pluginId))
                return true;

            if (visited.Contains(pluginId))
                return false;

            visiting.Add(pluginId);

            if (_dependencies.TryGetValue(pluginId, out var dependencies))
            {
                foreach (var dependency in dependencies)
                {
                    if (HasCycleDFS(dependency, visited, visiting))
                        return true;
                }
            }

            visiting.Remove(pluginId);
            visited.Add(pluginId);

            return false;
        }
    }
}