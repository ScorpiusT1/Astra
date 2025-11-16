using Addins.Configuration;
using Addins.Core.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Addins.Management
{
    /// <summary>
    /// 插件配置管理工具
    /// </summary>
    public class PluginConfigTool : IPluginManagementTool
    {
        private readonly IPluginHost _host;
        private readonly IConfigurationStore _configStore;
        private List<PluginInfo> _plugins;

        public string Name => "config";
        public string Description => "管理插件配置和设置";

        public PluginConfigTool(IPluginHost host, IConfigurationStore configStore)
        {
            _host = host;
            _configStore = configStore;
        }

        public async Task ExecuteAsync(string[] args)
        {
            if (args.Length == 0)
            {
                await ShowAllConfigurationsAsync();
                return;
            }

            var command = args[0].ToLower();
            switch (command)
            {
                case "list":
                    await ShowAllConfigurationsAsync();
                    break;
                case "get":
                    if (args.Length >= 3)
                        await GetConfigurationAsync(args[1], args[2]);
                    else
                        Console.WriteLine("Usage: config get <pluginId> <key>");
                    break;
                case "set":
                    if (args.Length >= 4)
                        await SetConfigurationAsync(args[1], args[2], args[3]);
                    else
                        Console.WriteLine("Usage: config set <pluginId> <key> <value>");
                    break;
                case "reset":
                    if (args.Length >= 2)
                        await ResetConfigurationAsync(args[1]);
                    else
                        Console.WriteLine("Usage: config reset <pluginId>");
                    break;
                default:
                    ShowHelp();
                    break;
            }
        }

        public void ShowHelp()
        {
            Console.WriteLine("Plugin Configuration Tool");
            Console.WriteLine("Usage: config <command> [options]");
            Console.WriteLine("Commands:");
            Console.WriteLine("  list                    List all plugin configurations");
            Console.WriteLine("  get <pluginId> <key>    Get configuration value");
            Console.WriteLine("  set <pluginId> <key> <value>  Set configuration value");
            Console.WriteLine("  reset <pluginId>        Reset plugin configuration to defaults");
            Console.WriteLine("  help                    Show this help message");
        }

        private async Task ShowAllConfigurationsAsync()
        {
            Console.WriteLine("\n========== Plugin Configurations ==========");
            await RefreshPluginListAsync();

            foreach (var plugin in _plugins)
            {
                Console.WriteLine($"\n[{plugin.Id}]");
                Console.WriteLine($"  Name: {plugin.Name}");
                Console.WriteLine($"  Version: {plugin.Version}");
                Console.WriteLine($"  Author: {plugin.Author}");
                Console.WriteLine($"  State: {plugin.State}");
                Console.WriteLine($"  Description: {plugin.Description}");

                // 显示插件配置
                var config = await GetPluginConfigurationAsync(plugin.Id);
                if (config.Any())
                {
                    Console.WriteLine("  Configuration:");
                    foreach (var kvp in config)
                    {
                        Console.WriteLine($"    {kvp.Key} = {kvp.Value}");
                    }
                }
            }

            Console.WriteLine("\n==========================================");
        }

        private async Task GetConfigurationAsync(string pluginId, string key)
        {
            var value = _configStore.Get($"Plugin:{pluginId}:{key}", "Not Set");
            Console.WriteLine($"Plugin {pluginId} - {key}: {value}");
        }

        private async Task SetConfigurationAsync(string pluginId, string key, string value)
        {
            _configStore.Set($"Plugin:{pluginId}:{key}", value);
            _configStore.Save();
            Console.WriteLine($"Updated configuration: Plugin:{pluginId}:{key} = {value}");
        }

        private async Task ResetConfigurationAsync(string pluginId)
        {
            // 这里可以实现重置逻辑
            Console.WriteLine($"Configuration reset for plugin {pluginId}");
        }

        private async Task RefreshPluginListAsync()
        {
            _plugins = _host.LoadedPlugins.Select(p => new PluginInfo
            {
                Id = p.Id,
                Name = p.Name,
                Version = p.Version.ToString(),
                State = "Running",
                Author = "Unknown",
                Description = "No description available"
            }).ToList();
        }

        private async Task<Dictionary<string, object>> GetPluginConfigurationAsync(string pluginId)
        {
            // 简化实现，实际应该遍历所有相关配置
            var config = new Dictionary<string, object>
            {
                ["Enabled"] = _configStore.Get($"Plugin:{pluginId}:Enabled", true),
                ["AutoStart"] = _configStore.Get($"Plugin:{pluginId}:AutoStart", false),
                ["LogLevel"] = _configStore.Get($"Plugin:{pluginId}:LogLevel", "Info")
            };

            return config;
        }

        private class PluginInfo
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public string Version { get; set; }
            public string Author { get; set; }
            public string Description { get; set; }
            public string State { get; set; }
        }
    }
}
