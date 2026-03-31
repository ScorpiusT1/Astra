using Astra.Plugins.PLC.Configs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Astra.Plugins.PLC.Providers
{
    public static class PlcIoProvider
    {
        private static IReadOnlyList<IOConfig> GetCachedIoConfigs()
        {
            var plugin = PlcPlugin.Current;
            if (plugin == null)
            {
                return Array.Empty<IOConfig>();
            }

            return plugin.GetAllIoConfigs();
        }

        private static async Task<IReadOnlyList<IOConfig>> GetLatestIoConfigsAsync()
        {
            try
            {
                var mgr = PlcPlugin.GetConfigurationManager();
                if (mgr != null)
                {
                    var result = await mgr.GetAllAsync<IOConfig>().ConfigureAwait(false);
                    if (result != null && result.Success && result.Data != null)
                    {
                        return result.Data.ToList();
                    }
                }
            }
            catch
            {
                // 读取配置管理器失败时回退到插件缓存
            }

            return GetCachedIoConfigs();
        }

        public static List<string> GetIoNames()
        {
            try
            {
                return GetCachedIoConfigs()
                    .Where(c => c != null && c.IOs != null && c.IOs.Count > 0)
                    .SelectMany(c => c.IOs)
                    .Where(i => i != null && i.IsEnabled && !string.IsNullOrWhiteSpace(i.Name))
                    .Select(i => i.Name!.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(k => k)
                    .ToList();
            }
            catch
            {
                return new List<string>();
            }
        }

        /// <summary>
        /// 列出 IO 名称；若指定了 PLC 设备名称，则仅返回绑定到该 PLC 或未指定绑定 PLC 的 IO。
        /// </summary>
        public static List<string> GetIoNamesForPlcDevice(string? plcDeviceName)
        {
            try
            {
                var filter = plcDeviceName?.Trim() ?? string.Empty;
                return GetCachedIoConfigs()
                    .Where(c => c != null && c.IOs != null && c.IOs.Count > 0)
                    .SelectMany(c => c.IOs!)
                    .Where(i => i != null && i.IsEnabled && !string.IsNullOrWhiteSpace(i.Name))
                    .Where(i =>
                        string.IsNullOrWhiteSpace(filter) ||
                        string.IsNullOrWhiteSpace(i!.PlcDeviceName) ||
                        string.Equals(i.PlcDeviceName.Trim(), filter, StringComparison.OrdinalIgnoreCase))
                    .Select(i => i!.Name!.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(k => k)
                    .ToList();
            }
            catch
            {
                return new List<string>();
            }
        }

        public static async Task<List<string>> GetIoNamesAsync()
        {
            try
            {
                var configs = await GetLatestIoConfigsAsync().ConfigureAwait(false);
                return configs
                    .Where(c => c != null && c.IOs != null && c.IOs.Count > 0)
                    .SelectMany(c => c.IOs)
                    .Where(i => i != null && i.IsEnabled && !string.IsNullOrWhiteSpace(i.Name))
                    .Select(i => i.Name!.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(k => k)
                    .ToList();
            }
            catch
            {
                return new List<string>();
            }
        }

        public static async Task<List<string>> GetIoNamesForPlcDeviceAsync(string? plcDeviceName)
        {
            try
            {
                var filter = plcDeviceName?.Trim() ?? string.Empty;
                var configs = await GetLatestIoConfigsAsync().ConfigureAwait(false);
                return configs
                    .Where(c => c != null && c.IOs != null && c.IOs.Count > 0)
                    .SelectMany(c => c.IOs!)
                    .Where(i => i != null && i.IsEnabled && !string.IsNullOrWhiteSpace(i.Name))
                    .Where(i =>
                        string.IsNullOrWhiteSpace(filter) ||
                        string.IsNullOrWhiteSpace(i!.PlcDeviceName) ||
                        string.Equals(i.PlcDeviceName.Trim(), filter, StringComparison.OrdinalIgnoreCase))
                    .Select(i => i!.Name!.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(k => k)
                    .ToList();
            }
            catch
            {
                return new List<string>();
            }
        }

        public static IoPointModel? FindByName(string name)
        {
            var plugin = PlcPlugin.Current;
            if (plugin == null)
            {
                return null;
            }

            return plugin.FindIoByName(name);
        }
    }
}

