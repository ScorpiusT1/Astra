using Astra.Plugins.PLC.Configs;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Astra.Plugins.PLC.Providers
{
    public static class PlcIoProvider
    {
        public static List<string> GetIoNames()
        {
            try
            {
                var plugin = PlcPlugin.Current;
                if (plugin == null)
                {
                    return new List<string>();
                }

                return plugin.GetAllIoConfigs()
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
                var plugin = PlcPlugin.Current;
                if (plugin == null)
                {
                    return new List<string>();
                }

                var filter = plcDeviceName?.Trim() ?? string.Empty;
                return plugin.GetAllIoConfigs()
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

