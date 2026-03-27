using Astra.Core.Devices.Interfaces;
using Astra.Plugins.PLC.Devices;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Astra.Plugins.PLC.Providers
{
    internal static class PlcDeviceProvider
    {
        public static List<string> GetPlcDeviceNames()
        {
            try
            {
                var plugin = PlcPlugin.Current;
                if (plugin == null)
                {
                    return new List<string>();
                }

                return plugin.GetAllPlcs()
                    .OfType<PlcDeviceBase>()
                    .Where(d => !string.IsNullOrWhiteSpace(d.DeviceName))
                    .Select(d => d.DeviceName)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(n => n)
                    .ToList();
            }
            catch
            {
                return new List<string>();
            }
        }
    }
}
