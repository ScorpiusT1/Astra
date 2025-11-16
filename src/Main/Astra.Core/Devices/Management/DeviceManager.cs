using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Astra.Core.Devices.Events;
using Astra.Core.Devices.Interfaces;
using Astra.Core.Devices.Extensions;
using Astra.Core.Logs;
using Astra.Core.Foundation.Common;

namespace Astra.Core.Devices.Management
{
    /// <summary>
    /// 设备管理器实现
    /// </summary>
    public class DeviceManager : IDeviceManager
    {
        private readonly ConcurrentDictionary<string, IDevice> _devices;
        private readonly ConcurrentDictionary<string, DeviceStatus> _deviceStatus;
        private readonly object _lockObject = new object();
        private readonly ILogger _logger;
        private readonly IDeviceUsageService _usageService;
        private readonly IDeviceEventPublisher _eventPublisher;
        private bool _isMonitoring;
        private CancellationTokenSource _monitoringCts;
        private Task _monitoringTask;

        /// <summary>
        /// 监控间隔（毫秒），默认5000ms
        /// </summary>
        public int MonitoringInterval { get; set; } = 5000;

        public DeviceManager(
            ILogger logger = null,
            IDeviceUsageService usageService = null,
            IDeviceEventPublisher eventPublisher = null)
        {
            _devices = new ConcurrentDictionary<string, IDevice>();
            _deviceStatus = new ConcurrentDictionary<string, DeviceStatus>();
            _logger = logger; // 允许为 null，扩展方法会处理

            _usageService = usageService ?? DeviceUsageService.Null;
            _eventPublisher = eventPublisher ?? NullDeviceEventPublisher.Instance;

            DeviceUsageTracker.SetService(_usageService);
        }

        #region 设备注册

        public OperationResult RegisterDevice(IDevice device)
        {
            if (device == null)
                return OperationResult.Fail("设备对象不能为空", ErrorCodes.InvalidData);

            if (string.IsNullOrWhiteSpace(device.DeviceId))
                return OperationResult.Fail("设备ID不能为空", ErrorCodes.InvalidData);

            if (_devices.ContainsKey(device.DeviceId))
                return OperationResult.Fail($"设备 {device.DeviceId} 已存在", ErrorCodes.InvalidData);

            if (_devices.TryAdd(device.DeviceId, device))
            {
                // 订阅设备事件
                SubscribeDeviceEvents(device);

                // 记录初始状态
                _deviceStatus[device.DeviceId] = device.Status;

                // 触发注册事件
                OnDeviceRegistered(new DeviceRegisteredEventArgs
                {
                    DeviceId = device.DeviceId,
                    DeviceType = device.Type
                });

                // 记录日志
                _logger?.LogDeviceRegistered(device);

                _ = _eventPublisher.PublishDeviceAddedAsync(device);

                return OperationResult.Succeed($"设备 {device.DeviceId} 注册成功");
            }

            return OperationResult.Fail($"设备 {device.DeviceId} 注册失败");
        }

        public OperationResult UnregisterDevice(string deviceId)
        {
            if (string.IsNullOrWhiteSpace(deviceId))
                return OperationResult.Fail("设备ID不能为空", ErrorCodes.InvalidData);

            if (!_devices.TryGetValue(deviceId, out var existingDevice))
                return OperationResult.Fail($"设备 {deviceId} 不存在", ErrorCodes.DeviceNotFound);

            if (_usageService.HasConsumers(deviceId, out var consumers) && consumers.Count > 0)
            {
                _ = _eventPublisher.PublishDeviceRemovalBlockedAsync(existingDevice, consumers, "DeviceInUse");
                var detail = string.Join(", ", consumers.Select(c => string.IsNullOrWhiteSpace(c.ConsumerName) ? c.ConsumerId : c.ConsumerName));
                return OperationResult.Fail($"设备 {deviceId} 正被以下使用者引用: {detail}", ErrorCodes.DeviceInUse);
            }

            if (_devices.TryRemove(deviceId, out var device))
            {
                // 取消订阅设备事件
                UnsubscribeDeviceEvents(device);

                // 如果设备在线，先断开
                if (device.IsOnline)
                {
                    device.Disconnect();
                }

                _deviceStatus.TryRemove(deviceId, out _);

                // 触发注销事件
                OnDeviceUnregistered(new DeviceUnregisteredEventArgs
                {
                    DeviceId = deviceId
                });

                // 记录日志
                _logger?.LogDeviceUnregistered(deviceId);

                _usageService.ClearDevice(deviceId);
                _ = _eventPublisher.PublishDeviceRemovedAsync(device);

                return OperationResult.Succeed($"设备 {deviceId} 注销成功");
            }

            return OperationResult.Fail($"设备 {deviceId} 不存在", ErrorCodes.DeviceNotFound);
        }

        public OperationResult<int> RegisterDevices(IEnumerable<IDevice> devices)
        {
            if (devices == null)
                return OperationResult<int>.Fail("设备列表不能为空", ErrorCodes.InvalidData);

            int successCount = 0;
            foreach (var device in devices)
            {
                var result = RegisterDevice(device);
                if (result.Success)
                    successCount++;
            }

            return OperationResult<int>.Succeed(successCount, $"成功注册 {successCount} 个设备");
        }

        public OperationResult UnregisterAllDevices()
        {
            var deviceIds = _devices.Keys.ToList();
            int successCount = 0;

            foreach (var deviceId in deviceIds)
            {
                var result = UnregisterDevice(deviceId);
                if (result.Success)
                    successCount++;
            }

            return OperationResult.Succeed($"成功注销 {successCount} 个设备");
        }

        #endregion

        #region 设备查询

        public OperationResult<IDevice> GetDevice(string deviceId)
        {
            if (_devices.TryGetValue(deviceId, out var device))
            {
                return OperationResult<IDevice>.Succeed(device);
            }

            return OperationResult<IDevice>.Fail($"设备 {deviceId} 不存在", ErrorCodes.DeviceNotFound);
        }

        public OperationResult<List<IDevice>> GetAllDevices()
        {
            var devices = _devices.Values.ToList();
            return OperationResult<List<IDevice>>.Succeed(devices, $"共 {devices.Count} 个设备");
        }

        public OperationResult<List<IDevice>> GetDevicesByType(DeviceType type)
        {
            var devices = _devices.Values
                .Where(d => d.Type == type)
                .ToList();

            return OperationResult<List<IDevice>>.Succeed(devices, $"找到 {devices.Count} 个 {type} 类型的设备");
        }

        public OperationResult<List<IDevice>> GetDevicesByStatus(DeviceStatus status)
        {
            var devices = _devices.Values
                .Where(d => d.Status == status)
                .ToList();

            return OperationResult<List<IDevice>>.Succeed(devices, $"找到 {devices.Count} 个 {status} 状态的设备");
        }

        public bool DeviceExists(string deviceId)
        {
            return _devices.ContainsKey(deviceId);
        }

        public int GetDeviceCount()
        {
            return _devices.Count;
        }

        public int GetDeviceCountByType(DeviceType type)
        {
            return _devices.Values.Count(d => d.Type == type);
        }

        public int GetOnlineDeviceCount()
        {
            return _devices.Values.Count(d => d.IsOnline);
        }

        #endregion

        #region 批量操作

        public OperationResult<Dictionary<string, OperationResult>> ConnectAll()
        {
            var results = new Dictionary<string, OperationResult>();

            foreach (var kvp in _devices)
            {
                results[kvp.Key] = kvp.Value.Connect();
            }

            int successCount = results.Count(r => r.Value.Success);
            return OperationResult<Dictionary<string, OperationResult>>.Succeed(
                results,
                $"批量连接完成: 成功 {successCount}/{results.Count}"
            );
        }

        public OperationResult<Dictionary<string, OperationResult>> DisconnectAll()
        {
            var results = new Dictionary<string, OperationResult>();

            foreach (var kvp in _devices)
            {
                results[kvp.Key] = kvp.Value.Disconnect();
            }

            int successCount = results.Count(r => r.Value.Success);
            return OperationResult<Dictionary<string, OperationResult>>.Succeed(
                results,
                $"批量断开完成: 成功 {successCount}/{results.Count}"
            );
        }

        public OperationResult<Dictionary<string, OperationResult>> ConnectByType(DeviceType type)
        {
            var results = new Dictionary<string, OperationResult>();

            var devicesToConnect = _devices.Where(kvp => kvp.Value.Type == type);

            foreach (var kvp in devicesToConnect)
            {
                results[kvp.Key] = kvp.Value.Connect();
            }

            int successCount = results.Count(r => r.Value.Success);
            return OperationResult<Dictionary<string, OperationResult>>.Succeed(
                results,
                $"批量连接 {type} 设备完成: 成功 {successCount}/{results.Count}"
            );
        }

        public OperationResult<Dictionary<string, OperationResult>> DisconnectByType(DeviceType type)
        {
            var results = new Dictionary<string, OperationResult>();

            var devicesToDisconnect = _devices.Where(kvp => kvp.Value.Type == type);

            foreach (var kvp in devicesToDisconnect)
            {
                results[kvp.Key] = kvp.Value.Disconnect();
            }

            int successCount = results.Count(r => r.Value.Success);
            return OperationResult<Dictionary<string, OperationResult>>.Succeed(
                results,
                $"批量断开 {type} 设备完成: 成功 {successCount}/{results.Count}"
            );
        }

        public OperationResult<Dictionary<string, OperationResult>> BroadcastMessage(DeviceMessage message)
        {
            var results = new Dictionary<string, OperationResult>();

            foreach (var kvp in _devices)
            {
                if (kvp.Value.IsOnline)
                {
                    if (kvp.Value is IDataTransfer dataTransfer)
                    {
                        results[kvp.Key] = dataTransfer.Send(message);
                    }
                    else
                    {
                        results[kvp.Key] = OperationResult.Fail("设备不支持数据传输", ErrorCodes.NotSupported);
                    }
                }
                else
                {
                    results[kvp.Key] = OperationResult.Fail("设备未在线", ErrorCodes.DeviceNotOnline);
                }
            }

            int successCount = results.Count(r => r.Value.Success);
            return OperationResult<Dictionary<string, OperationResult>>.Succeed(
                results,
                $"广播消息完成: 成功 {successCount}/{results.Count}"
            );
        }

        public OperationResult<Dictionary<string, OperationResult>> BroadcastMessageByType(DeviceType type, DeviceMessage message)
        {
            var results = new Dictionary<string, OperationResult>();

            var devicesToSend = _devices.Where(kvp => kvp.Value.Type == type);

            foreach (var kvp in devicesToSend)
            {
                if (kvp.Value.IsOnline)
                {
                    if (kvp.Value is IDataTransfer dataTransfer)
                    {
                        results[kvp.Key] = dataTransfer.Send(message);
                    }
                    else
                    {
                        results[kvp.Key] = OperationResult.Fail("设备不支持数据传输", ErrorCodes.NotSupported);
                    }
                }
                else
                {
                    results[kvp.Key] = OperationResult.Fail("设备未在线", ErrorCodes.DeviceNotOnline);
                }
            }

            int successCount = results.Count(r => r.Value.Success);
            return OperationResult<Dictionary<string, OperationResult>>.Succeed(
                results,
                $"向 {type} 设备广播消息完成: 成功 {successCount}/{results.Count}"
            );
        }

        #endregion

        #region 设备监控

        public OperationResult<Dictionary<string, DeviceStatus>> GetAllDeviceStatus()
        {
            var statusDict = _devices.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.Status
            );

            return OperationResult<Dictionary<string, DeviceStatus>>.Succeed(statusDict);
        }

        public OperationResult<DeviceStatistics> GetStatistics()
        {
            var statistics = new DeviceStatistics
            {
                TotalDevices = _devices.Count,
                OnlineDevices = _devices.Values.Count(d => d.IsOnline),
                OfflineDevices = _devices.Values.Count(d => !d.IsOnline),
                ErrorDevices = _devices.Values.Count(d => d.Status == DeviceStatus.Error)
            };

            // 按类型统计
            foreach (DeviceType type in Enum.GetValues(typeof(DeviceType)))
            {
                statistics.DevicesByType[type] = _devices.Values.Count(d => d.Type == type);
            }

            // 按状态统计
            foreach (DeviceStatus status in Enum.GetValues(typeof(DeviceStatus)))
            {
                statistics.DevicesByStatus[status] = _devices.Values.Count(d => d.Status == status);
            }

            statistics.LastUpdateTime = DateTime.Now;

            return OperationResult<DeviceStatistics>.Succeed(statistics);
        }

        public OperationResult StartMonitoring()
        {
            lock (_lockObject)
            {
                if (_isMonitoring)
                    return OperationResult.Fail("监控已在运行中");

                _isMonitoring = true;
                _monitoringCts = new CancellationTokenSource();

                _monitoringTask = Task.Run(async () =>
                {
                    while (!_monitoringCts.Token.IsCancellationRequested)
                    {
                        try
                        {
                            // 检查所有设备状态
                            foreach (var kvp in _devices)
                            {
                                var device = kvp.Value;
                                var currentStatus = device.Status;

                                // 如果状态发生变化，更新记录
                                if (_deviceStatus.TryGetValue(kvp.Key, out var lastStatus) && lastStatus != currentStatus)
                                {
                                    _deviceStatus[kvp.Key] = currentStatus;
                                }
                            }

                            // 按配置的间隔检查
                            await Task.Delay(MonitoringInterval, _monitoringCts.Token);
                        }
                        catch (OperationCanceledException)
                        {
                            break;
                        }
                        catch (Exception ex)
                        {
                            _logger?.Error($"监控异常: {ex.Message}", ex, LogCategory.System);
                        }
                    }
                }, _monitoringCts.Token);

                return OperationResult.Succeed("设备监控已启动");
            }
        }

        public OperationResult StopMonitoring()
        {
            lock (_lockObject)
            {
                if (!_isMonitoring)
                    return OperationResult.Fail("监控未在运行");

                _isMonitoring = false;
                _monitoringCts?.Cancel();

                try
                {
                    _monitoringTask?.Wait(TimeSpan.FromSeconds(10));
                }
                catch (Exception ex)
                {
                    return OperationResult.Fail($"停止监控失败: {ex.Message}", ex);
                }

                return OperationResult.Succeed("设备监控已停止");
            }
        }

        #endregion

        #region 事件处理

        public event EventHandler<DeviceRegisteredEventArgs> DeviceRegistered;
        public event EventHandler<DeviceUnregisteredEventArgs> DeviceUnregistered;
        public event EventHandler<DeviceStatusChangedEventArgs> DeviceStatusChanged;
        public event EventHandler<DeviceErrorEventArgs> DeviceError;

        protected virtual void OnDeviceRegistered(DeviceRegisteredEventArgs e)
        {
            DeviceRegistered?.Invoke(this, e);
        }

        protected virtual void OnDeviceUnregistered(DeviceUnregisteredEventArgs e)
        {
            DeviceUnregistered?.Invoke(this, e);
        }

        private void SubscribeDeviceEvents(IDevice device)
        {
            device.StatusChanged += Device_StatusChanged;
            device.ErrorOccurred += Device_ErrorOccurred;
        }

        private void UnsubscribeDeviceEvents(IDevice device)
        {
            device.StatusChanged -= Device_StatusChanged;
            device.ErrorOccurred -= Device_ErrorOccurred;
        }

        private void Device_StatusChanged(object sender, DeviceStatusChangedEventArgs e)
        {
            // 记录状态变更日志
            if (sender is IDevice device)
            {
                _logger?.LogDeviceStatusChanged(device, e.OldStatus, e.NewStatus);
            }

            DeviceStatusChanged?.Invoke(sender, e);
        }

        private void Device_ErrorOccurred(object sender, DeviceErrorEventArgs e)
        {
            // 记录错误日志
            if (sender is IDevice device)
            {
                _logger?.LogDeviceError(device, e.ErrorMessage, e.Exception, e.ErrorCode);
            }

            DeviceError?.Invoke(sender, e);
        }

        #endregion

        #region 资源释放

        private bool _disposed;

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                // 停止监控
                StopMonitoring();

                // 清理所有设备的事件订阅并断开连接
                var deviceIds = _devices.Keys.ToList();
                foreach (var deviceId in deviceIds)
                {
                    if (_devices.TryGetValue(deviceId, out var device))
                    {
                        // 取消订阅设备事件
                        UnsubscribeDeviceEvents(device);

                        // 断开连接
                        if (device.IsOnline)
                        {
                            try
                            {
                                device.Disconnect();
                            }
                            catch
                            {
                                // 忽略断开时的异常，确保清理继续
                            }
                        }

                        // 如果设备实现了IDisposable，则释放
                        if (device is IDisposable disposable)
                        {
                            try
                            {
                                disposable.Dispose();
                            }
                            catch
                            {
                                // 忽略释放时的异常
                            }
                        }
                    }
                }

                // 清理所有事件订阅
                ClearAllEventSubscriptions();

                // 清空字典
                _devices.Clear();
                _deviceStatus.Clear();

                // 释放资源
                _monitoringCts?.Dispose();
                _monitoringTask?.Dispose();
            }

            _disposed = true;
        }

        /// <summary>
        /// 清理所有事件订阅
        /// </summary>
        private void ClearAllEventSubscriptions()
        {
            DeviceRegistered = null;
            DeviceUnregistered = null;
            DeviceStatusChanged = null;
            DeviceError = null;
        }

        #endregion
    }
}