using Astra.Core.Plugins.Abstractions;
using Astra.Core.Plugins.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Astra.Core.Plugins.Loading
{
    /// <summary>
    /// 基于程序集的插件加载器
    /// </summary>
    public class AssemblyPluginLoader : IPluginLoader
    {
        private readonly ConcurrentDictionary<string, PluginAssemblyLoadContext> _loadContexts = new();
        private readonly ConcurrentDictionary<string, PluginDescriptor> _descriptors = new();
        private readonly IPluginContext _pluginContext;

        /// <summary>
        /// 构造基于程序集的插件加载器。
        /// </summary>
        /// <param name="pluginContext">提供给插件的上下文</param>
        public AssemblyPluginLoader(IPluginContext pluginContext)
        {
            _pluginContext = pluginContext;
        }

        /// <summary>
        /// 以隔离上下文加载插件并初始化，但不启用。
        /// </summary>
        /// <param name="descriptor">插件描述符</param>
        /// <returns>已创建的插件实例</returns>
        public async Task<IPlugin> LoadAsync(PluginDescriptor descriptor)
        {
            if (!File.Exists(descriptor.AssemblyPath))
                throw new FileNotFoundException($"Assembly not found: {descriptor.AssemblyPath}");

            // 创建隔离的加载上下文
			var loadContext = new PluginAssemblyLoadContext(descriptor.AssemblyPath, isCollectible: true);
            _loadContexts[descriptor.Id] = loadContext;
            _descriptors[descriptor.Id] = descriptor;

            // 加载程序集
            var assembly = loadContext.LoadFromAssemblyPath(descriptor.AssemblyPath);

            // 查找插件类型
            Type pluginType;
            if (!string.IsNullOrEmpty(descriptor.TypeName))
            {
                pluginType = assembly.GetType(descriptor.TypeName);
            }
            else
            {
                // 自动查找实现 IPlugin 的类型
                pluginType = assembly.GetTypes()
                    .FirstOrDefault(t => typeof(IPlugin).IsAssignableFrom(t) && !t.IsAbstract);
            }

            if (pluginType == null)
                throw new InvalidOperationException($"Plugin type not found in {descriptor.AssemblyPath}");

            // 创建插件实例
			var plugin = PluginActivatorFactory.Create(pluginType);
            if (plugin == null)
                throw new InvalidOperationException($"Failed to create plugin instance: {pluginType}");

            // 初始化插件
            await plugin.InitializeAsync(_pluginContext, CancellationToken.None);

            return plugin;
        }

        /// <summary>
        /// 卸载指定插件的加载上下文，并触发 GC 回收。
        /// </summary>
        /// <param name="pluginId">插件标识</param>
        public async Task UnloadAsync(string pluginId)
        {
            if (_loadContexts.TryRemove(pluginId, out var loadContext))
            {
                // 等待垃圾回收
                loadContext.Unload();

                // 触发 GC 确保卸载
                for (int i = 0; i < 3; i++)
                {
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    await Task.Delay(100);
                }
            }

            _descriptors.TryRemove(pluginId, out _);
        }

        /// <summary>
        /// 重新加载指定插件：先卸载再加载。
        /// </summary>
        public async Task<IPlugin> ReloadAsync(string pluginId)
        {
            if (!_descriptors.TryGetValue(pluginId, out var descriptor))
                throw new InvalidOperationException($"Plugin not found: {pluginId}");

            await UnloadAsync(pluginId);
            return await LoadAsync(descriptor);
        }

        /// <summary>
        /// 获取已加载插件的上下文（若存在）。
        /// </summary>
        public PluginAssemblyLoadContext GetLoadContext(string pluginId)
        {
            return _loadContexts.TryGetValue(pluginId, out var context) ? context : null;
        }
    }
}
