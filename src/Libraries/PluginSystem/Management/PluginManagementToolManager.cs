using Addins.Configuration;
using Addins.Core.Abstractions;
using Addins.Core.Lifecycle;
using Addins.Messaging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Addins.Management
{
    /// <summary>
    /// 插件管理工具管理器
    /// </summary>
    public class PluginManagementToolManager
    {
        private readonly Dictionary<string, IPluginManagementTool> _tools = new();

        /// <summary>
        /// 注册管理工具
        /// </summary>
        public void RegisterTool(IPluginManagementTool tool)
        {
            _tools[tool.Name] = tool;
        }

        /// <summary>
        /// 取消注册管理工具
        /// </summary>
        public void UnregisterTool(string name)
        {
            _tools.Remove(name);
        }

        /// <summary>
        /// 获取所有注册的工具
        /// </summary>
        public IEnumerable<IPluginManagementTool> GetTools()
        {
            return _tools.Values;
        }

        /// <summary>
        /// 执行管理工具
        /// </summary>
        public async Task<bool> ExecuteToolAsync(string name, string[] args)
        {
            if (!_tools.TryGetValue(name, out var tool))
            {
                Console.WriteLine($"Unknown tool: {name}");
                Console.WriteLine("Available tools:");
                foreach (var availableTool in _tools.Values)
                {
                    Console.WriteLine($"  {availableTool.Name} - {availableTool.Description}");
                }
                return false;
            }

            try
            {
                await tool.ExecuteAsync(args);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error executing tool {name}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 显示工具帮助
        /// </summary>
        public void ShowToolHelp(string name)
        {
            if (_tools.TryGetValue(name, out var tool))
            {
                tool.ShowHelp();
            }
            else
            {
                Console.WriteLine($"Unknown tool: {name}");
            }
        }

        /// <summary>
        /// 显示所有工具列表
        /// </summary>
        public void ShowAllTools()
        {
            Console.WriteLine("Available Plugin Management Tools:");
            Console.WriteLine("==================================");
            
            foreach (var tool in _tools.Values.OrderBy(t => t.Name))
            {
                Console.WriteLine($"  {tool.Name,-15} - {tool.Description}");
            }
            
            Console.WriteLine("\nUsage: tool <name> [args...]");
            Console.WriteLine("       tool <name> help");
        }
    }

    /// <summary>
    /// 插件管理工具工厂
    /// </summary>
    public static class PluginManagementToolFactory
    {
        /// <summary>
        /// 创建默认的管理工具集合
        /// </summary>
        public static PluginManagementToolManager CreateDefaultManager(
            IPluginHost host,
            PluginLifecycleManager lifecycleManager,
            IMessageBus messageBus,
            IConfigurationStore configStore)
        {
            var manager = new PluginManagementToolManager();

            // 注册默认工具
            manager.RegisterTool(new PluginDebugTool(host, lifecycleManager, messageBus));
            manager.RegisterTool(new PluginConfigTool(host, configStore));

            return manager;
        }
    }
}
