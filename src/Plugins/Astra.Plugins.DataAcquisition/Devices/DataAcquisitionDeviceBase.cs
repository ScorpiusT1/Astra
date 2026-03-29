using Astra.Contract.Communication.Abstractions;
using Astra.Core.Devices;
using Astra.Core.Devices.Base;
using Astra.Core.Foundation.Common;
using Astra.Core.Logs;
using Astra.Core.Logs.Extensions;
using Astra.Core.Plugins.Messaging;
using Astra.Plugins.DataAcquisition.Configs;
using Microsoft.Extensions.Logging;
using NVHDataBridge.Models;
using System.Threading.Channels;

namespace Astra.Plugins.DataAcquisition.Devices
{
    /// <summary>
    /// 通用数据采集设备抽象基类，封装状态机、NVH 数据结构和消息发布逻辑。
    /// 各具体采集卡只需要实现硬件相关的启动/停止和单帧采集逻辑。
    /// </summary>
    public abstract class DataAcquisitionDeviceBase : DeviceBase<DataAcquisitionConfig>, IDataAcquisition
    {
        protected readonly IMessageBus _messageBus;
        protected readonly ILogger _logger;

        protected readonly SemaphoreSlim _stateLock = new(1, 1);
        protected readonly object _eventLock = new();
        protected CancellationTokenSource _acquisitionCts;
        protected Task _acquisitionTask;
        protected ManualResetEventSlim _pauseEvent = new(true);
        protected AcquisitionState _state = AcquisitionState.Idle;
        protected bool _initialized;
        private Channel<DeviceMessage> _publishQueue;
        private Task _publishTask;
        private CancellationTokenSource _publishCts;
        private const int PublishQueueCapacity = 256;

        // NVHDataBridge 数据结构
        protected NvhMemoryFile? _dataFile;
        protected const string GROUP_NAME = "Signal";
        protected readonly Dictionary<int, NvhMemoryChannelBase> _channelMap = new();
        protected DateTime _acquisitionStartTime;

        protected DataAcquisitionDeviceBase(
            DeviceConnectionBase connection,
            DataAcquisitionConfig config,
            IMessageBus messageBus = null,
            ILogger logger = null)
            : base(connection, config)
        {
            _messageBus = messageBus;
            _logger = logger;
        }

        protected ILogger Logger => _logger;

        public AcquisitionState CurrentState => _state;

        public event EventHandler<DeviceMessage> DataReceived;

        public event EventHandler<Exception> ErrorOccurred;

        public NvhMemoryFile? GetDataFile() => _dataFile;

        public NvhMemoryGroup? GetDataGroup()
        {
            if (_dataFile == null)
                return null;

            _dataFile.TryGetGroup(GROUP_NAME, out var group);
            return group;
        }

        public NvhMemoryChannelBase? GetDataChannel(int channelId)
        {
            _channelMap.TryGetValue(channelId, out var channel);
            return channel;
        }

        /// <summary>
        /// 当前配置中已启用通道的名称（与 NVH Signal 组内通道名一致，参见 <see cref="InitializeDataStructures"/>）。
        /// </summary>
        public IReadOnlyList<string> GetConfiguredEnabledChannelNames()
        {
            var list = new List<string>();
            if (_config?.Channels == null)
            {
                return list;
            }

            foreach (var channelConfig in _config.Channels)
            {
                if (channelConfig == null || !channelConfig.Enabled)
                {
                    continue;
                }

                var name = string.IsNullOrWhiteSpace(channelConfig.ChannelName)
                    ? $"Channel{channelConfig.ChannelId}"
                    : channelConfig.ChannelName.Trim();
                list.Add(name);
            }

            return list;
        }

        public async Task<OperationResult> DisposeAsync()
        {
            await StopAcquisitionAsync().ConfigureAwait(false);
            Dispose();
            return OperationResult.Succeed();
        }

        public AcquisitionState GetState() => _state;

        public async Task<OperationResult> InitializeAsync()
        {
            await _stateLock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (_initialized)
                    return OperationResult.Succeed("采集卡已初始化");

                var validation = ValidateConfig(_config);
                if (!validation.Success)
                {
                    _state = AcquisitionState.Error;
                    RaiseInitializationError(validation.ErrorMessage);
                    return OperationResult.Failure(validation.ErrorMessage ?? "采集卡配置校验失败");
                }

                var existsResult = await DeviceExistsAsync().ConfigureAwait(false);
                if (!existsResult.Success || !existsResult.Data)
                {
                    _state = AcquisitionState.Error;
                    var message = existsResult.ErrorMessage ?? "采集卡不存在或未就绪";
                    RaiseInitializationError(message);
                    return OperationResult.Failure(message);
                }

                InitializeDataStructures();

                await OnInitializeAsync().ConfigureAwait(false);

                _initialized = true;
                _state = AcquisitionState.Idle;
                return OperationResult.Succeed("采集卡初始化成功");
            }
            finally
            {
                _stateLock.Release();
            }
        }

        public async Task<OperationResult> PauseAsync()
        {
            await _stateLock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (_state != AcquisitionState.Running)
                    return OperationResult.Succeed("采集卡当前不在运行状态，无需暂停");

                _pauseEvent.Reset();
                _state = AcquisitionState.Paused;
                _logger?.LogInfo($"[{DeviceName}] 已暂停数据采集", LogCategory.Device);
                return OperationResult.Succeed("采集卡已暂停");
            }
            finally
            {
                _stateLock.Release();
            }
        }

        public async Task<OperationResult> ResumeAsync()
        {
            await _stateLock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (_state != AcquisitionState.Paused)
                    return OperationResult.Succeed("采集卡当前不在暂停状态，无需恢复");

                _pauseEvent.Set();
                _state = AcquisitionState.Running;
                _logger?.LogInfo($"[{DeviceName}] 已恢复数据采集", LogCategory.Device);
                return OperationResult.Succeed("采集卡已恢复");
            }
            finally
            {
                _stateLock.Release();
            }
        }

        public async Task<OperationResult> StartAcquisitionAsync(CancellationToken cancellationToken = default)
        {
            // 先在锁外完成初始化，避免在持有 _stateLock 时再次调用 InitializeAsync 导致自死锁
            if (!_initialized)
            {
                var initResult = await InitializeAsync().ConfigureAwait(false);
                if (!initResult.Success)
                    return OperationResult.Failure(initResult.ErrorMessage ?? "采集卡初始化失败");
            }

            await _stateLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (_state == AcquisitionState.Running)
                    return OperationResult.Succeed("采集已在运行状态");

                cancellationToken.ThrowIfCancellationRequested();

                var connectResult = await ConnectAsync(cancellationToken).ConfigureAwait(false);
                if (!connectResult.Success)
                {
                    _state = AcquisitionState.Error;
                    var message = connectResult.ErrorMessage ?? "采集卡连接失败";
                    RaiseInitializationError(message);
                    return OperationResult.Failure(message);
                }

                await OnStartHardwareAsync(cancellationToken).ConfigureAwait(false);

                _acquisitionStartTime = DateTime.UtcNow;
                var dataGroup = GetDataGroup();
                if (dataGroup != null)
                {
                    dataGroup.Properties.Set("AcquisitionStartTime", _acquisitionStartTime);

                    foreach (var channel in _channelMap.Values)
                    {
                        channel.WfStartTime = _acquisitionStartTime;
                    }
                }

                var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                _acquisitionCts = linkedCts;
                _pauseEvent.Set();
                StartPublishLoop();

                _acquisitionTask = Task.Run(() => RunAcquisitionLoopAsync(linkedCts.Token), CancellationToken.None);
                _state = AcquisitionState.Running;
                _logger?.LogInfo($"[{DeviceName}] 数据采集已启动", LogCategory.Device);
                return OperationResult.Succeed("采集卡启动成功");
            }
            finally
            {
                _stateLock.Release();
            }
        }

        public async Task<OperationResult> StopAcquisitionAsync()
        {
            await _stateLock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (_state == AcquisitionState.Idle)
                    return OperationResult.Succeed("采集卡当前为空闲状态，无需停止");

                _state = AcquisitionState.Idle;
                _pauseEvent.Set();

                if (_acquisitionCts != null)
                {
                    _acquisitionCts.Cancel();
                }

                try
                {
                    await OnStopHardwareAsync().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger?.LogWarn($"[{DeviceName}] 停止硬件采集失败: {ex.Message}", LogCategory.Device);
                }

                if (_acquisitionTask != null)
                {
                    try
                    {
                        await _acquisitionTask.ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                    }
                }

                await StopPublishLoopAsync().ConfigureAwait(false);

                if (!_config.KeepConnectionAlive)
                {
                    await DisconnectAsync().ConfigureAwait(false);
                }

                var dataGroup = GetDataGroup();
                if (dataGroup != null)
                {
                    dataGroup.Properties.Set("AcquisitionEndTime", DateTime.UtcNow);
                    dataGroup.Properties.Set("AcquisitionDuration", (DateTime.UtcNow - _acquisitionStartTime).TotalSeconds);

                    foreach (var channel in _channelMap.Values)
                    {
                        if (channel is NvhMemoryChannel<float> floatChannel)
                        {
                            floatChannel.TrimExcess(0.1);
                            var stats = floatChannel.GetMemoryStats();
                            _logger?.LogInfo($"[{DeviceName}] 通道 {channel.Name}: " +
                                $"样本数={stats.UsedSamples:N0}, " +
                                $"内存={stats.UsedBytes / 1024.0 / 1024.0:F2}MB, " +
                                $"浪费={stats.WasteRatio:P2}", LogCategory.Device);
                        }
                    }
                }

                _logger?.LogInfo($"[{DeviceName}] 数据采集已停止", LogCategory.Device);
                return OperationResult.Succeed("采集卡停止成功");
            }
            finally
            {
                _stateLock.Release();
            }
        }

        private async Task RunAcquisitionLoopAsync(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    _pauseEvent.Wait(token);

                    await AcquireAndProcessFrameAsync(token).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                _state = AcquisitionState.Error;
                _logger?.LogError($"[{DeviceName}] 采集循环异常: {ex.Message}", ex, LogCategory.Device);
                ErrorOccurred?.Invoke(this, ex);
            }
        }

        protected void PublishData(byte[] payload)
        {
            if (payload == null || payload.Length == 0)
            {
                return;
            }

            var message = new DeviceMessage(payload, DeviceId)
            {
                Timestamp = DateTime.UtcNow,
                Properties = new Dictionary<string, object>
                {
                    ["DeviceId"] = DeviceId,
                    ["DeviceName"] = DeviceName,
                    ["ChannelCount"] = _config.ChannelCount,
                    ["SampleRate"] = _config.SampleRate,
                    ["BufferSize"] = _config.BufferSize
                }
            };

            if (_publishQueue == null)
            {
                DispatchData(message);
                return;
            }

            if (!_publishQueue.Writer.TryWrite(message))
            {
                _logger?.LogWarn($"[{DeviceName}] 数据发布队列已满，丢弃一帧数据", LogCategory.Device);
            }
        }

        private void StartPublishLoop()
        {
            _publishCts?.Cancel();
            _publishCts?.Dispose();
            _publishCts = new CancellationTokenSource();

            _publishQueue = Channel.CreateBounded<DeviceMessage>(new BoundedChannelOptions(PublishQueueCapacity)
            {
                SingleReader = true,
                SingleWriter = true,
                FullMode = BoundedChannelFullMode.DropOldest
            });

            _publishTask = Task.Run(() => RunPublishLoopAsync(_publishCts.Token), CancellationToken.None);
        }

        private async Task StopPublishLoopAsync()
        {
            if (_publishQueue != null)
            {
                _publishQueue.Writer.TryComplete();
            }

            if (_publishCts != null)
            {
                _publishCts.Cancel();
            }

            if (_publishTask != null)
            {
                try
                {
                    await _publishTask.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                }
            }

            _publishTask = null;
            _publishQueue = null;
            _publishCts?.Dispose();
            _publishCts = null;
        }

        private async Task RunPublishLoopAsync(CancellationToken token)
        {
            if (_publishQueue == null)
                return;

            try
            {
                while (await _publishQueue.Reader.WaitToReadAsync(token).ConfigureAwait(false))
                {
                    while (_publishQueue.Reader.TryRead(out var message))
                    {
                        DispatchData(message);
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                _logger?.LogError($"[{DeviceName}] 数据分发循环异常: {ex.Message}", ex, LogCategory.Device);
            }
        }

        private void DispatchData(DeviceMessage message)
        {
            lock (_eventLock)
            {
                DataReceived?.Invoke(this, message);
            }

            if (_messageBus != null)
            {
                _ = _messageBus.PublishAsync($"devices/{DeviceId}/data", message);
            }
        }

        protected void RaiseInitializationError(string message)
        {
            var exception = new InvalidOperationException(message);
            ErrorOccurred?.Invoke(this, exception);
            _logger?.LogError($"[{DeviceName}] 初始化失败: {message}", exception, LogCategory.Device);
        }

        protected void InitializeDataStructures()
        {
            _dataFile = new NvhMemoryFile(estimatedGroupCount: 1);

            var dataGroup = _dataFile.GetOrCreateGroup(GROUP_NAME);

            dataGroup.Properties.Set("DeviceId", DeviceId);
            dataGroup.Properties.Set("DeviceName", DeviceName);
            dataGroup.Properties.Set("SerialNumber", _config.SerialNumber);
            dataGroup.Properties.Set("SampleRate", _config.SampleRate);
            dataGroup.Properties.Set("ChannelCount", _config.ChannelCount);

            _channelMap.Clear();
            foreach (var channelConfig in _config.Channels ?? Enumerable.Empty<DAQChannelConfig>())
            {
                if (!channelConfig.Enabled)
                    continue;

                long estimatedSamples = (long)(channelConfig.SampleRate * 20);
                int ringBufferSize = CalculateRingBufferSize(channelConfig.SampleRate);

                var dataChannel = dataGroup.CreateChannel<float>(
                    name: channelConfig.ChannelName ?? $"Channel{channelConfig.ChannelId}",
                    ringBufferSize: ringBufferSize,
                    initialCapacity: (int)Math.Min(estimatedSamples / 10, 1_000_000),
                    estimatedTotalSamples: estimatedSamples
                );

                dataChannel.WfStartTime = DateTime.UtcNow;
                dataChannel.WfIncrement = 1.0 / channelConfig.SampleRate;
                dataChannel.WfStartOffset = 0.0;

                dataChannel.Properties.Set("ChannelId", channelConfig.ChannelId);
                dataChannel.Properties.Set("SampleRate", channelConfig.SampleRate);
                dataChannel.Properties.Set("Gain", channelConfig.Gain);
                dataChannel.Properties.Set("Offset", channelConfig.Offset);
                dataChannel.Properties.Set("CouplingMode", channelConfig.CouplingMode.ToString());

                if (channelConfig.Sensor != null)
                {
                    dataChannel.Properties.Set("SensorName", channelConfig.Sensor.ConfigName);
                    dataChannel.Properties.Set("SensorType", channelConfig.Sensor.SensorType.ToString());
                }

                _channelMap[channelConfig.ChannelId] = dataChannel;
            }

            _logger?.LogInfo($"[{DeviceName}] 已初始化 {_channelMap.Count} 个数据通道", LogCategory.Device);
        }

        protected int CalculateRingBufferSize(double sampleRate)
        {
            if (sampleRate <= 0)
                return 262144;

            long samples5Seconds = (long)(sampleRate * 5);
            int size = 1;
            while (size < samples5Seconds && size < int.MaxValue / 2)
            {
                size <<= 1;
            }

            return Math.Min(size, 1 << 20);
        }

        /// <summary>
        /// 子类在此实现一次采集 + 写入 NVH + 发布消息的完整流程。
        /// 基类循环中会不断调用该方法。
        /// </summary>
        protected abstract Task AcquireAndProcessFrameAsync(CancellationToken token);

        /// <summary>
        /// 子类可以在此执行额外的初始化逻辑（例如缓冲区分配）。
        /// </summary>
        protected virtual Task OnInitializeAsync() => Task.CompletedTask;

        /// <summary>
        /// 子类在采集开始前启动硬件。
        /// </summary>
        protected virtual Task OnStartHardwareAsync(CancellationToken token) => Task.CompletedTask;

        /// <summary>
        /// 子类在采集停止时停止硬件。
        /// </summary>
        protected virtual Task OnStopHardwareAsync() => Task.CompletedTask;

        public override void Dispose()
        {
            try
            {
                StopPublishLoopAsync().GetAwaiter().GetResult();
            }
            catch
            {
            }

            _pauseEvent?.Set();
            _pauseEvent?.Dispose();
            _stateLock?.Dispose();
            _acquisitionCts?.Cancel();
            _acquisitionCts?.Dispose();

            _channelMap?.Clear();
            _dataFile = null;

            base.Dispose();
        }
    }
}

