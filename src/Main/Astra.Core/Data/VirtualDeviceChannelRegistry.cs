using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Astra.Core.Data
{
    /// <summary>
    /// 虚拟设备（如文件导入）的通道名注册表。
    /// 文件导入节点在加载文件后将可用通道注册到此处，
    /// 下游属性编辑器的通道下拉列表据此动态刷新。
    /// </summary>
    public static class VirtualDeviceChannelRegistry
    {
        private static readonly ConcurrentDictionary<string, List<string>> _channels = new(StringComparer.OrdinalIgnoreCase);
        private static readonly ConcurrentDictionary<string, string> _aliasToDeviceId = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>注册虚拟设备的可用通道名列表（覆盖已有记录）。</summary>
        public static void Register(string deviceDisplayName, IEnumerable<string> channelNames)
        {
            if (string.IsNullOrWhiteSpace(deviceDisplayName))
                return;
            _channels[deviceDisplayName.Trim()] = channelNames?.ToList() ?? new List<string>();
        }

        /// <summary>获取虚拟设备的可用通道名（首项为空字符串表示默认首通道）。</summary>
        public static List<string> GetChannels(string deviceDisplayName)
        {
            if (string.IsNullOrWhiteSpace(deviceDisplayName))
                return new List<string> { string.Empty };

            if (_channels.TryGetValue(deviceDisplayName.Trim(), out var list) && list.Count > 0)
            {
                var result = new List<string> { string.Empty };
                result.AddRange(list.Where(n => !string.IsNullOrEmpty(n)));
                return result;
            }

            return new List<string> { string.Empty };
        }

        /// <summary>清除指定虚拟设备的通道注册。</summary>
        public static void Clear(string deviceDisplayName)
        {
            if (!string.IsNullOrWhiteSpace(deviceDisplayName))
                _channels.TryRemove(deviceDisplayName.Trim(), out _);
        }

        /// <summary>注册虚拟设备别名到 DeviceId 的映射。</summary>
        public static void RegisterAlias(string alias, string deviceId)
        {
            if (string.IsNullOrWhiteSpace(alias) || string.IsNullOrWhiteSpace(deviceId))
                return;
            _aliasToDeviceId[alias.Trim()] = deviceId.Trim();
        }

        /// <summary>尝试按别名解析 DeviceId。</summary>
        public static bool TryResolveDeviceId(string alias, out string deviceId)
        {
            deviceId = string.Empty;
            if (string.IsNullOrWhiteSpace(alias))
                return false;
            return _aliasToDeviceId.TryGetValue(alias.Trim(), out deviceId);
        }

        /// <summary>移除虚拟设备别名。</summary>
        public static void RemoveAlias(string alias)
        {
            if (!string.IsNullOrWhiteSpace(alias))
                _aliasToDeviceId.TryRemove(alias.Trim(), out _);
        }

        /// <summary>获取所有已注册的虚拟设备别名。</summary>
        public static IReadOnlyList<string> GetAllAliases()
        {
            return _aliasToDeviceId.Keys.ToList();
        }

        /// <summary>检查指定名称是否为已注册的虚拟设备。</summary>
        public static bool IsVirtualDevice(string deviceDisplayName)
        {
            if (string.IsNullOrWhiteSpace(deviceDisplayName))
                return false;
            return _aliasToDeviceId.ContainsKey(deviceDisplayName.Trim()) ||
                   _channels.ContainsKey(deviceDisplayName.Trim());
        }
    }
}
