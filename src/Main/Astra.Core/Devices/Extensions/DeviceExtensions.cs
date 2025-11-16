using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Astra.Core.Devices.Interfaces;
using Astra.Core.Foundation.Common;

namespace Astra.Core.Devices.Extensions
{
    /// <summary>
    /// 设备扩展方法
    /// </summary>
    public static class DeviceExtensions
    {
        #region 连接扩展

        /// <summary>
        /// 连接并验证设备
        /// </summary>
        public static OperationResult ConnectAndValidate(this IDevice device)
        {
            var connectResult = device.Connect();
            if (!connectResult.Success)
                return connectResult;

            var validateResult = device.IsAlive();
            if (!validateResult.Success || !validateResult.Data)
            {
                device.Disconnect();
                return OperationResult.Fail("设备连接成功但验证失败", ErrorCodes.DeviceNoResponse);
            }

            return OperationResult.Succeed("设备连接并验证成功");
        }

        /// <summary>
        /// 异步连接并验证设备
        /// </summary>
        public static async Task<OperationResult> ConnectAndValidateAsync(this IDevice device, CancellationToken cancellationToken = default)
        {
            var connectResult = await device.ConnectAsync(cancellationToken);
            if (!connectResult.Success)
                return connectResult;

            var validateResult = await device.IsAliveAsync(cancellationToken);
            if (!validateResult.Success || !validateResult.Data)
            {
                await device.DisconnectAsync(cancellationToken);
                return OperationResult.Fail("设备连接成功但验证失败", ErrorCodes.DeviceNoResponse);
            }

            return OperationResult.Succeed("设备连接并验证成功");
        }

        /// <summary>
        /// 重连设备（先断开再连接）
        /// </summary>
        public static OperationResult Reconnect(this IDevice device)
        {
            if (device.IsOnline)
            {
                var disconnectResult = device.Disconnect();
                if (!disconnectResult.Success)
                    return disconnectResult;
            }

            return device.Connect();
        }

        /// <summary>
        /// 异步重连设备
        /// </summary>
        public static async Task<OperationResult> ReconnectAsync(this IDevice device, CancellationToken cancellationToken = default)
        {
            if (device.IsOnline)
            {
                var disconnectResult = await device.DisconnectAsync(cancellationToken);
                if (!disconnectResult.Success)
                    return disconnectResult;
            }

            return await device.ConnectAsync(cancellationToken);
        }

        /// <summary>
        /// 确保设备在线（如果不在线则连接）
        /// </summary>
        public static OperationResult EnsureOnline(this IDevice device)
        {
            if (device.IsOnline)
                return OperationResult.Succeed("设备已在线");

            return device.Connect();
        }

        #endregion

        #region 数据传输扩展

        /// <summary>
        /// 发送并接收（请求-响应模式）
        /// 仅适用于实现了 IDataTransfer 接口的设备
        /// </summary>
        public static OperationResult<DeviceMessage> SendAndReceive(this IDataTransfer device, DeviceMessage message, int timeoutMs = 5000)
        {
            var sendResult = device.Send(message);
            if (!sendResult.Success)
                return OperationResult<DeviceMessage>.Fail(sendResult.ErrorMessage, sendResult.ErrorCode);

            // 等待响应
            var startTime = DateTime.Now;
            while ((DateTime.Now - startTime).TotalMilliseconds < timeoutMs)
            {
                var receiveResult = device.Receive();
                if (receiveResult.Success)
                {
                    return receiveResult;
                }

                Thread.Sleep(10);
            }

            return OperationResult<DeviceMessage>.Fail("接收超时", ErrorCodes.ReceiveTimeout);
        }

        /// <summary>
        /// 发送字节数组（便捷方法）
        /// 仅适用于实现了 IDataTransfer 接口的设备
        /// </summary>
        public static OperationResult SendBytes(this IDataTransfer device, byte[] data, string channelId = null)
        {
            var message = new DeviceMessage(data, channelId);
            return device.Send(message);
        }

        /// <summary>
        /// 接收字节数组（便捷方法）
        /// 仅适用于实现了 IDataTransfer 接口的设备
        /// </summary>
        public static OperationResult<byte[]> ReceiveBytes(this IDataTransfer device, string channelId = null)
        {
            var result = string.IsNullOrEmpty(channelId)
                ? device.Receive()
                : device.Receive(channelId);

            if (result.Success)
            {
                return OperationResult<byte[]>.Succeed(result.Data.Data);
            }

            return OperationResult<byte[]>.Fail(result.ErrorMessage, result.ErrorCode);
        }

        /// <summary>
        /// 批量发送（适用于实现了 IBatchDataTransfer 接口的设备）
        /// </summary>
        public static OperationResult<int> SendBatch(this IBatchDataTransfer device, params DeviceMessage[] messages)
        {
            return device.BatchSend(messages);
        }

        #endregion

        #region 高速设备扩展

        // 注意：数据采集相关的扩展方法已移除
        // 数据采集功能应该由数据采集插件提供

        #endregion

        #region 设备信息扩展

        /// <summary>
        /// 获取设备完整信息（格式化字符串）
        /// </summary>
        public static string GetDeviceInfoString(this IDevice device)
        {
            var info = device.GetDeviceInfo();
            var lines = new List<string>
            {
                $"设备ID: {device.DeviceId}",
                $"设备名称: {device.DeviceName}",
                $"设备类型: {device.Type}",
                $"设备状态: {device.Status}",
                $"在线状态: {(device.IsOnline ? "在线" : "离线")}"
            };

            foreach (var kvp in info)
            {
                lines.Add($"{kvp.Key}: {kvp.Value}");
            }

            return string.Join(Environment.NewLine, lines);
        }

        /// <summary>
        /// 判断设备是否健康
        /// </summary>
        public static bool IsHealthy(this IDevice device)
        {
            return device.IsOnline &&
                   device.Status != DeviceStatus.Error &&
                   device.Status != DeviceStatus.Offline;
        }

        #endregion

        #region 链式调用扩展

        /// <summary>
        /// 执行操作并返回设备（支持链式调用）
        /// </summary>
        public static IDevice Do(this IDevice device, Action<IDevice> action)
        {
            action?.Invoke(device);
            return device;
        }

        /// <summary>
        /// 条件执行
        /// </summary>
        public static IDevice DoIf(this IDevice device, bool condition, Action<IDevice> action)
        {
            if (condition)
            {
                action?.Invoke(device);
            }
            return device;
        }

        /// <summary>
        /// 安全执行（捕获异常）
        /// </summary>
        public static IDevice DoSafely(this IDevice device, Action<IDevice> action, Action<Exception> onError = null)
        {
            try
            {
                action?.Invoke(device);
            }
            catch (Exception ex)
            {
                onError?.Invoke(ex);
            }
            return device;
        }

        #endregion

        #region 批量操作扩展

        /// <summary>
        /// 批量连接设备
        /// </summary>
        public static Dictionary<string, OperationResult> ConnectAll(this IEnumerable<IDevice> devices)
        {
            return devices.ToDictionary(
                d => d.DeviceId,
                d => d.Connect()
            );
        }

        /// <summary>
        /// 批量断开设备
        /// </summary>
        public static Dictionary<string, OperationResult> DisconnectAll(this IEnumerable<IDevice> devices)
        {
            return devices.ToDictionary(
                d => d.DeviceId,
                d => d.Disconnect()
            );
        }

        /// <summary>
        /// 筛选在线设备
        /// </summary>
        public static IEnumerable<IDevice> WhereOnline(this IEnumerable<IDevice> devices)
        {
            return devices.Where(d => d.IsOnline);
        }

        /// <summary>
        /// 筛选离线设备
        /// </summary>
        public static IEnumerable<IDevice> WhereOffline(this IEnumerable<IDevice> devices)
        {
            return devices.Where(d => !d.IsOnline);
        }

        /// <summary>
        /// 按类型筛选
        /// </summary>
        public static IEnumerable<IDevice> OfDeviceType(this IEnumerable<IDevice> devices, DeviceType type)
        {
            return devices.Where(d => d.Type == type);
        }

        /// <summary>
        /// 按状态筛选
        /// </summary>
        public static IEnumerable<IDevice> OfStatus(this IEnumerable<IDevice> devices, DeviceStatus status)
        {
            return devices.Where(d => d.Status == status);
        }

        #endregion

        #region 重试机制扩展

        /// <summary>
        /// 带重试的连接
        /// </summary>
        public static OperationResult ConnectWithRetry(this IDevice device, int maxRetries = 3, int retryDelayMs = 1000)
        {
            for (int i = 0; i < maxRetries; i++)
            {
                var result = device.Connect();
                if (result.Success)
                    return result;

                if (i < maxRetries - 1)
                {
                    Thread.Sleep(retryDelayMs);
                }
            }

            return OperationResult.Fail($"连接失败，已重试 {maxRetries} 次", ErrorCodes.ConnectFailed);
        }

        /// <summary>
        /// 带重试的发送
        /// 仅适用于实现了 IDataTransfer 接口的设备
        /// </summary>
        public static OperationResult SendWithRetry(this IDataTransfer device, DeviceMessage message, int maxRetries = 3, int retryDelayMs = 100)
        {
            for (int i = 0; i < maxRetries; i++)
            {
                var result = device.Send(message);
                if (result.Success)
                    return result;

                if (i < maxRetries - 1)
                {
                    Thread.Sleep(retryDelayMs);
                }
            }

            return OperationResult.Fail($"发送失败，已重试 {maxRetries} 次", ErrorCodes.SendFailed);
        }

        /// <summary>
        /// 带重试的异步操作
        /// </summary>
        public static async Task<OperationResult> RetryAsync(
            Func<Task<OperationResult>> operation,
            int maxRetries = 3,
            int retryDelayMs = 1000,
            CancellationToken cancellationToken = default)
        {
            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    var result = await operation();
                    if (result.Success)
                        return result;

                    if (i < maxRetries - 1)
                    {
                        await Task.Delay(retryDelayMs, cancellationToken);
                    }
                }
                catch (OperationCanceledException)
                {
                    return OperationResult.Fail("操作已取消");
                }
                catch (Exception ex)
                {
                    if (i == maxRetries - 1)
                    {
                        return OperationResult.Fail($"操作失败: {ex.Message}", ex);
                    }

                    await Task.Delay(retryDelayMs, cancellationToken);
                }
            }

            return OperationResult.Fail($"操作失败，已重试 {maxRetries} 次");
        }

        #endregion

        #region 性能监控扩展

        /// <summary>
        /// 测量操作执行时间
        /// </summary>
        public static OperationResult<TimeSpan> MeasureExecutionTime(this IDevice device, Action<IDevice> operation)
        {
            var startTime = DateTime.Now;

            try
            {
                operation(device);
                var elapsed = DateTime.Now - startTime;
                return OperationResult<TimeSpan>.Succeed(elapsed, $"操作耗时: {elapsed.TotalMilliseconds:F2}ms");
            }
            catch (Exception ex)
            {
                var elapsed = DateTime.Now - startTime;
                return OperationResult<TimeSpan>.Fail($"操作失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 异步测量执行时间
        /// </summary>
        public static async Task<OperationResult<TimeSpan>> MeasureExecutionTimeAsync(
            this IDevice device,
            Func<IDevice, Task> operation)
        {
            var startTime = DateTime.Now;

            try
            {
                await operation(device);
                var elapsed = DateTime.Now - startTime;
                return OperationResult<TimeSpan>.Succeed(elapsed, $"操作耗时: {elapsed.TotalMilliseconds:F2}ms");
            }
            catch (Exception ex)
            {
                var elapsed = DateTime.Now - startTime;
                return OperationResult<TimeSpan>.Fail($"操作失败: {ex.Message}", ex);
            }
        }

        #endregion
    }
}