using Astra.Core.Devices.Interfaces;
using Astra.Core.Logs;
using Astra.Core.Logs.Extensions;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using MSILogger = Microsoft.Extensions.Logging.ILogger;

namespace Astra.Core.Devices.Extensions
{
    /// <summary>
    /// 设备日志扩展方法
    /// 提供设备相关的日志记录功能
    /// 支持 Microsoft.Extensions.Logging.ILogger
    /// </summary>
    public static class DeviceLoggerExtensions
    {
        #region 设备连接日志

        /// <summary>
        /// 记录设备连接开始
        /// </summary>
        /// <param name="logger">日志器</param>
        /// <param name="device">设备对象</param>
        public static void LogDeviceConnecting(this MSILogger logger, IDevice device)
        {
            if (logger == null || device == null)
                return;

            var data = new Dictionary<string, object>
            {
                { "device_id", device.DeviceId },
                { "device_name", device.DeviceName },
                { "device_type", device.Type.ToString() },
                { "action", "Connecting" }
            };

            logger.LogInfo($"设备连接中: {device.DeviceName} ({device.DeviceId})", LogCategory.System, data);
        }

        /// <summary>
        /// 记录设备连接成功
        /// </summary>
        /// <param name="logger">日志器</param>
        /// <param name="device">设备对象</param>
        /// <param name="duration">连接耗时</param>
        public static void LogDeviceConnected(this MSILogger logger, IDevice device, TimeSpan? duration = null)
        {
            if (logger == null || device == null)
                return;

            var data = new Dictionary<string, object>
            {
                { "device_id", device.DeviceId },
                { "device_name", device.DeviceName },
                { "device_type", device.Type.ToString() },
                { "action", "Connected" }
            };

            if (duration.HasValue)
            {
                data["duration_ms"] = duration.Value.TotalMilliseconds;
            }

            logger.LogInfo($"设备连接成功: {device.DeviceName} ({device.DeviceId})" + 
                       (duration.HasValue ? $" (耗时: {duration.Value.TotalMilliseconds:F2}ms)" : ""), 
                       LogCategory.System, data);
        }

        /// <summary>
        /// 记录设备连接失败
        /// </summary>
        /// <param name="logger">日志器</param>
        /// <param name="device">设备对象</param>
        /// <param name="ex">异常对象</param>
        /// <param name="duration">连接耗时</param>
        public static void LogDeviceConnectFailed(this MSILogger logger, IDevice device, Exception ex = null, TimeSpan? duration = null)
        {
            if (logger == null || device == null)
                return;

            var data = new Dictionary<string, object>
            {
                { "device_id", device.DeviceId },
                { "device_name", device.DeviceName },
                { "device_type", device.Type.ToString() },
                { "action", "ConnectFailed" },
                { "error_type", ex?.GetType().Name }
            };

            if (duration.HasValue)
            {
                data["duration_ms"] = duration.Value.TotalMilliseconds;
            }

            logger.LogError($"设备连接失败: {device.DeviceName} ({device.DeviceId}) - {ex?.Message}", ex, LogCategory.System, data);
        }

        /// <summary>
        /// 记录设备断开连接
        /// </summary>
        /// <param name="logger">日志器</param>
        /// <param name="device">设备对象</param>
        public static void LogDeviceDisconnected(this MSILogger logger, IDevice device)
        {
            if (logger == null || device == null)
                return;

            var data = new Dictionary<string, object>
            {
                { "device_id", device.DeviceId },
                { "device_name", device.DeviceName },
                { "device_type", device.Type.ToString() },
                { "action", "Disconnected" }
            };

            logger.LogInfo($"设备断开连接: {device.DeviceName} ({device.DeviceId})", LogCategory.System, data);
        }

        #endregion

        #region 设备状态日志

        /// <summary>
        /// 记录设备状态变更
        /// </summary>
        /// <param name="logger">日志器</param>
        /// <param name="device">设备对象</param>
        /// <param name="oldStatus">旧状态</param>
        /// <param name="newStatus">新状态</param>
        public static void LogDeviceStatusChanged(this MSILogger logger, IDevice device, DeviceStatus oldStatus, DeviceStatus newStatus)
        {
            if (logger == null || device == null)
                return;

            var data = new Dictionary<string, object>
            {
                { "device_id", device.DeviceId },
                { "device_name", device.DeviceName },
                { "device_type", device.Type.ToString() },
                { "old_status", oldStatus.ToString() },
                { "new_status", newStatus.ToString() },
                { "action", "StatusChanged" }
            };

            var isWarning = newStatus == DeviceStatus.Error || newStatus == DeviceStatus.Offline;

            if (isWarning)
            {
                logger.LogWarn($"设备状态变更: {device.DeviceName} ({device.DeviceId}) - {oldStatus} -> {newStatus}", 
                             LogCategory.System, data);
            }
            else
            {
                logger.LogInfo($"设备状态变更: {device.DeviceName} ({device.DeviceId}) - {oldStatus} -> {newStatus}", 
                           LogCategory.System, data);
            }
        }

        #endregion

        #region 设备数据传输日志

        /// <summary>
        /// 记录设备发送数据
        /// </summary>
        /// <param name="logger">日志器</param>
        /// <param name="device">设备对象</param>
        /// <param name="message">消息对象</param>
        /// <param name="success">是否成功</param>
        public static void LogDeviceSend(this MSILogger logger, IDevice device, DeviceMessage message, bool success)
        {
            if (logger == null || device == null)
                return;

            var data = new Dictionary<string, object>
            {
                { "device_id", device.DeviceId },
                { "device_name", device.DeviceName },
                { "action", "Send" },
                { "success", success }
            };

            if (message != null)
            {
                data["channel_id"] = message.ChannelId;
                data["data_length"] = message.Data?.Length ?? 0;
            }

            if (success)
            {
                logger.LogDebug($"设备发送数据: {device.DeviceName} ({device.DeviceId})", LogCategory.Network, data);
            }
            else
            {
                logger.LogWarn($"设备发送数据失败: {device.DeviceName} ({device.DeviceId})", LogCategory.Network, data);
            }
        }

        /// <summary>
        /// 记录设备接收数据
        /// </summary>
        /// <param name="logger">日志器</param>
        /// <param name="device">设备对象</param>
        /// <param name="message">消息对象</param>
        public static void LogDeviceReceive(this MSILogger logger, IDevice device, DeviceMessage message)
        {
            if (logger == null || device == null)
                return;

            var data = new Dictionary<string, object>
            {
                { "device_id", device.DeviceId },
                { "device_name", device.DeviceName },
                { "action", "Receive" }
            };

            if (message != null)
            {
                data["channel_id"] = message.ChannelId;
                data["data_length"] = message.Data?.Length ?? 0;
            }

            logger.LogDebug($"设备接收数据: {device.DeviceName} ({device.DeviceId})", LogCategory.Network, data);
        }

        #endregion

        #region 设备错误日志

        /// <summary>
        /// 记录设备错误
        /// </summary>
        /// <param name="logger">日志器</param>
        /// <param name="device">设备对象</param>
        /// <param name="errorMessage">错误消息</param>
        /// <param name="ex">异常对象</param>
        /// <param name="errorCode">错误代码</param>
        public static void LogDeviceError(this MSILogger logger, IDevice device, string errorMessage, Exception ex = null, int errorCode = -1)
        {
            if (logger == null || device == null)
                return;

            var data = new Dictionary<string, object>
            {
                { "device_id", device.DeviceId },
                { "device_name", device.DeviceName },
                { "device_type", device.Type.ToString() },
                { "action", "Error" },
                { "error_code", errorCode },
                { "error_type", ex?.GetType().Name }
            };

            logger.LogError($"设备错误: {device.DeviceName} ({device.DeviceId}) - {errorMessage}", ex, LogCategory.System, data);
        }

        #endregion

        #region 设备配置日志

        /// <summary>
        /// 记录设备配置变更
        /// </summary>
        /// <param name="logger">日志器</param>
        /// <param name="device">设备对象</param>
        /// <param name="changedProperties">变更的属性列表</param>
        public static void LogDeviceConfigChanged(this MSILogger logger, IDevice device, List<string> changedProperties)
        {
            if (logger == null || device == null)
                return;

            var data = new Dictionary<string, object>
            {
                { "device_id", device.DeviceId },
                { "device_name", device.DeviceName },
                { "action", "ConfigChanged" },
                { "changed_properties", changedProperties }
            };

            logger.LogInfo($"设备配置变更: {device.DeviceName} ({device.DeviceId}) - 变更了 {changedProperties?.Count ?? 0} 个属性", 
                       LogCategory.System, data);
        }

        #endregion

        #region 设备注册/注销日志

        /// <summary>
        /// 记录设备注册
        /// </summary>
        /// <param name="logger">日志器</param>
        /// <param name="device">设备对象</param>
        public static void LogDeviceRegistered(this MSILogger logger, IDevice device)
        {
            if (logger == null || device == null)
                return;

            var data = new Dictionary<string, object>
            {
                { "device_id", device.DeviceId },
                { "device_name", device.DeviceName },
                { "device_type", device.Type.ToString() },
                { "action", "Registered" }
            };

            logger.LogInfo($"设备注册: {device.DeviceName} ({device.DeviceId})", LogCategory.System, data);
        }

        /// <summary>
        /// 记录设备注销
        /// </summary>
        /// <param name="logger">日志器</param>
        /// <param name="deviceId">设备ID</param>
        public static void LogDeviceUnregistered(this MSILogger logger, string deviceId)
        {
            if (logger == null || string.IsNullOrEmpty(deviceId))
                return;

            var data = new Dictionary<string, object>
            {
                { "device_id", deviceId },
                { "action", "Unregistered" }
            };

            logger.LogInfo($"设备注销: {deviceId}", LogCategory.System, data);
        }

        #endregion
    }
}

