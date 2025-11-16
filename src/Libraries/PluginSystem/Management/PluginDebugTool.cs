using Addins.Core.Abstractions;
using Addins.Core.Lifecycle;
using Addins.Messaging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Addins.Management
{
    /// <summary>
    /// 插件管理工具接口
    /// </summary>
    public interface IPluginManagementTool
    {
        string Name { get; }
        string Description { get; }
        Task ExecuteAsync(string[] args);
        void ShowHelp();
    }

    /// <summary>
    /// 插件调试工具
    /// </summary>
    public class PluginDebugTool : IPluginManagementTool
    {
        private readonly IPluginHost _host;
        private readonly PluginLifecycleManager _lifecycleManager;
        private readonly IMessageBus _messageBus;
        private List<LogEntry> _logs = new();
        private bool _isVisible;

        public string Name => "debug";
        public string Description => "显示插件调试信息和系统状态";

        public PluginDebugTool(
            IPluginHost host,
            PluginLifecycleManager lifecycleManager,
            IMessageBus messageBus)
        {
            _host = host;
            _lifecycleManager = lifecycleManager;
            _messageBus = messageBus;

            // 订阅系统事件
            _messageBus.Subscribe<string>("system.log", msg => AddLog("INFO", msg));
            _messageBus.Subscribe<Exception>("system.error", ex => AddLog("ERROR", ex.Message));
        }

        public async Task ExecuteAsync(string[] args)
        {
            _isVisible = true;
            Console.WriteLine("\n========== Plugin Debug Information ==========");
            await DisplaySystemInfoAsync();
            await DisplayPluginInfoAsync();
            await DisplayLogsAsync();
            Console.WriteLine("===============================================\n");
        }

        public void ShowHelp()
        {
            Console.WriteLine("Plugin Debug Tool");
            Console.WriteLine("Usage: debug [options]");
            Console.WriteLine("Options:");
            Console.WriteLine("  --dump <pluginId>  Dump detailed information for specific plugin");
            Console.WriteLine("  --logs             Show only log information");
            Console.WriteLine("  --help             Show this help message");
        }

        private async Task DisplaySystemInfoAsync()
        {
            Console.WriteLine("\n--- System Information ---");
            Console.WriteLine($"  Total Plugins: {_host.LoadedPlugins.Count}");
            Console.WriteLine($"  Process Memory: {Process.GetCurrentProcess().WorkingSet64 / 1024 / 1024} MB");
            Console.WriteLine($"  Thread Count: {Process.GetCurrentProcess().Threads.Count}");
            Console.WriteLine($"  Runtime Version: {Environment.Version}");
            Console.WriteLine($"  OS Version: {Environment.OSVersion}");
        }

        private async Task DisplayPluginInfoAsync()
        {
            Console.WriteLine("\n--- Plugin Status ---");
            foreach (var plugin in _host.LoadedPlugins)
            {
                var state = _lifecycleManager.GetState(plugin.Id);
                var stats = _lifecycleManager.ResourceTracker.GetStatistics(plugin.Id);

                Console.WriteLine($"\n  [{plugin.Id}] {plugin.Name} v{plugin.Version}");
                Console.WriteLine($"    Phase: {state?.Phase}");
                Console.WriteLine($"    Start Count: {state?.StartCount}");
                Console.WriteLine($"    Error Count: {state?.ErrorCount}");
                Console.WriteLine($"    Resources: {stats.ResourceCount}");
                Console.WriteLine($"    Background Tasks: {stats.BackgroundTaskCount}");

                if (state?.LastError != null)
                {
                    Console.WriteLine($"    Last Error: {state.LastError.Message}");
                }
            }
        }

        private async Task DisplayLogsAsync()
        {
            Console.WriteLine("\n--- Recent Logs ---");
            foreach (var log in _logs.TakeLast(10))
            {
                Console.WriteLine($"  [{log.Timestamp:HH:mm:ss}] [{log.Level}] {log.Message}");
            }
        }

        public void AddLog(string level, string message)
        {
            _logs.Add(new LogEntry
            {
                Timestamp = DateTime.Now,
                Level = level,
                Message = message
            });

            // 保持最近 100 条日志
            if (_logs.Count > 100)
            {
                _logs.RemoveAt(0);
            }
        }

        public async Task DumpPluginInfoAsync(string pluginId)
        {
            var plugin = _host.LoadedPlugins.FirstOrDefault(p => p.Id == pluginId);
            if (plugin == null)
            {
                Console.WriteLine($"Plugin not found: {pluginId}");
                return;
            }

            var state = _lifecycleManager.GetState(pluginId);
            var stats = _lifecycleManager.ResourceTracker.GetStatistics(pluginId);

            var sb = new StringBuilder();
            sb.AppendLine($"\n========== Plugin Dump: {pluginId} ==========");
            sb.AppendLine($"Name: {plugin.Name}");
            sb.AppendLine($"Version: {plugin.Version}");
            sb.AppendLine($"Phase: {state?.Phase}");
            sb.AppendLine($"Last Transition: {state?.LastTransition}");
            sb.AppendLine($"Start Count: {state?.StartCount}");
            sb.AppendLine($"Stop Count: {state?.StopCount}");
            sb.AppendLine($"Error Count: {state?.ErrorCount}");
            sb.AppendLine($"Resources: {stats.ResourceCount}");
            sb.AppendLine($"Cancellation Tokens: {stats.CancellationTokenCount}");
            sb.AppendLine($"Background Tasks: {stats.BackgroundTaskCount}");
            sb.AppendLine("================================================");

            Console.WriteLine(sb.ToString());
        }

        private class LogEntry
        {
            public DateTime Timestamp { get; set; }
            public string Level { get; set; }
            public string Message { get; set; }
        }
    }
}
