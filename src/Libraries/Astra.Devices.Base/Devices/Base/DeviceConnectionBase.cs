using System;
using Astra.Core.Devices;
using Astra.Core.Foundation.Common;
using System.Threading;
using System.Threading.Tasks;
using Astra.Core.Devices.Interfaces;

namespace Astra.Core.Devices.Base
{
    /// <summary>
    /// 设备连接管理基类
    /// </summary>
    public abstract class DeviceConnectionBase : IDeviceConnection, IHeartbeatMonitor, IDisposable
    {
        protected DeviceStatus _status = DeviceStatus.Uninitialized;
        protected readonly object _statusLock = new object();

        protected bool _isHeartbeatRunning;
        protected CancellationTokenSource _heartbeatCts;
        protected Task _heartbeatTask;
        protected int _heartbeatFailCount;
        protected readonly object _heartbeatLock = new object();

        protected bool _isReconnecting;
        protected CancellationTokenSource _reconnectCts;
        protected Task _reconnectTask;
        protected int _reconnectAttemptCount;
        protected readonly object _reconnectLock = new object();

        public DeviceStatus Status
        {
            get
            {
                lock (_statusLock)
                {
                    return _status;
                }
            }
            protected set
            {
                lock (_statusLock)
                {
                    if (_status != value)
                    {
                        var oldStatus = _status;
                        _status = value;
                        OnStatusChanged(new DeviceStatusChangedEventArgs
                        {
                            OldStatus = oldStatus,
                            NewStatus = value,
                            Message = $"状态从 {oldStatus} 变更为 {value}"
                        });
                    }
                }
            }
        }

        public bool IsOnline => Status == DeviceStatus.Online;

        public int HeartbeatInterval { get; set; } = 5000;
        public int HeartbeatTimeoutThreshold { get; set; } = 3;
        public bool IsHeartbeatRunning => _isHeartbeatRunning;

        public bool AutoReconnectEnabled { get; set; } = true;
        public int MaxReconnectAttempts { get; set; } = 5;
        public int InitialReconnectDelay { get; set; } = 1000;
        public int MaxReconnectDelay { get; set; } = 30000;
        public double ReconnectDelayMultiplier { get; set; } = 2.0;
        public bool IsReconnecting => _isReconnecting;

        public event EventHandler<DeviceStatusChangedEventArgs> StatusChanged;
        public event EventHandler<DeviceErrorEventArgs> ErrorOccurred;

        protected virtual void OnStatusChanged(DeviceStatusChangedEventArgs e)
        {
            StatusChanged?.Invoke(this, e);
        }

        protected virtual void OnErrorOccurred(DeviceErrorEventArgs e)
        {
            ErrorOccurred?.Invoke(this, e);
        }

        public virtual OperationResult Connect()
        {
            if (IsOnline)
                return OperationResult.Succeed("设备已在线");

            Status = DeviceStatus.Connecting;

            try
            {
                var result = DoConnect();

                if (result.Success)
                {
                    Status = DeviceStatus.Connected;

                    var aliveResult = IsAlive();
                    if (aliveResult.Success && aliveResult.Data)
                    {
                        StopAutoReconnect();

                        Status = DeviceStatus.Online;
                        return OperationResult.Succeed("设备连接成功");
                    }
                    else
                    {
                        DoDisconnect();
                        Status = DeviceStatus.Disconnected;
                        return OperationResult.Failure("设备连接成功但无响应", ErrorCodes.DeviceNoResponse);
                    }
                }
                else
                {
                    Status = DeviceStatus.Error;
                    OnErrorOccurred(new DeviceErrorEventArgs
                    {
                        ErrorMessage = result.ErrorMessage,
                        ErrorCode = result.ErrorCode,
                        Exception = result.Exception
                    });
                    return result;
                }
            }
            catch (Exception ex)
            {
                Status = DeviceStatus.Error;
                OnErrorOccurred(new DeviceErrorEventArgs
                {
                    ErrorMessage = ex.Message,
                    Exception = ex
                });
                return OperationResult.Fail($"连接失败: {ex.Message}", ex, ErrorCodes.ConnectFailed);
            }
        }

        public virtual async Task<OperationResult> ConnectAsync(CancellationToken cancellationToken = default)
        {
            if (IsOnline)
                return OperationResult.Succeed("设备已在线");

            Status = DeviceStatus.Connecting;

            try
            {
                var result = await DoConnectAsync(cancellationToken);

                if (result.Success)
                {
                    Status = DeviceStatus.Connected;

                    var aliveResult = await IsAliveAsync(cancellationToken);
                    if (aliveResult.Success && aliveResult.Data)
                    {
                        StopAutoReconnect();

                        Status = DeviceStatus.Online;
                        return OperationResult.Succeed("设备连接成功");
                    }
                    else
                    {
                        await DoDisconnectAsync(cancellationToken);
                        Status = DeviceStatus.Disconnected;
                        return OperationResult.Failure("设备连接成功但无响应", ErrorCodes.DeviceNoResponse);
                    }
                }
                else
                {
                    Status = DeviceStatus.Error;
                    OnErrorOccurred(new DeviceErrorEventArgs
                    {
                        ErrorMessage = result.ErrorMessage,
                        ErrorCode = result.ErrorCode,
                        Exception = result.Exception
                    });
                    return result;
                }
            }
            catch (OperationCanceledException)
            {
                Status = DeviceStatus.Disconnected;
                return OperationResult.Failure("连接操作已取消");
            }
            catch (Exception ex)
            {
                Status = DeviceStatus.Error;
                OnErrorOccurred(new DeviceErrorEventArgs
                {
                    ErrorMessage = ex.Message,
                    Exception = ex
                });
                return OperationResult.Fail($"连接失败: {ex.Message}", ex, ErrorCodes.ConnectFailed);
            }
        }

        public virtual OperationResult Disconnect()
        {
            if (!IsOnline && Status != DeviceStatus.Connected)
                return OperationResult.Succeed("设备未连接");

            Status = DeviceStatus.Disconnecting;

            try
            {
                StopAutoReconnect();

                if (IsHeartbeatRunning)
                {
                    StopHeartbeat();
                }

                var result = DoDisconnect();

                if (result.Success)
                {
                    Status = DeviceStatus.Disconnected;
                    return OperationResult.Succeed("设备断开成功");
                }
                else
                {
                    Status = DeviceStatus.Error;
                    return result;
                }
            }
            catch (Exception ex)
            {
                Status = DeviceStatus.Error;
                OnErrorOccurred(new DeviceErrorEventArgs
                {
                    ErrorMessage = ex.Message,
                    Exception = ex
                });
                return OperationResult.Fail($"断开失败: {ex.Message}", ex, ErrorCodes.DisconnectFailed);
            }
        }

        public virtual async Task<OperationResult> DisconnectAsync(CancellationToken cancellationToken = default)
        {
            if (!IsOnline && Status != DeviceStatus.Connected)
                return OperationResult.Succeed("设备未连接");

            Status = DeviceStatus.Disconnecting;

            try
            {
                StopAutoReconnect();

                if (IsHeartbeatRunning)
                {
                    StopHeartbeat();
                }

                var result = await DoDisconnectAsync(cancellationToken);

                if (result.Success)
                {
                    Status = DeviceStatus.Disconnected;
                    return OperationResult.Succeed("设备断开成功");
                }
                else
                {
                    Status = DeviceStatus.Error;
                    return result;
                }
            }
            catch (OperationCanceledException)
            {
                Status = DeviceStatus.Disconnected;
                return OperationResult.Failure("断开操作已取消");
            }
            catch (Exception ex)
            {
                Status = DeviceStatus.Error;
                OnErrorOccurred(new DeviceErrorEventArgs
                {
                    ErrorMessage = ex.Message,
                    Exception = ex
                });
                return OperationResult.Fail($"断开失败: {ex.Message}", ex, ErrorCodes.DisconnectFailed);
            }
        }

        public virtual OperationResult Reset()
        {
            var disconnectResult = Disconnect();
            if (!disconnectResult.Success)
                return disconnectResult;

            Thread.Sleep(1000);

            return Connect();
        }

        public virtual async Task<OperationResult> ResetAsync(CancellationToken cancellationToken = default)
        {
            var disconnectResult = await DisconnectAsync(cancellationToken);
            if (!disconnectResult.Success)
                return disconnectResult;

            await Task.Delay(1000, cancellationToken);

            return await ConnectAsync(cancellationToken);
        }

        public virtual OperationResult<bool> DeviceExists()
        {
            try
            {
                var exists = DoCheckDeviceExists();
                return OperationResult<bool>.Succeed(exists);
            }
            catch (Exception ex)
            {
                return OperationResult<bool>.Fail($"检测设备存在性失败: {ex.Message}", ex);
            }
        }

        public virtual async Task<OperationResult<bool>> DeviceExistsAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var exists = await DoCheckDeviceExistsAsync(cancellationToken);
                return OperationResult<bool>.Succeed(exists);
            }
            catch (Exception ex)
            {
                return OperationResult<bool>.Fail($"检测设备存在性失败: {ex.Message}", ex);
            }
        }

        public virtual OperationResult<bool> IsAlive()
        {
            if (!IsOnline)
                return OperationResult<bool>.Succeed(false, "设备未在线");

            try
            {
                var alive = DoCheckAlive();
                return OperationResult<bool>.Succeed(alive);
            }
            catch (Exception ex)
            {
                return OperationResult<bool>.Fail($"检测设备活动性失败: {ex.Message}", ex);
            }
        }

        public virtual async Task<OperationResult<bool>> IsAliveAsync(CancellationToken cancellationToken = default)
        {
            if (!IsOnline)
                return OperationResult<bool>.Succeed(false, "设备未在线");

            try
            {
                var alive = await DoCheckAliveAsync(cancellationToken);
                return OperationResult<bool>.Succeed(alive);
            }
            catch (Exception ex)
            {
                return OperationResult<bool>.Fail($"检测设备活动性失败: {ex.Message}", ex);
            }
        }

        public virtual OperationResult StartHeartbeat()
        {
            lock (_heartbeatLock)
            {
                if (_isHeartbeatRunning)
                    return OperationResult.Failure("心跳已在运行", ErrorCodes.HeartbeatAlreadyRunning);

                if (!IsOnline)
                    return OperationResult.Failure("设备未在线", ErrorCodes.DeviceNotOnline);

                _isHeartbeatRunning = true;
                _heartbeatFailCount = 0;
                _heartbeatCts = new CancellationTokenSource();

                _heartbeatTask = Task.Run(async () =>
                {
                    while (!_heartbeatCts.Token.IsCancellationRequested)
                    {
                        try
                        {
                            await Task.Delay(HeartbeatInterval, _heartbeatCts.Token);

                            var aliveResult = await IsAliveAsync(_heartbeatCts.Token);

                            if (aliveResult.Success && aliveResult.Data)
                            {
                                _heartbeatFailCount = 0;
                            }
                            else
                            {
                                _heartbeatFailCount++;

                                if (_heartbeatFailCount >= HeartbeatTimeoutThreshold)
                                {
                                    Status = DeviceStatus.Offline;
                                    OnErrorOccurred(new DeviceErrorEventArgs
                                    {
                                        ErrorMessage = $"心跳超时: 连续 {_heartbeatFailCount} 次失败",
                                        ErrorCode = ErrorCodes.HeartbeatTimeout
                                    });

                                    if (AutoReconnectEnabled)
                                    {
                                        StartAutoReconnect();
                                    }

                                    break;
                                }
                            }
                        }
                        catch (OperationCanceledException)
                        {
                            break;
                        }
                        catch (Exception ex)
                        {
                            OnErrorOccurred(new DeviceErrorEventArgs
                            {
                                ErrorMessage = $"心跳异常: {ex.Message}",
                                ErrorCode = ErrorCodes.HeartbeatError,
                                Exception = ex
                            });
                        }
                    }
                }, _heartbeatCts.Token);

                return OperationResult.Succeed("心跳已启动");
            }
        }

        public virtual OperationResult StopHeartbeat()
        {
            lock (_heartbeatLock)
            {
                if (!_isHeartbeatRunning)
                    return OperationResult.Failure("心跳未运行", ErrorCodes.HeartbeatNotRunning);

                _isHeartbeatRunning = false;
                _heartbeatCts?.Cancel();

                try
                {
                    _heartbeatTask?.Wait(TimeSpan.FromSeconds(5));
                }
                catch (Exception ex)
                {
                    return OperationResult.Fail($"停止心跳失败: {ex.Message}", ex);
                }

                return OperationResult.Succeed("心跳已停止");
            }
        }

        protected virtual void StartAutoReconnect()
        {
            lock (_reconnectLock)
            {
                if (_isReconnecting)
                    return;

                _isReconnecting = true;
                _reconnectAttemptCount = 0;
                _reconnectCts = new CancellationTokenSource();

                _reconnectTask = Task.Run(async () =>
                {
                    while (!_reconnectCts.Token.IsCancellationRequested)
                    {
                        if (MaxReconnectAttempts >= 0 && _reconnectAttemptCount >= MaxReconnectAttempts)
                        {
                            Status = DeviceStatus.Error;
                            OnErrorOccurred(new DeviceErrorEventArgs
                            {
                                ErrorMessage = $"自动重连失败: 已达到最大重连次数 ({MaxReconnectAttempts})",
                                ErrorCode = ErrorCodes.ConnectFailed
                            });
                            break;
                        }

                        int delay = CalculateReconnectDelay(_reconnectAttemptCount);
                        _reconnectAttemptCount++;

                        try
                        {
                            await Task.Delay(delay, _reconnectCts.Token);

                            Status = DeviceStatus.Connecting;
                            var connectResult = await ConnectAsync(_reconnectCts.Token);

                            if (connectResult.Success && IsOnline)
                            {
                                _heartbeatFailCount = 0;

                                if (!IsHeartbeatRunning)
                                {
                                    StartHeartbeat();
                                }

                                OnErrorOccurred(new DeviceErrorEventArgs
                                {
                                    ErrorMessage = $"自动重连成功，尝试次数: {_reconnectAttemptCount}",
                                    ErrorCode = 0
                                });

                                _isReconnecting = false;
                                break;
                            }
                            else
                            {
                                OnErrorOccurred(new DeviceErrorEventArgs
                                {
                                    ErrorMessage = $"自动重连失败（第 {_reconnectAttemptCount} 次尝试）: {connectResult.ErrorMessage}",
                                    ErrorCode = ErrorCodes.ConnectFailed
                                });
                            }
                        }
                        catch (OperationCanceledException)
                        {
                            break;
                        }
                        catch (Exception ex)
                        {
                            OnErrorOccurred(new DeviceErrorEventArgs
                            {
                                ErrorMessage = $"自动重连异常（第 {_reconnectAttemptCount} 次尝试）: {ex.Message}",
                                ErrorCode = ErrorCodes.ConnectFailed,
                                Exception = ex
                            });
                        }
                    }
                }, _reconnectCts.Token);
            }
        }

        protected virtual void StopAutoReconnect()
        {
            lock (_reconnectLock)
            {
                if (!_isReconnecting)
                    return;

                _isReconnecting = false;
                _reconnectCts?.Cancel();

                try
                {
                    _reconnectTask?.Wait(TimeSpan.FromSeconds(5));
                }
                catch
                {
                }
            }
        }

        protected virtual int CalculateReconnectDelay(int attemptCount)
        {
            if (attemptCount <= 0)
                return InitialReconnectDelay;

            double delay = InitialReconnectDelay * Math.Pow(ReconnectDelayMultiplier, attemptCount);
            int delayMs = (int)Math.Min(delay, MaxReconnectDelay);
            return Math.Max(delayMs, InitialReconnectDelay);
        }

        protected abstract OperationResult DoConnect();
        protected abstract Task<OperationResult> DoConnectAsync(CancellationToken cancellationToken);
        protected abstract OperationResult DoDisconnect();
        protected abstract Task<OperationResult> DoDisconnectAsync(CancellationToken cancellationToken);
        protected abstract bool DoCheckDeviceExists();
        protected abstract Task<bool> DoCheckDeviceExistsAsync(CancellationToken cancellationToken);
        protected abstract bool DoCheckAlive();
        protected abstract Task<bool> DoCheckAliveAsync(CancellationToken cancellationToken);

        private bool _disposed;

        public virtual void Dispose()
        {
            if (_disposed)
                return;

            StopAutoReconnect();

            if (IsHeartbeatRunning)
            {
                StopHeartbeat();
            }

            if (IsOnline || Status == DeviceStatus.Connected)
            {
                Disconnect();
            }

            _heartbeatCts?.Dispose();
            _reconnectCts?.Dispose();

            _disposed = true;
        }
    }
}

