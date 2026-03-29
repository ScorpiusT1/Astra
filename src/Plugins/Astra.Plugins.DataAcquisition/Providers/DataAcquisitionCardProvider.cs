using Astra.Core.Devices.Interfaces;
using Astra.Plugins.DataAcquisition.Devices;

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
        /// </summary>
        public static bool TryGetDeviceIdByDisplayName(string? displayName, out string deviceId)
        {
            deviceId = string.Empty;
            if (string.IsNullOrWhiteSpace(displayName))
            {
                return false;
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
                    if (dev is IDevice d && string.Equals(d.DeviceName?.Trim(), displayName.Trim(), StringComparison.OrdinalIgnoreCase))
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
        /// 返回当前插件中所有采集卡的设备名称列表（用于属性编辑器多选，绑定到字符串集合）。
        /// </summary>
        public static List<string> GetDataAcquisitionNames()
        {
            try
            {
                var plugin = DataAcquisitionPlugin.Current;
                if (plugin == null)
                {
                    return new List<string>();
                }

                var devices = plugin.GetAllDataAcquisitions();

                return devices?
                           .Select(d => d as IDevice)
                           .Where(d => d != null && !string.IsNullOrWhiteSpace(d.DeviceName))
                           .Select(d => d.DeviceName)
                           .Distinct()
                           .ToList()
                       ?? new List<string>();
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
        /// </summary>
        public static List<string> GetConfiguredChannelNamesForDeviceDisplayName(string? deviceDisplayName)
        {
            var result = new List<string> { string.Empty };
            if (string.IsNullOrWhiteSpace(deviceDisplayName))
            {
                return result;
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
                        !string.Equals(idev.DeviceName?.Trim(), deviceDisplayName.Trim(), StringComparison.OrdinalIgnoreCase))
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
    }
}
