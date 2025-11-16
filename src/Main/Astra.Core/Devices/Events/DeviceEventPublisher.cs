using Astra.Core.Devices;
using Astra.Core.Devices.Interfaces;
using Astra.Core.Devices.Management;
using Astra.Core.Plugins.Messaging;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Astra.Core.Devices.Events
{
    /// <summary>
    /// 基于 IMessageBus 的设备事件发布器实现
    /// </summary>
    public sealed class DeviceEventPublisher : IDeviceEventPublisher
    {
        private readonly IMessageBus _messageBus;

        public DeviceEventPublisher(IMessageBus messageBus)
        {
            _messageBus = messageBus;
        }

        public Task PublishDeviceAddedAsync(IDevice device)
        {
            if (_messageBus == null || device == null)
                return Task.CompletedTask;

            return _messageBus.PublishAsync("Devices/Added", new DeviceLifecycleEvent
            {
                DeviceId = device.DeviceId,
                DeviceType = device.Type,
                EventType = DeviceLifecycleEventType.Added
            });
        }

        public Task PublishDeviceRemovedAsync(IDevice device)
        {
            if (_messageBus == null || device == null)
                return Task.CompletedTask;

            return _messageBus.PublishAsync("Devices/Removed", new DeviceLifecycleEvent
            {
                DeviceId = device.DeviceId,
                DeviceType = device.Type,
                EventType = DeviceLifecycleEventType.Removed
            });
        }

        public Task PublishDeviceRemovalBlockedAsync(IDevice device, IReadOnlyCollection<DeviceUsageInfo> consumers, string reason)
        {
            if (_messageBus == null || device == null || consumers == null)
                return Task.CompletedTask;

            return _messageBus.PublishAsync("Devices/RemovalBlocked", new DeviceRemovalBlockedEvent
            {
                DeviceId = device.DeviceId,
                DeviceType = device.Type,
                Reason = reason,
                Consumers = consumers
            });
        }
    }

    public enum DeviceLifecycleEventType
    {
        Added,
        Removed
    }

    public sealed class DeviceLifecycleEvent
    {
        public string DeviceId { get; set; }
        public DeviceType DeviceType { get; set; }
        public DeviceLifecycleEventType EventType { get; set; }
    }

    public sealed class DeviceRemovalBlockedEvent
    {
        public string DeviceId { get; set; }
        public DeviceType DeviceType { get; set; }
        public string Reason { get; set; }
        public IReadOnlyCollection<DeviceUsageInfo> Consumers { get; set; }
    }
}


