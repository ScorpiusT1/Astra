using Astra.Core.Devices;
using Astra.Core.Devices.Attributes;
using Astra.Core.Devices.Base;
using Astra.Core.Logs;
using Astra.Core.Plugins.Messaging;
using Astra.Plugins.DataAcquisition.Abstractions;
using Astra.Plugins.DataAcquisition.SDKs;
using Astra.Plugins.DataAcquisition.ViewModels;
using Astra.Plugins.DataAcquisition.Views;
using NVHDataBridge.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Astra.Plugins.DataAcquisition.Devices
{
    /// <summary>
    /// BRC数据采集设备
    /// </summary>
    [DeviceDebugUI(typeof(DataAcquisitionDeviceDebugView), typeof(DataAcquisitionDeviceDebugViewModel))]
    public class BRCDataAcquisitionDevice : DeviceBase<DataAcquisitionConfig>, IDataAcquisition
    {
        private readonly BRCDataAcquisitionDeviceConnection _brcConnection;
        private readonly IMessageBus _messageBus;
        private readonly ILogger _logger;
        private readonly SemaphoreSlim _stateLock = new(1, 1);
        private readonly object _eventLock = new();
        private CancellationTokenSource _acquisitionCts;
        private Task _acquisitionTask;
        private ManualResetEventSlim _pauseEvent = new(true);
        private AcquisitionState _state = AcquisitionState.Idle;
        private bool _initialized;

        // NVHDataBridge 数据结构
        private NvhMemoryFile? _dataFile;
        private const string GROUP_NAME = "Signal";
        private readonly Dictionary<int, NvhMemoryChannelBase> _channelMap = new();
        private DateTime _acquisitionStartTime;

        // 数据缓冲区
        private double[] _dataBuffer;

        public BRCDataAcquisitionDevice(
            DataAcquisitionConfig config,
            IMessageBus messageBus = null,
            ILogger logger = null)
            : base(new BRCDataAcquisitionDeviceConnection(config, logger), config)
        {
            _brcConnection = (BRCDataAcquisitionDeviceConnection)_connection;
            _messageBus = messageBus;
            _logger = logger;
        }

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

        public async Task DisposeAsync()
        {
            await StopAcquisitionAsync().ConfigureAwait(false);
            Dispose();
        }

        public AcquisitionState GetState() => _state;

        public async Task<bool> InitializeAsync()
        {
            await _stateLock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (_initialized)
                    return true;

                var validation = ValidateConfig(_config);
                if (!validation.Success)
                {
                    _state = AcquisitionState.Error;
                    RaiseInitializationError(validation.ErrorMessage);
                    return false;
                }

                var existsResult = await DeviceExistsAsync().ConfigureAwait(false);
                if (!existsResult.Success || !existsResult.Data)
                {
                    _state = AcquisitionState.Error;
                    RaiseInitializationError(existsResult.ErrorMessage ?? "BRC采集卡不存在或未就绪");
                    return false;
                }

                // 初始化数据结构
                InitializeDataStructures();

                // 初始化数据缓冲区
                var enabledChannels = _config.Channels?.Count(c => c.Enabled) ?? _config.ChannelCount;
                var bufferSize = _config.BufferSize;
                _dataBuffer = new double[enabledChannels * bufferSize];

                _initialized = true;
                _state = AcquisitionState.Idle;
                return true;
            }
            finally
            {
                _stateLock.Release();
            }
        }

        public async Task PauseAsync()
        {
            await _stateLock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (_state != AcquisitionState.Running)
                    return;

                _pauseEvent.Reset();
                _state = AcquisitionState.Paused;
                _logger?.Info($"[{DeviceName}] 已暂停数据采集", LogCategory.Device);
            }
            finally
            {
                _stateLock.Release();
            }
        }

        public async Task ResumeAsync()
        {
            await _stateLock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (_state != AcquisitionState.Paused)
                    return;

                _pauseEvent.Set();
                _state = AcquisitionState.Running;
                _logger?.Info($"[{DeviceName}] 已恢复数据采集", LogCategory.Device);
            }
            finally
            {
                _stateLock.Release();
            }
        }

        public async Task StartAcquisitionAsync(CancellationToken cancellationToken = default)
        {
            await _stateLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (_state == AcquisitionState.Running)
                    throw new InvalidOperationException("采集已在运行状态");

                if (!_initialized && !await InitializeAsync().ConfigureAwait(false))
                    throw new InvalidOperationException("BRC采集卡初始化失败");

                cancellationToken.ThrowIfCancellationRequested();

                var connectResult = await ConnectAsync(cancellationToken).ConfigureAwait(false);
                if (!connectResult.Success)
                {
                    _state = AcquisitionState.Error;
                    RaiseInitializationError(connectResult.ErrorMessage ?? "BRC采集卡连接失败");
                    return;
                }

                // 获取BRC设备实例
                var brcDevice = _brcConnection.GetBrcDevice();
                if (brcDevice == null)
                {
                    _state = AcquisitionState.Error;
                    RaiseInitializationError("BRC设备实例无效");
                    return;
                }

                // 启动采集
                brcDevice.Start();

                // 记录采集开始时间
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

                _acquisitionTask = Task.Run(() => RunAcquisitionLoopAsync(linkedCts.Token), CancellationToken.None);
                _state = AcquisitionState.Running;
                _logger?.Info($"[{DeviceName}] BRC数据采集已启动", LogCategory.Device);
            }
            finally
            {
                _stateLock.Release();
            }
        }

        public async Task StopAcquisitionAsync()
        {
            await _stateLock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (_state == AcquisitionState.Idle)
                    return;

                _state = AcquisitionState.Idle;
                _pauseEvent.Set();

                if (_acquisitionCts != null)
                {
                    _acquisitionCts.Cancel();
                }

                // 停止BRC设备采集
                try
                {
                    var brcDevice = _brcConnection.GetBrcDevice();
                    brcDevice?.Stop();
                }
                catch (Exception ex)
                {
                    _logger?.Warn($"[{DeviceName}] 停止BRC采集失败: {ex.Message}", LogCategory.Device);
                }

                if (_acquisitionTask != null)
                {
                    try
                    {
                        await _acquisitionTask.ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        // 正常取消
                    }
                }

                await DisconnectAsync().ConfigureAwait(false);

                // 采集停止后处理
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
                            _logger?.Info($"[{DeviceName}] 通道 {channel.Name}: " +
                                $"样本数={stats.UsedSamples:N0}, " +
                                $"内存={stats.UsedBytes / 1024.0 / 1024.0:F2}MB", LogCategory.Device);
                        }
                    }
                }

                _logger?.Info($"[{DeviceName}] BRC数据采集已停止", LogCategory.Device);
            }
            finally
            {
                _stateLock.Release();
            }
        }

        private async Task RunAcquisitionLoopAsync(CancellationToken token)
        {
            var brcDevice = _brcConnection.GetBrcDevice();
            if (brcDevice == null)
            {
                _state = AcquisitionState.Error;
                RaiseInitializationError("BRC设备实例无效");
                return;
            }

            var moduleInfo = _brcConnection.GetModuleInfo();
            if (moduleInfo == null)
            {
                _state = AcquisitionState.Error;
                RaiseInitializationError("模块信息无效");
                return;
            }

            var enabledChannels = _config.Channels?.Where(c => c.Enabled).ToList() 
                ?? Enumerable.Range(1, _config.ChannelCount).Select(i => new Configs.DAQChannelConfig { ChannelId = i, Enabled = true }).ToList();
            var bufferSize = _config.BufferSize;
            var timeout = TimeSpan.FromMilliseconds(1000); // 1秒超时

            try
            {
                while (!token.IsCancellationRequested)
                {
                    _pauseEvent.Wait(token);

                    // 从BRC设备获取数据（GetChannelsData内部已处理unsafe）
                    var bufferMemory = new Memory<double>(_dataBuffer);
                    brcDevice.GetChannelsData(bufferMemory, timeout);

                    // 转换为字节数组并发布
                    var payload = ConvertToByteArray(bufferMemory.Span);
                    PublishData(payload);

                    // 写入NVHDataBridge通道
                    WriteDataToChannels(bufferMemory.Span, enabledChannels, bufferSize);

                    // 根据采样率和缓冲区大小计算延迟
                    var frameInterval = CalculateFrameInterval();
                    if (frameInterval > TimeSpan.Zero)
                    {
                        await Task.Delay(frameInterval, token).ConfigureAwait(false);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // 正常退出
            }
            catch (Exception ex)
            {
                _state = AcquisitionState.Error;
                _logger?.Error($"[{DeviceName}] 采集循环异常: {ex.Message}", ex, LogCategory.Device);
                ErrorOccurred?.Invoke(this, ex);
            }
        }

        private byte[] ConvertToByteArray(Span<double> data)
        {
            var floatData = new float[data.Length];
            for (int i = 0; i < data.Length; i++)
            {
                floatData[i] = (float)data[i];
            }

            var bytes = new byte[floatData.Length * sizeof(float)];
            var floatSpan = MemoryMarshal.Cast<byte, float>(bytes);
            floatData.CopyTo(floatSpan);
            return bytes;
        }

        private TimeSpan CalculateFrameInterval()
        {
            if (_config.SampleRate <= 0 || _config.BufferSize <= 0)
                return TimeSpan.FromMilliseconds(100);

            var seconds = (double)_config.BufferSize / _config.SampleRate;
            seconds = Math.Clamp(seconds, 0.01, 1.0);
            return TimeSpan.FromSeconds(seconds);
        }

        private void PublishData(byte[] payload)
        {
            if (payload == null || payload.Length == 0)
                return;

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

            lock (_eventLock)
            {
                DataReceived?.Invoke(this, message);
            }

            if (_messageBus != null)
            {
                _ = _messageBus.PublishAsync($"devices/{DeviceId}/data", message);
            }
        }

        private void WriteDataToChannels(Span<double> data, List<Configs.DAQChannelConfig> enabledChannels, int bufferSize)
        {
            if (data.Length == 0 || _channelMap.Count == 0)
                return;

            try
            {
                var channelCount = enabledChannels.Count;

                // 数据格式：Channel0[0], Channel1[0], ..., ChannelN[0], Channel0[1], Channel1[1], ...
                for (int channelIdx = 0; channelIdx < channelCount; channelIdx++)
                {
                    var channelConfig = enabledChannels[channelIdx];
                    var channelId = channelConfig.ChannelId;

                    if (!_channelMap.TryGetValue(channelId, out var dataChannel))
                        continue;

                    // 提取该通道的所有样本
                    float[] channelSamples = new float[bufferSize];
                    for (int i = 0; i < bufferSize; i++)
                    {
                        int sourceIndex = i * channelCount + channelIdx;
                        if (sourceIndex < data.Length)
                        {
                            channelSamples[i] = (float)data[sourceIndex];
                        }
                    }

                    // 批量写入数据
                    dataChannel.WriteData<float>(channelSamples);
                }
            }
            catch (Exception ex)
            {
                _logger?.Error($"[{DeviceName}] 写入数据到通道失败: {ex.Message}", ex, LogCategory.Device);
            }
        }

        private void InitializeDataStructures()
        {
            _dataFile = new NvhMemoryFile(estimatedGroupCount: 1);
            var dataGroup = _dataFile.GetOrCreateGroup(GROUP_NAME);

            dataGroup.Properties.Set("DeviceId", DeviceId);
            dataGroup.Properties.Set("DeviceName", DeviceName);
            dataGroup.Properties.Set("SerialNumber", _config.SerialNumber);
            dataGroup.Properties.Set("SampleRate", _config.SampleRate);
            dataGroup.Properties.Set("ChannelCount", _config.ChannelCount);

            _channelMap.Clear();
            foreach (var channelConfig in _config.Channels ?? Enumerable.Empty<Configs.DAQChannelConfig>())
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

            _logger?.Info($"[{DeviceName}] 已初始化 {_channelMap.Count} 个数据通道", LogCategory.Device);
        }

        private int CalculateRingBufferSize(double sampleRate)
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

        private void RaiseInitializationError(string message)
        {
            var exception = new InvalidOperationException(message);
            ErrorOccurred?.Invoke(this, exception);
            _logger?.Error($"[{DeviceName}] 初始化失败: {message}", exception, LogCategory.Device);
        }

        public override void Dispose()
        {
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

