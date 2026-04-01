using Astra.Core.Constants;
using Astra.Core.Data;
using Astra.Core.Devices.Interfaces;
using Astra.Plugins.DataAcquisition.Devices;
using System.Linq;

namespace Astra.Plugins.DataAcquisition.Providers
{
    /// <summary>
    /// 采集卡提供者：为属性编辑器提供可选的 IDataAcquisition 设备列表。
    /// 
    /// 通过 DataAcquisitionPlugin 内部维护的 _devices 列表获取当前所有已注册的采集卡实例，
    /// 确保属性编辑器中看到的是实时的设备列表，而不是重复创建新实例。
    /// </summary>
    public static class DataAcquisitionCardProvider
    {       
        /// <summary>
        /// 按设备显示名解析 DeviceId（供其它插件解析 Raw 键等）。
        /// 优先检查内置虚拟设备 → 再查 VirtualDeviceChannelRegistry 别名 → 最后查真实采集卡。
        /// </summary>
        public static bool TryGetDeviceIdByDisplayName(string? displayName, out string deviceId)
        {
            deviceId = string.Empty;
            if (string.IsNullOrWhiteSpace(displayName))
            {
                return false;
            }

            var trimmed = displayName.Trim();

            if (string.Equals(trimmed, AstraSharedConstants.VirtualImportDevices.DisplayName, StringComparison.OrdinalIgnoreCase))
            {
                deviceId = AstraSharedConstants.VirtualImportDevices.DeviceId;
                return true;
            }

            if (VirtualDeviceChannelRegistry.TryResolveDeviceId(trimmed, out var aliasDeviceId))
            {
                deviceId = aliasDeviceId;
                return true;
            }

            try
            {
                var plugin = DataAcquisitionPlugin.Current;
                if (plugin == null)
                {
                    return false;
                }

                foreach (var dev in plugin.GetAllDataAcquisitions())
                {
                    if (dev is IDevice d && string.Equals(d.DeviceName?.Trim(), trimmed, StringComparison.OrdinalIgnoreCase))
                    {
                        deviceId = d.DeviceId;
                        return true;
                    }
                }
            }
            catch
            {
            }

            return false;
        }

        /// <summary>
        /// 返回当前插件中所有采集卡的设备名称列表（含虚拟设备与别名），用于属性编辑器多选。
        /// </summary>
        public static List<string> GetDataAcquisitionNames()
        {
            try
            {
                var plugin = DataAcquisitionPlugin.Current;
                if (plugin == null)
                {
                    var fallback = new List<string>();
                    AppendVirtualAliasesOrGeneric(fallback);
                    return fallback;
                }

                var devices = plugin.GetAllDataAcquisitions();

                var list = devices?
                               .Select(d => d as IDevice)
                               .Where(d => d != null && !string.IsNullOrWhiteSpace(d.DeviceName))
                               .Select(d => d!.DeviceName!)
                               .Distinct()
                               .ToList()
                           ?? new List<string>();

                AppendVirtualAliasesOrGeneric(list);
                return list;
            }
            catch
            {
                return new List<string>();
            }
        }

        /// <summary>
        /// 汇总所有已注册采集卡配置中「已启用」通道的名称（与 NVH 中通道键一致）。
        /// 首项为空字符串，表示使用组内第一个通道（与卡控节点逻辑一致）。
        /// </summary>
        public static List<string> GetConfiguredChannelNames()
        {
            var result = new List<string> { string.Empty };
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { string.Empty };
            try
            {
                var plugin = DataAcquisitionPlugin.Current;
                if (plugin == null)
                {
                    return result;
                }

                foreach (var dev in plugin.GetAllDataAcquisitions())
                {
                    if (dev is not DataAcquisitionDeviceBase daq)
                    {
                        continue;
                    }

                    foreach (var name in daq.GetConfiguredEnabledChannelNames())
                    {
                        if (seen.Add(name))
                        {
                            result.Add(name);
                        }
                    }
                }
            }
            catch
            {
            }

            return result;
        }

        /// <summary>
        /// 指定采集卡（设备显示名）下已启用通道名；首项为空表示组内首通道。
        /// 虚拟设备（文件导入及其别名）从 <see cref="VirtualDeviceChannelRegistry"/> 动态获取。
        /// </summary>
        public static List<string> GetConfiguredChannelNamesForDeviceDisplayName(string? deviceDisplayName)
        {
            var result = new List<string> { string.Empty };
            if (string.IsNullOrWhiteSpace(deviceDisplayName))
            {
                return result;
            }

            var trimmed = deviceDisplayName.Trim();

            if (string.Equals(trimmed, AstraSharedConstants.VirtualImportDevices.DisplayName, StringComparison.OrdinalIgnoreCase) ||
                VirtualDeviceChannelRegistry.IsVirtualDevice(trimmed))
            {
                return VirtualDeviceChannelRegistry.GetChannels(trimmed);
            }

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { string.Empty };
            try
            {
                var plugin = DataAcquisitionPlugin.Current;
                if (plugin == null)
                {
                    return result;
                }

                foreach (var dev in plugin.GetAllDataAcquisitions())
                {
                    if (dev is not DataAcquisitionDeviceBase daq)
                    {
                        continue;
                    }

                    if (dev is not IDevice idev ||
                        !string.Equals(idev.DeviceName?.Trim(), trimmed, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    foreach (var name in daq.GetConfiguredEnabledChannelNames())
                    {
                        if (seen.Add(name))
                        {
                            result.Add(name);
                        }
                    }

                    break;
                }
            }
            catch
            {
            }

            return result;
        }

        /// <summary>
        /// 将虚拟别名追加到列表。若已有任何以 "文件导入" 为前缀的别名，则不再添加通用名 "文件导入"，
        /// 避免下拉中出现重复条目导致 BFS 解析失败。
        /// </summary>
        private static void AppendVirtualAliasesOrGeneric(List<string> list)
        {
            var aliases = VirtualDeviceChannelRegistry.GetAllAliases().ToList();
            var genericName = AstraSharedConstants.VirtualImportDevices.DisplayName;
            var hasSpecificAlias = aliases.Any(a =>
                a.StartsWith(genericName, StringComparison.OrdinalIgnoreCase) && a.Length > genericName.Length);

            if (!hasSpecificAlias && !list.Contains(genericName, StringComparer.OrdinalIgnoreCase))
                list.Add(genericName);

            foreach (var alias in aliases)
            {
                if (!list.Contains(alias, StringComparer.OrdinalIgnoreCase))
                    list.Add(alias);
            }
        }
    }
}
