using Addins.Core.Abstractions;
using Addins.Core.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Addins.Core.Loading
{
    /// <summary>
    /// 基于程序集的插件加载器
    /// </summary>
    public class AssemblyPluginLoader : IPluginLoader
    {
        private readonly ConcurrentDictionary<string, PluginLoadContext> _loadContexts = new();
        private readonly ConcurrentDictionary<string, PluginDescriptor> _descriptors = new();
        private readonly IPluginContext _pluginContext;

        public AssemblyPluginLoader(IPluginContext pluginContext)
        {
            _pluginContext = pluginContext;
        }

        public async Task<IPlugin> LoadAsync(PluginDescriptor descriptor)
        {
            if (!File.Exists(descriptor.AssemblyPath))
                throw new FileNotFoundException($"Assembly not found: {descriptor.AssemblyPath}");

            // 创建隔离的加载上下文
            var loadContext = new PluginLoadContext(descriptor.AssemblyPath, isCollectible: true);
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
            var plugin = Activator.CreateInstance(pluginType) as IPlugin;
            if (plugin == null)
                throw new InvalidOperationException($"Failed to create plugin instance: {pluginType}");

            // 初始化插件
            await plugin.InitializeAsync(_pluginContext);

            return plugin;
        }

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

        public async Task<IPlugin> ReloadAsync(string pluginId)
        {
            if (!_descriptors.TryGetValue(pluginId, out var descriptor))
                throw new InvalidOperationException($"Plugin not found: {pluginId}");

            await UnloadAsync(pluginId);
            return await LoadAsync(descriptor);
        }

        public PluginLoadContext GetLoadContext(string pluginId)
        {
            return _loadContexts.TryGetValue(pluginId, out var context) ? context : null;
        }
    }
}
