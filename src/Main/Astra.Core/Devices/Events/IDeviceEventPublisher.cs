using Astra.Core.Devices.Interfaces;
using Astra.Core.Devices.Management;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Astra.Core.Devices.Events
{
    /// <summary>
    /// 设备事件发布器接口
    /// </summary>
    public interface IDeviceEventPublisher
    {
        Task PublishDeviceAddedAsync(IDevice device);
        Task PublishDeviceRemovedAsync(IDevice device);
        Task PublishDeviceRemovalBlockedAsync(IDevice device, IReadOnlyCollection<DeviceUsageInfo> consumers, string reason);
    }

    /// <summary>
    /// 空实现，避免判空
    /// </summary>
    public sealed class NullDeviceEventPublisher : IDeviceEventPublisher
    {
        public static IDeviceEventPublisher Instance { get; } = new NullDeviceEventPublisher();

        private NullDeviceEventPublisher()
        {
        }

        public Task PublishDeviceAddedAsync(IDevice device) => Task.CompletedTask;

        public Task PublishDeviceRemovedAsync(IDevice device) => Task.CompletedTask;

        public Task PublishDeviceRemovalBlockedAsync(IDevice device, IReadOnlyCollection<DeviceUsageInfo> consumers, string reason) => Task.CompletedTask;
    }
}


