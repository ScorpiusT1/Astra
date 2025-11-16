using Astra.Core.Plugins.Configuration;
using Astra.Core.Plugins.Abstractions;
using Astra.Core.Plugins.Lifecycle;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Astra.Core.Plugins.Management.Commands
{
    /// <summary>
    /// 插件管理器实现
    /// </summary>
    public class PluginAdministrator : IPluginAdministrator
    {
        private readonly IPluginHost _host;
        private readonly IConfigurationStore _configStore;
        private readonly PluginLifecycleManager _lifecycleManager;

        public PluginAdministrator(
            IPluginHost host,
            IConfigurationStore configStore,
            PluginLifecycleManager lifecycleManager)
        {
            _host = host;
            _configStore = configStore;
            _lifecycleManager = lifecycleManager;
        }

        public async Task<List<PluginInfo>> GetAllPluginsAsync()
        {
            return _host.LoadedPlugins.Select(p => new PluginInfo
            {
                Id = p.Id,
                Name = p.Name,
                Version = p.Version.ToString(),
                State = _lifecycleManager.GetState(p.Id)?.Phase.ToString(),
                IsEnabled = _lifecycleManager.GetState(p.Id)?.Phase == LifecyclePhase.Running
            }).ToList();
        }

        public async Task EnablePluginAsync(string pluginId)
        {
            var plugin = _host.LoadedPlugins.FirstOrDefault(p => p.Id == pluginId);
            if (plugin == null)
                throw new InvalidOperationException($"Plugin not found: {pluginId}");

			await plugin.OnEnableAsync();
            _configStore.Set($"Plugin:{pluginId}:Enabled", true);
            _configStore.Save();
        }

        public async Task DisablePluginAsync(string pluginId)
        {
            var plugin = _host.LoadedPlugins.FirstOrDefault(p => p.Id == pluginId);
            if (plugin == null)
                throw new InvalidOperationException($"Plugin not found: {pluginId}");

			await plugin.OnDisableAsync();
            _configStore.Set($"Plugin:{pluginId}:Enabled", false);
            _configStore.Save();
        }

        public async Task UpdateConfigurationAsync(string pluginId, Dictionary<string, object> config)
        {
            foreach (var kvp in config)
            {
                _configStore.Set($"Plugin:{pluginId}:{kvp.Key}", kvp.Value);
            }
            _configStore.Save();

            await Task.CompletedTask;
        }

        public async Task<Dictionary<string, object>> GetConfigurationAsync(string pluginId)
        {
            // 简化实现，实际应该遍历所有相关配置
            var config = new Dictionary<string, object>
            {
                ["Enabled"] = _configStore.Get($"Plugin:{pluginId}:Enabled", true)
            };

            return await Task.FromResult(config);
        }

        public async Task<CommandResult> ExecuteCommandAsync(AdminCommand command)
        {
            try
            {
                switch (command.Type)
                {
                    case CommandType.Start:
                        await EnablePluginAsync(command.PluginId);
                        return CommandResult.Success($"Plugin {command.PluginId} started");

                    case CommandType.Stop:
                        await DisablePluginAsync(command.PluginId);
                        return CommandResult.Success($"Plugin {command.PluginId} stopped");

                    case CommandType.Reload:
                        await _host.UnloadPluginAsync(command.PluginId);
                        await _host.LoadPluginAsync(command.Parameters["path"] as string);
                        return CommandResult.Success($"Plugin {command.PluginId} reloaded");

                    case CommandType.Configure:
                        await UpdateConfigurationAsync(command.PluginId, command.Parameters);
                        return CommandResult.Success($"Plugin {command.PluginId} configured");

                    default:
                        return CommandResult.Failure($"Unknown command type: {command.Type}");
                }
            }
            catch (Exception ex)
            {
                return CommandResult.Failure($"Command failed: {ex.Message}");
            }
        }
    }
}
