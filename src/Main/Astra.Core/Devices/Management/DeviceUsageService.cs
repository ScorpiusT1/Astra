using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Astra.Core.Devices.Management
{
    /// <summary>
    /// 设备使用跟踪服务默认实现
    /// </summary>
    public sealed class DeviceUsageService : IDeviceUsageService
    {
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, DeviceUsageInfo>> _usages =
            new(StringComparer.OrdinalIgnoreCase);

        public static IDeviceUsageService Null { get; } = new NullDeviceUsageService();

        public void RegisterUsage(string deviceId, string consumerId, string consumerName = null, string scope = null)
        {
            if (string.IsNullOrWhiteSpace(deviceId) || string.IsNullOrWhiteSpace(consumerId))
                return;

            var consumers = _usages.GetOrAdd(deviceId, _ => new ConcurrentDictionary<string, DeviceUsageInfo>(StringComparer.OrdinalIgnoreCase));
            consumers[consumerId] = new DeviceUsageInfo(
                deviceId,
                consumerId,
                consumerName ?? consumerId,
                scope ?? string.Empty,
                DateTime.UtcNow);
        }

        public void UnregisterUsage(string deviceId, string consumerId)
        {
            if (string.IsNullOrWhiteSpace(deviceId) || string.IsNullOrWhiteSpace(consumerId))
                return;

            if (_usages.TryGetValue(deviceId, out var consumers))
            {
                consumers.TryRemove(consumerId, out _);

                if (consumers.IsEmpty)
                {
                    _usages.TryRemove(deviceId, out _);
                }
            }
        }

        public void ReplaceUsage(string oldDeviceId, string newDeviceId, string consumerId, string consumerName = null, string scope = null)
        {
            if (!string.IsNullOrWhiteSpace(oldDeviceId))
            {
                UnregisterUsage(oldDeviceId, consumerId);
            }

            if (!string.IsNullOrWhiteSpace(newDeviceId))
            {
                RegisterUsage(newDeviceId, consumerId, consumerName, scope);
            }
        }

        public void RemoveConsumer(string consumerId)
        {
            if (string.IsNullOrWhiteSpace(consumerId))
                return;

            foreach (var kvp in _usages.ToArray())
            {
                if (kvp.Value.TryRemove(consumerId, out _))
                {
                    if (kvp.Value.IsEmpty)
                    {
                        _usages.TryRemove(kvp.Key, out _);
                    }
                }
            }
        }

        public void ClearDevice(string deviceId)
        {
            if (string.IsNullOrWhiteSpace(deviceId))
                return;

            _usages.TryRemove(deviceId, out _);
        }

        public IReadOnlyCollection<DeviceUsageInfo> GetConsumers(string deviceId)
        {
            if (string.IsNullOrWhiteSpace(deviceId))
                return Array.Empty<DeviceUsageInfo>();

            if (_usages.TryGetValue(deviceId, out var consumers))
            {
                return consumers.Values.ToList();
            }

            return Array.Empty<DeviceUsageInfo>();
        }

        public bool HasConsumers(string deviceId, out IReadOnlyCollection<DeviceUsageInfo> consumers)
        {
            consumers = GetConsumers(deviceId);
            return consumers.Count > 0;
        }

        private sealed class NullDeviceUsageService : IDeviceUsageService
        {
            public void RegisterUsage(string deviceId, string consumerId, string consumerName = null, string scope = null)
            {
            }

            public void UnregisterUsage(string deviceId, string consumerId)
            {
            }

            public void ReplaceUsage(string oldDeviceId, string newDeviceId, string consumerId, string consumerName = null, string scope = null)
            {
            }

            public void RemoveConsumer(string consumerId)
            {
            }

            public void ClearDevice(string deviceId)
            {
            }

            public IReadOnlyCollection<DeviceUsageInfo> GetConsumers(string deviceId)
            {
                return Array.Empty<DeviceUsageInfo>();
            }

            public bool HasConsumers(string deviceId, out IReadOnlyCollection<DeviceUsageInfo> consumers)
            {
                consumers = Array.Empty<DeviceUsageInfo>();
                return false;
            }
        }
    }
}


