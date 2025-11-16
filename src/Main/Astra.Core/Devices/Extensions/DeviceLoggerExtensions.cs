using Astra.Core.Devices.Interfaces;
using Astra.Core.Logs;
using System;
using System.Collections.Generic;

namespace Astra.Core.Devices.Extensions
{
    /// <summary>
    /// 设备日志扩展方法
    /// 提供设备相关的日志记录功能
    /// </summary>
    public static class DeviceLoggerExtensions
    {
        #region 设备连接日志

        /// <summary>
        /// 记录设备连接开始
        /// </summary>
        /// <param name="logger">日志器</param>
        /// <param name="device">设备对象</param>
        /// <param name="triggerUIEvent">是否触发UI更新事件</param>
        public static void LogDeviceConnecting(this ILogger logger, IDevice device, bool? triggerUIEvent = null)
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

            logger.Info($"设备连接中: {device.DeviceName} ({device.DeviceId})", LogCategory.System, data, triggerUIEvent);
        }

        /// <summary>
        /// 记录设备连接成功
        /// </summary>
        /// <param name="logger">日志器</param>
        /// <param name="device">设备对象</param>
        /// <param name="duration">连接耗时</param>
        /// <param name="triggerUIEvent">是否触发UI更新事件</param>
        public static void LogDeviceConnected(this ILogger logger, IDevice device, TimeSpan? duration = null, bool? triggerUIEvent = null)
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

            logger.Info($"设备连接成功: {device.DeviceName} ({device.DeviceId})" + 
                       (duration.HasValue ? $" (耗时: {duration.Value.TotalMilliseconds:F2}ms)" : ""), 
                       LogCategory.System, data, triggerUIEvent);
        }

        /// <summary>
        /// 记录设备连接失败
        /// </summary>
        /// <param name="logger">日志器</param>
        /// <param name="device">设备对象</param>
        /// <param name="ex">异常对象</param>
        /// <param name="duration">连接耗时</param>
        /// <param name="triggerUIEvent">是否触发UI更新事件</param>
        public static void LogDeviceConnectFailed(this ILogger logger, IDevice device, Exception ex = null, TimeSpan? duration = null, bool? triggerUIEvent = null)
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

            logger.Error($"设备连接失败: {device.DeviceName} ({device.DeviceId}) - {ex?.Message}", ex, LogCategory.System, data, triggerUIEvent);
        }

        /// <summary>
        /// 记录设备断开连接
        /// </summary>
        /// <param name="logger">日志器</param>
        /// <param name="device">设备对象</param>
        /// <param name="triggerUIEvent">是否触发UI更新事件</param>
        public static void LogDeviceDisconnected(this ILogger logger, IDevice device, bool? triggerUIEvent = null)
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

            logger.Info($"设备断开连接: {device.DeviceName} ({device.DeviceId})", LogCategory.System, data, triggerUIEvent);
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
        /// <param name="triggerUIEvent">是否触发UI更新事件</param>
        public static void LogDeviceStatusChanged(this ILogger logger, IDevice device, DeviceStatus oldStatus, DeviceStatus newStatus, bool? triggerUIEvent = null)
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

            var level = newStatus == DeviceStatus.Error || newStatus == DeviceStatus.Offline 
                ? LogLevel.Warning 
                : LogLevel.Info;

            if (level == LogLevel.Warning)
            {
                logger.Warning($"设备状态变更: {device.DeviceName} ({device.DeviceId}) - {oldStatus} -> {newStatus}", 
                             LogCategory.System, data, triggerUIEvent);
            }
            else
            {
                logger.Info($"设备状态变更: {device.DeviceName} ({device.DeviceId}) - {oldStatus} -> {newStatus}", 
                           LogCategory.System, data, triggerUIEvent);
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
        /// <param name="triggerUIEvent">是否触发UI更新事件</param>
        public static void LogDeviceSend(this ILogger logger, IDevice device, DeviceMessage message, bool success, bool? triggerUIEvent = null)
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
                logger.Debug($"设备发送数据: {device.DeviceName} ({device.DeviceId})", LogCategory.Network, data, triggerUIEvent);
            }
            else
            {
                logger.Warning($"设备发送数据失败: {device.DeviceName} ({device.DeviceId})", LogCategory.Network, data, triggerUIEvent);
            }
        }

        /// <summary>
        /// 记录设备接收数据
        /// </summary>
        /// <param name="logger">日志器</param>
        /// <param name="device">设备对象</param>
        /// <param name="message">消息对象</param>
        /// <param name="triggerUIEvent">是否触发UI更新事件</param>
        public static void LogDeviceReceive(this ILogger logger, IDevice device, DeviceMessage message, bool? triggerUIEvent = null)
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

            logger.Debug($"设备接收数据: {device.DeviceName} ({device.DeviceId})", LogCategory.Network, data, triggerUIEvent);
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
        /// <param name="triggerUIEvent">是否触发UI更新事件</param>
        public static void LogDeviceError(this ILogger logger, IDevice device, string errorMessage, Exception ex = null, int errorCode = -1, bool? triggerUIEvent = null)
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

            logger.Error($"设备错误: {device.DeviceName} ({device.DeviceId}) - {errorMessage}", ex, LogCategory.System, data, triggerUIEvent);
        }

        #endregion

        #region 设备配置日志

        /// <summary>
        /// 记录设备配置变更
        /// </summary>
        /// <param name="logger">日志器</param>
        /// <param name="device">设备对象</param>
        /// <param name="changedProperties">变更的属性列表</param>
        /// <param name="triggerUIEvent">是否触发UI更新事件</param>
        public static void LogDeviceConfigChanged(this ILogger logger, IDevice device, List<string> changedProperties, bool? triggerUIEvent = null)
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

            logger.Info($"设备配置变更: {device.DeviceName} ({device.DeviceId}) - 变更了 {changedProperties?.Count ?? 0} 个属性", 
                       LogCategory.System, data, triggerUIEvent);
        }

        #endregion

        #region 设备注册/注销日志

        /// <summary>
        /// 记录设备注册
        /// </summary>
        /// <param name="logger">日志器</param>
        /// <param name="device">设备对象</param>
        /// <param name="triggerUIEvent">是否触发UI更新事件</param>
        public static void LogDeviceRegistered(this ILogger logger, IDevice device, bool? triggerUIEvent = null)
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

            logger.Info($"设备注册: {device.DeviceName} ({device.DeviceId})", LogCategory.System, data, triggerUIEvent);
        }

        /// <summary>
        /// 记录设备注销
        /// </summary>
        /// <param name="logger">日志器</param>
        /// <param name="deviceId">设备ID</param>
        /// <param name="triggerUIEvent">是否触发UI更新事件</param>
        public static void LogDeviceUnregistered(this ILogger logger, string deviceId, bool? triggerUIEvent = null)
        {
            if (logger == null || string.IsNullOrEmpty(deviceId))
                return;

            var data = new Dictionary<string, object>
            {
                { "device_id", deviceId },
                { "action", "Unregistered" }
            };

            logger.Info($"设备注销: {deviceId}", LogCategory.System, data, triggerUIEvent);
        }

        #endregion
    }
}

