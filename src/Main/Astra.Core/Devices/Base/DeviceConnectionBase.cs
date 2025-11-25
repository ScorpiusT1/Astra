using System;
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

        // 心跳相关
        protected bool _isHeartbeatRunning;
        protected CancellationTokenSource _heartbeatCts;
        protected Task _heartbeatTask;
        protected int _heartbeatFailCount;
        protected readonly object _heartbeatLock = new object();

        // 自动重连相关
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

        #region 心跳配置

        public int HeartbeatInterval { get; set; } = 5000;
        public int HeartbeatTimeoutThreshold { get; set; } = 3;
        public bool IsHeartbeatRunning => _isHeartbeatRunning;

        #endregion

        #region 自动重连配置

        /// <summary>
        /// 是否启用自动重连（默认：true）
        /// </summary>
        public bool AutoReconnectEnabled { get; set; } = true;

        /// <summary>
        /// 最大重连次数（默认：5次，-1表示无限重连）
        /// </summary>
        public int MaxReconnectAttempts { get; set; } = 5;

        /// <summary>
        /// 初始重连延迟（毫秒，默认：1000ms）
        /// </summary>
        public int InitialReconnectDelay { get; set; } = 1000;

        /// <summary>
        /// 最大重连延迟（毫秒，默认：30000ms）
        /// </summary>
        public int MaxReconnectDelay { get; set; } = 30000;

        /// <summary>
        /// 重连延迟倍增因子（默认：2.0，指数退避）
        /// </summary>
        public double ReconnectDelayMultiplier { get; set; } = 2.0;

        /// <summary>
        /// 是否正在重连
        /// </summary>
        public bool IsReconnecting => _isReconnecting;

        #endregion

        #region 事件

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

        #endregion

        #region 连接管理（抽象方法）

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

                    // 验证设备是否真的可用
                    var aliveResult = IsAlive();
                    if (aliveResult.Success && aliveResult.Data)
                    {
                        // 如果正在自动重连，停止重连任务
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
                        // 如果正在自动重连，停止重连任务
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
                // 先停止自动重连
                StopAutoReconnect();

                // 先停止心跳
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
                // 先停止自动重连
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

            Thread.Sleep(1000); // 等待设备复位

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

        #endregion

        #region 设备检测

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

        #endregion

        #region 心跳管理

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

                                    // 如果启用自动重连，则触发重连
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

        #endregion

        #region 自动重连管理

        /// <summary>
        /// 启动自动重连（指数退避策略）
        /// </summary>
        protected virtual void StartAutoReconnect()
        {
            lock (_reconnectLock)
            {
                if (_isReconnecting)
                    return; // 已经在重连中，避免重复启动

                _isReconnecting = true;
                _reconnectAttemptCount = 0;
                _reconnectCts = new CancellationTokenSource();

                _reconnectTask = Task.Run(async () =>
                {
                    while (!_reconnectCts.Token.IsCancellationRequested)
                    {
                        // 检查是否达到最大重连次数
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

                        // 计算延迟时间（指数退避）
                        int delay = CalculateReconnectDelay(_reconnectAttemptCount);
                        _reconnectAttemptCount++;

                        try
                        {
                            // 等待延迟时间
                            await Task.Delay(delay, _reconnectCts.Token);

                            // 尝试重连
                            Status = DeviceStatus.Connecting;
                            var connectResult = await ConnectAsync(_reconnectCts.Token);

                            if (connectResult.Success && IsOnline)
                            {
                                // 重连成功，重置失败计数
                                _heartbeatFailCount = 0;

                                // 如果心跳已停止，重新启动
                                if (!IsHeartbeatRunning)
                                {
                                    StartHeartbeat();
                                }

                                OnErrorOccurred(new DeviceErrorEventArgs
                                {
                                    ErrorMessage = $"自动重连成功，尝试次数: {_reconnectAttemptCount}",
                                    ErrorCode = 0 // 0表示成功
                                });

                                _isReconnecting = false;
                                break;
                            }
                            else
                            {
                                // 重连失败，继续下一次尝试
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

        /// <summary>
        /// 停止自动重连
        /// </summary>
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
                    // 忽略异常
                }
            }
        }

        /// <summary>
        /// 计算重连延迟时间（指数退避）
        /// </summary>
        /// <param name="attemptCount">当前尝试次数（从0开始）</param>
        /// <returns>延迟时间（毫秒）</returns>
        protected virtual int CalculateReconnectDelay(int attemptCount)
        {
            if (attemptCount <= 0)
                return InitialReconnectDelay;

            // 指数退避: delay = initial * (multiplier ^ attemptCount)
            double delay = InitialReconnectDelay * Math.Pow(ReconnectDelayMultiplier, attemptCount);

            // 限制在最大延迟范围内
            int delayMs = (int)Math.Min(delay, MaxReconnectDelay);

            // 确保至少为初始延迟
            return Math.Max(delayMs, InitialReconnectDelay);
        }

        #endregion

        #region 抽象方法（子类必须实现）

        /// <summary>
        /// 执行连接操作
        /// </summary>
        protected abstract OperationResult DoConnect();

        /// <summary>
        /// 执行异步连接操作
        /// </summary>
        protected abstract Task<OperationResult> DoConnectAsync(CancellationToken cancellationToken);

        /// <summary>
        /// 执行断开操作
        /// </summary>
        protected abstract OperationResult DoDisconnect();

        /// <summary>
        /// 执行异步断开操作
        /// </summary>
        protected abstract Task<OperationResult> DoDisconnectAsync(CancellationToken cancellationToken);

        /// <summary>
        /// 检查设备是否存在
        /// </summary>
        protected abstract bool DoCheckDeviceExists();

        /// <summary>
        /// 异步检查设备是否存在
        /// </summary>
        protected abstract Task<bool> DoCheckDeviceExistsAsync(CancellationToken cancellationToken);

        /// <summary>
        /// 检查设备是否活动
        /// </summary>
        protected abstract bool DoCheckAlive();

        /// <summary>
        /// 异步检查设备是否活动
        /// </summary>
        protected abstract Task<bool> DoCheckAliveAsync(CancellationToken cancellationToken);

        #endregion

        #region IDisposable 实现

        private bool _disposed;

        public virtual void Dispose()
        {
            if (_disposed)
                return;

            // 停止自动重连
            StopAutoReconnect();

            // 停止心跳
            if (IsHeartbeatRunning)
            {
                StopHeartbeat();
            }

            // 断开连接
            if (IsOnline || Status == DeviceStatus.Connected)
            {
                Disconnect();
            }

            // 释放资源
            _heartbeatCts?.Dispose();
            _reconnectCts?.Dispose();

            _disposed = true;
        }

        #endregion
    }
}