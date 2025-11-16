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
        #region 设备注册

        /// <summary>
        /// 注册设备
        /// </summary>
        OperationResult RegisterDevice(IDevice device);

        /// <summary>
        /// 注销设备
        /// </summary>
        OperationResult UnregisterDevice(string deviceId);

        /// <summary>
        /// 批量注册设备
        /// </summary>
        OperationResult<int> RegisterDevices(IEnumerable<IDevice> devices);

        /// <summary>
        /// 注销所有设备
        /// </summary>
        OperationResult UnregisterAllDevices();

        #endregion

        #region 设备查询

        /// <summary>
        /// 获取设备
        /// </summary>
        OperationResult<IDevice> GetDevice(string deviceId);

        /// <summary>
        /// 获取所有设备
        /// </summary>
        OperationResult<List<IDevice>> GetAllDevices();

        /// <summary>
        /// 按类型获取设备
        /// </summary>
        OperationResult<List<IDevice>> GetDevicesByType(DeviceType type);

        /// <summary>
        /// 按状态获取设备
        /// </summary>
        OperationResult<List<IDevice>> GetDevicesByStatus(DeviceStatus status);

        /// <summary>
        /// 检查设备是否存在
        /// </summary>
        bool DeviceExists(string deviceId);

        /// <summary>
        /// 获取设备数量
        /// </summary>
        int GetDeviceCount();

        /// <summary>
        /// 获取设备数量（按类型）
        /// </summary>
        int GetDeviceCountByType(DeviceType type);

        /// <summary>
        /// 获取在线设备数量
        /// </summary>
        int GetOnlineDeviceCount();

        #endregion

        #region 批量操作

        /// <summary>
        /// 连接所有设备
        /// </summary>
        OperationResult<Dictionary<string, OperationResult>> ConnectAll();

        /// <summary>
        /// 断开所有设备
        /// </summary>
        OperationResult<Dictionary<string, OperationResult>> DisconnectAll();

        /// <summary>
        /// 连接指定类型的设备
        /// </summary>
        OperationResult<Dictionary<string, OperationResult>> ConnectByType(DeviceType type);

        /// <summary>
        /// 断开指定类型的设备
        /// </summary>
        OperationResult<Dictionary<string, OperationResult>> DisconnectByType(DeviceType type);

        /// <summary>
        /// 批量发送消息
        /// </summary>
        OperationResult<Dictionary<string, OperationResult>> BroadcastMessage(DeviceMessage message);

        /// <summary>
        /// 按类型广播消息
        /// </summary>
        OperationResult<Dictionary<string, OperationResult>> BroadcastMessageByType(DeviceType type, DeviceMessage message);

        #endregion

        #region 设备监控

        /// <summary>
        /// 获取所有设备状态
        /// </summary>
        OperationResult<Dictionary<string, DeviceStatus>> GetAllDeviceStatus();

        /// <summary>
        /// 获取设备统计信息
        /// </summary>
        OperationResult<DeviceStatistics> GetStatistics();

        /// <summary>
        /// 开始监控所有设备
        /// </summary>
        OperationResult StartMonitoring();

        /// <summary>
        /// 停止监控所有设备
        /// </summary>
        OperationResult StopMonitoring();

        #endregion

        #region 事件

        /// <summary>
        /// 设备注册事件
        /// </summary>
        event EventHandler<DeviceRegisteredEventArgs> DeviceRegistered;

        /// <summary>
        /// 设备注销事件
        /// </summary>
        event EventHandler<DeviceUnregisteredEventArgs> DeviceUnregistered;

        /// <summary>
        /// 设备状态变更事件
        /// </summary>
        event EventHandler<DeviceStatusChangedEventArgs> DeviceStatusChanged;

        /// <summary>
        /// 设备错误事件
        /// </summary>
        event EventHandler<DeviceErrorEventArgs> DeviceError;

        #endregion
    }

    #region 事件参数

    /// <summary>
    /// 设备注册事件参数
    /// </summary>
    public class DeviceRegisteredEventArgs : EventArgs
    {
        public string DeviceId { get; set; }
        public DeviceType DeviceType { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// 设备注销事件参数
    /// </summary>
    public class DeviceUnregisteredEventArgs : EventArgs
    {
        public string DeviceId { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// 设备统计信息
    /// </summary>
    public class DeviceStatistics
    {
        public int TotalDevices { get; set; }
        public int OnlineDevices { get; set; }
        public int OfflineDevices { get; set; }
        public int ErrorDevices { get; set; }
        public Dictionary<DeviceType, int> DevicesByType { get; set; }
        public Dictionary<DeviceStatus, int> DevicesByStatus { get; set; }
        public DateTime LastUpdateTime { get; set; }

        public DeviceStatistics()
        {
            DevicesByType = new Dictionary<DeviceType, int>();
            DevicesByStatus = new Dictionary<DeviceStatus, int>();
            LastUpdateTime = DateTime.Now;
        }
    }

    #endregion
}