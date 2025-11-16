using System;
using System.Collections.Generic;

namespace Astra.Core.Devices.Management
{
    /// <summary>
    /// 设备使用跟踪服务
    /// 负责记录工作流节点、后台任务等对设备的引用关系，用于设备删除校验和告警。
    /// </summary>
    public interface IDeviceUsageService
    {
        void RegisterUsage(string deviceId, string consumerId, string consumerName = null, string scope = null);
        void UnregisterUsage(string deviceId, string consumerId);
        void ReplaceUsage(string oldDeviceId, string newDeviceId, string consumerId, string consumerName = null, string scope = null);
        void RemoveConsumer(string consumerId);
        void ClearDevice(string deviceId);
        IReadOnlyCollection<DeviceUsageInfo> GetConsumers(string deviceId);
        bool HasConsumers(string deviceId, out IReadOnlyCollection<DeviceUsageInfo> consumers);
    }

    /// <summary>
    /// 设备使用信息
    /// </summary>
    public sealed record DeviceUsageInfo(
        string DeviceId,
        string ConsumerId,
        string ConsumerName,
        string Scope,
        DateTime RegisteredAt);
}


