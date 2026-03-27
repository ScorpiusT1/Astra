using Astra.Plugins.PLC.Configs;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Astra.Plugins.PLC.Providers
{
    internal static class PlcIoProvider
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

