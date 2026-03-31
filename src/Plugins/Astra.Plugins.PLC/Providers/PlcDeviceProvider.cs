using Astra.Core.Devices.Interfaces;
using Astra.Plugins.PLC.Configs;
using Astra.Plugins.PLC.Devices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Astra.Plugins.PLC.Providers
{
    public static class PlcDeviceProvider
    {
        /// <summary>
        /// 从插件内存缓存（<c>_devices</c>）同步读取设备名列表。
        /// 仅在明确不需要最新持久化数据的场合使用（如节点属性同步读）。
        /// </summary>
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

        /// <summary>
        /// 从配置管理器异步读取最新设备名列表（保存后立刻可见）。
        /// 下拉框刷新场景应使用此方法。
        /// 读取失败时自动回退到内存缓存。
        /// </summary>
        public static async Task<List<string>> GetPlcDeviceNamesAsync()
        {
            try
            {
                var plugin = PlcPlugin.Current;
                if (plugin != null)
                {
                    var configs = await plugin.GetAllPlcDeviceConfigsFromStorageAsync().ConfigureAwait(false);
                    return configs
                        .Where(c => c != null && !string.IsNullOrWhiteSpace(c.DeviceName))
                        .Select(c => c.DeviceName)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(n => n)
                        .ToList();
                }
            }
            catch
            {
                // 读取失败时回退到插件内存缓存
            }

            return GetPlcDeviceNames();
        }
    }
}
