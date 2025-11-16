using Addins.Configuration;
using Addins.Core.Abstractions;
using Addins.Core.Lifecycle;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Addins.Management
{
    /// <summary>
    /// 插件管理接口 - 提供插件管理功能
    /// </summary>
    public interface IPluginManager
    {
        /// <summary>
        /// 获取所有插件信息
        /// </summary>
        Task<IEnumerable<PluginInfo>> GetAllPluginsAsync();

        /// <summary>
        /// 获取插件详细信息
        /// </summary>
        Task<PluginInfo> GetPluginInfoAsync(string pluginId);

        /// <summary>
        /// 启用插件
        /// </summary>
        Task<bool> EnablePluginAsync(string pluginId);

        /// <summary>
        /// 禁用插件
        /// </summary>
        Task<bool> DisablePluginAsync(string pluginId);

        /// <summary>
        /// 重新加载插件
        /// </summary>
        Task<bool> ReloadPluginAsync(string pluginId);

        /// <summary>
        /// 获取插件配置
        /// </summary>
        Task<Dictionary<string, object>> GetPluginConfigurationAsync(string pluginId);

        /// <summary>
        /// 更新插件配置
        /// </summary>
        Task<bool> UpdatePluginConfigurationAsync(string pluginId, Dictionary<string, object> config);

        /// <summary>
        /// 获取插件状态
        /// </summary>
        Task<PluginStatus> GetPluginStatusAsync(string pluginId);

        /// <summary>
        /// 获取插件性能统计
        /// </summary>
        Task<PluginPerformanceStats> GetPluginPerformanceStatsAsync(string pluginId);
    }

    /// <summary>
    /// 插件信息
    /// </summary>
    public class PluginInfo
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Version { get; set; }
        public string Author { get; set; }
        public string Description { get; set; }
        public PluginStatus Status { get; set; }
        public DateTime LoadedTime { get; set; }
        public string IconPath { get; set; }
        public Dictionary<string, object> Properties { get; set; }
        public List<string> Dependencies { get; set; }
    }

    /// <summary>
    /// 插件状态
    /// </summary>
    public enum PluginStatus
    {
        Unknown,
        Discovered,
        Loading,
        Loaded,
        Initializing,
        Running,
        Stopping,
        Stopped,
        Failed,
        Unloading,
        Disabled
    }

    /// <summary>
    /// 插件性能统计
    /// </summary>
    public class PluginPerformanceStats
    {
        public string PluginId { get; set; }
        public DateTime StartTime { get; set; }
        public TimeSpan Uptime { get; set; }
        public long MemoryUsage { get; set; }
        public int ThreadCount { get; set; }
        public int ErrorCount { get; set; }
        public int RestartCount { get; set; }
        public DateTime LastErrorTime { get; set; }
        public string LastError { get; set; }
        public Dictionary<string, object> CustomMetrics { get; set; }
    }

    /// <summary>
    /// 插件管理实现
    /// </summary>
    public class PluginManager : IPluginManager
    {
        private readonly IPluginHost _host;
        private readonly PluginLifecycleManager _lifecycleManager;
        private readonly IConfigurationStore _configStore;

        public PluginManager(
            IPluginHost host,
            PluginLifecycleManager lifecycleManager,
            IConfigurationStore configStore)
        {
            _host = host;
            _lifecycleManager = lifecycleManager;
            _configStore = configStore;
        }

        public async Task<IEnumerable<PluginInfo>> GetAllPluginsAsync()
        {
            return _host.LoadedPlugins.Select(p => new PluginInfo
            {
                Id = p.Id,
                Name = p.Name,
                Version = p.Version.ToString(),
                Status = GetPluginStatus(p.Id),
                LoadedTime = DateTime.UtcNow, // 实际应该从插件描述符获取
                Properties = new Dictionary<string, object>(),
                Dependencies = new List<string>()
            });
        }

        public async Task<PluginInfo> GetPluginInfoAsync(string pluginId)
        {
            var plugin = _host.LoadedPlugins.FirstOrDefault(p => p.Id == pluginId);
            if (plugin == null)
                return null;

            return new PluginInfo
            {
                Id = plugin.Id,
                Name = plugin.Name,
                Version = plugin.Version.ToString(),
                Status = GetPluginStatus(pluginId),
                LoadedTime = DateTime.UtcNow,
                Properties = new Dictionary<string, object>(),
                Dependencies = new List<string>()
            };
        }

        public async Task<bool> EnablePluginAsync(string pluginId)
        {
            try
            {
                var plugin = _host.LoadedPlugins.FirstOrDefault(p => p.Id == pluginId);
                if (plugin == null)
                    return false;

                await plugin.StartAsync();
                _configStore.Set($"Plugin:{pluginId}:Enabled", true);
                _configStore.Save();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> DisablePluginAsync(string pluginId)
        {
            try
            {
                var plugin = _host.LoadedPlugins.FirstOrDefault(p => p.Id == pluginId);
                if (plugin == null)
                    return false;

                await plugin.StopAsync();
                _configStore.Set($"Plugin:{pluginId}:Enabled", false);
                _configStore.Save();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> ReloadPluginAsync(string pluginId)
        {
            try
            {
                await _host.UnloadPluginAsync(pluginId);
                // 注意：这里需要插件路径信息，实际实现中可能需要从配置中获取
                // await _host.LoadPluginAsync(pluginPath);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<Dictionary<string, object>> GetPluginConfigurationAsync(string pluginId)
        {
            var config = new Dictionary<string, object>
            {
                ["Enabled"] = _configStore.Get($"Plugin:{pluginId}:Enabled", true),
                ["AutoStart"] = _configStore.Get($"Plugin:{pluginId}:AutoStart", false),
                ["LogLevel"] = _configStore.Get($"Plugin:{pluginId}:LogLevel", "Info")
            };

            return config;
        }

        public async Task<bool> UpdatePluginConfigurationAsync(string pluginId, Dictionary<string, object> config)
        {
            try
            {
                foreach (var kvp in config)
                {
                    _configStore.Set($"Plugin:{pluginId}:{kvp.Key}", kvp.Value);
                }
                _configStore.Save();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<PluginStatus> GetPluginStatusAsync(string pluginId)
        {
            return GetPluginStatus(pluginId);
        }

        public async Task<PluginPerformanceStats> GetPluginPerformanceStatsAsync(string pluginId)
        {
            var state = _lifecycleManager.GetState(pluginId);
            var stats = _lifecycleManager.ResourceTracker.GetStatistics(pluginId);

            return new PluginPerformanceStats
            {
                PluginId = pluginId,
                StartTime = state?.LastTransition ?? DateTime.UtcNow,
                Uptime = DateTime.UtcNow - (state?.LastTransition ?? DateTime.UtcNow),
                MemoryUsage = 0, // 实际应该从系统获取
                ThreadCount = 0, // 实际应该从系统获取
                ErrorCount = state?.ErrorCount ?? 0,
                RestartCount = state?.StartCount ?? 0,
                LastErrorTime = state?.LastError != null ? state.LastTransition : DateTime.MinValue,
                LastError = state?.LastError?.Message,
                CustomMetrics = new Dictionary<string, object>
                {
                    ["ResourceCount"] = stats.ResourceCount,
                    ["CancellationTokenCount"] = stats.CancellationTokenCount,
                    ["BackgroundTaskCount"] = stats.BackgroundTaskCount
                }
            };
        }

        private PluginStatus GetPluginStatus(string pluginId)
        {
            var state = _lifecycleManager.GetState(pluginId);
            if (state == null)
                return PluginStatus.Unknown;

            return state.Phase switch
            {
                LifecyclePhase.Created => PluginStatus.Discovered,
                LifecyclePhase.Initializing => PluginStatus.Initializing,
                LifecyclePhase.Initialized => PluginStatus.Loaded,
                LifecyclePhase.Starting => PluginStatus.Loading,
                LifecyclePhase.Running => PluginStatus.Running,
                LifecyclePhase.Stopping => PluginStatus.Stopping,
                LifecyclePhase.Stopped => PluginStatus.Stopped,
                LifecyclePhase.Disposing => PluginStatus.Unloading,
                LifecyclePhase.Disposed => PluginStatus.Stopped,
                LifecyclePhase.Failed => PluginStatus.Failed,
                _ => PluginStatus.Unknown
            };
        }
    }
}
