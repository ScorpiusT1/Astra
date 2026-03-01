using System;
using System.Collections.Generic;
using Astra.Core.Devices.Interfaces;
using Astra.Core.Foundation.Common;

namespace Astra.Core.Devices.Management
{
    /// <summary>
    /// 设备管理器接口
    /// </summary>
    public interface IDeviceManager : IDisposable
    {
        OperationResult RegisterDevice(IDevice device);
        OperationResult UnregisterDevice(string deviceId);
        OperationResult<int> RegisterDevices(IEnumerable<IDevice> devices);
        OperationResult UnregisterAllDevices();

        OperationResult<IDevice> GetDevice(string deviceId);
        OperationResult<List<IDevice>> GetAllDevices();
        OperationResult<List<IDevice>> GetDevicesByType(DeviceType type);
        OperationResult<List<IDevice>> GetDevicesByStatus(DeviceStatus status);
        bool DeviceExists(string deviceId);
        int GetDeviceCount();
        int GetDeviceCountByType(DeviceType type);
        int GetOnlineDeviceCount();

        OperationResult<Dictionary<string, OperationResult>> ConnectAll();
        OperationResult<Dictionary<string, OperationResult>> DisconnectAll();
        OperationResult<Dictionary<string, OperationResult>> ConnectByType(DeviceType type);
        OperationResult<Dictionary<string, OperationResult>> DisconnectByType(DeviceType type);
        OperationResult<Dictionary<string, OperationResult>> BroadcastMessage(DeviceMessage message);
        OperationResult<Dictionary<string, OperationResult>> BroadcastMessageByType(DeviceType type, DeviceMessage message);

        OperationResult<Dictionary<string, DeviceStatus>> GetAllDeviceStatus();
        OperationResult<DeviceStatus> GetDeviceStatus(string deviceId);
    }
}

