using Astra.Core.Devices;
using Astra.Core.Devices.Attributes;
using Astra.Core.Devices.Base;
using Astra.Core.Logs;
using Astra.Core.Logs.Extensions;
using Astra.Core.Plugins.Messaging;
using Microsoft.Extensions.Logging;
using Astra.Plugins.DataAcquisition.Abstractions;
using Astra.Plugins.DataAcquisition.Configs;
using Astra.Plugins.DataAcquisition.ViewModels;
using Astra.Plugins.DataAcquisition.Views;
using NVHDataBridge.Models;
using System.Buffers.Binary;
using System.Linq;
using System.Runtime.InteropServices;

namespace Astra.Plugins.DataAcquisition.Devices
{
   
    [DeviceDebugUI(typeof(DataAcquisitionDeviceDebugView), typeof(DataAcquisitionDeviceDebugViewModel))]
    public class DataAcquisitionDevice : DeviceBase<DataAcquisitionConfig>, IDataAcquisition
    {
        private readonly DataAcquisitionDeviceConnection _acquisitionConnection;
        private readonly IMessageBus _messageBus;
        private readonly Microsoft.Extensions.Logging.ILogger _logger;
        private readonly SemaphoreSlim _stateLock = new(1, 1);
        private readonly object _eventLock = new();
        private CancellationTokenSource _acquisitionCts;
        private Task _acquisitionTask;
        private ManualResetEventSlim _pauseEvent = new(true);
        private AcquisitionState _state = AcquisitionState.Idle;
        private readonly Random _random = new();
        private bool _initialized;

        // ✅ NVHDataBridge 数据结构
        private NvhMemoryFile? _dataFile;
        private const string GROUP_NAME = "Signal";  // 组名称固定为 "Signal"
        private readonly Dictionary<int, NvhMemoryChannelBase> _channelMap = new();
        private DateTime _acquisitionStartTime;

        public DataAcquisitionDevice(
            DataAcquisitionConfig config,
            IMessageBus messageBus = null,
            Microsoft.Extensions.Logging.ILogger logger = null)
            : base(new DataAcquisitionDeviceConnection(config, logger), config)
        {
            _acquisitionConnection = (DataAcquisitionDeviceConnection)_connection;
            _messageBus = messageBus;
            _logger = logger;
        }

        public AcquisitionState CurrentState => _state;

        public event EventHandler<DeviceMessage> DataReceived;

        public event EventHandler<Exception> ErrorOccurred;

        // ✅ 获取数据文件（供外部访问）
        public NvhMemoryFile? GetDataFile() => _dataFile;

        // ✅ 获取数据组（从 File 中获取）
        public NvhMemoryGroup? GetDataGroup()
        {
            if (_dataFile == null)
                return null;

            _dataFile.TryGetGroup(GROUP_NAME, out var group);
            return group;
        }

        // ✅ 获取指定通道的数据通道
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

        public AcquisitionState GetState()
        {
            return _state;
        }

        public async Task<bool> InitializeAsync()
        {
            await _stateLock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (_initialized)
                {
                    return true;
                }

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
                    RaiseInitializationError(existsResult.ErrorMessage ?? "采集卡不存在或未就绪");
                    return false;
                }

                // ✅ 初始化 NVHDataBridge 数据结构
                InitializeDataStructures();

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
                {
                    return;
                }

                _pauseEvent.Reset();
                _state = AcquisitionState.Paused;
                _logger?.LogInfo($"[{DeviceName}] 已暂停数据采集", LogCategory.Device);
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
                {
                    return;
                }

                _pauseEvent.Set();
                _state = AcquisitionState.Running;
                _logger?.LogInfo($"[{DeviceName}] 已恢复数据采集", LogCategory.Device);
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
                {
                    throw new InvalidOperationException("采集已在运行状态");
                }

                if (!_initialized && !await InitializeAsync().ConfigureAwait(false))
                {
                    throw new InvalidOperationException("采集卡初始化失败");
                }

                cancellationToken.ThrowIfCancellationRequested();

                var connectResult = await ConnectAsync(cancellationToken).ConfigureAwait(false);
                if (!connectResult.Success)
                {
                    _state = AcquisitionState.Error;
                    RaiseInitializationError(connectResult.ErrorMessage ?? "采集卡连接失败");
                    return;
                }

                // ✅ 记录采集开始时间，更新 TDMS 属性
                _acquisitionStartTime = DateTime.UtcNow;
                var dataGroup = GetDataGroup();
                if (dataGroup != null)
                {
                    dataGroup.Properties.Set("AcquisitionStartTime", _acquisitionStartTime);
                    
                    // 更新所有通道的开始时间
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
                _logger?.LogInfo($"[{DeviceName}] 数据采集已启动", LogCategory.Device);
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
                {
                    return;
                }

                _state = AcquisitionState.Idle;
                _pauseEvent.Set();

                if (_acquisitionCts != null)
                {
                    _acquisitionCts.Cancel();
                }

                if (_acquisitionTask != null)
                {
                    try
                    {
                        await _acquisitionTask.ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        // ignore
                    }
                }

                await DisconnectAsync().ConfigureAwait(false);
                
                // ✅ 采集停止后，压缩内存并记录统计信息
                var dataGroup = GetDataGroup();
                if (dataGroup != null)
                {
                    dataGroup.Properties.Set("AcquisitionEndTime", DateTime.UtcNow);
                    dataGroup.Properties.Set("AcquisitionDuration", (DateTime.UtcNow - _acquisitionStartTime).TotalSeconds);

                    // 压缩所有通道的内存
                    foreach (var channel in _channelMap.Values)
                    {
                        if (channel is NvhMemoryChannel<float> floatChannel)
                        {
                            floatChannel.TrimExcess(0.1); // 释放超过10%浪费的内存
                            
                            // 记录统计信息
                            var stats = floatChannel.GetMemoryStats();
                            _logger?.LogInfo($"[{DeviceName}] 通道 {channel.Name}: " +
                                $"样本数={stats.UsedSamples:N0}, " +
                                $"内存={stats.UsedBytes / 1024.0 / 1024.0:F2}MB, " +
                                $"浪费={stats.WasteRatio:P2}", LogCategory.Device);
                        }
                    }
                }

                _logger?.LogInfo($"[{DeviceName}] 数据采集已停止", LogCategory.Device);
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
                var frameInterval = CalculateFrameInterval();

                while (!token.IsCancellationRequested)
                {
                    _pauseEvent.Wait(token);

                    var payload = GenerateSampleFrame();
                    PublishData(payload);
                    
                    // ✅ 将数据写入 NVHDataBridge 通道
                    WriteDataToChannels(payload);

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
                _logger?.LogError($"[{DeviceName}] 采集循环异常: {ex.Message}", ex, LogCategory.Device);
                ErrorOccurred?.Invoke(this, ex);
            }
        }

        private TimeSpan CalculateFrameInterval()
        {
            if (_config.SampleRate <= 0 || _config.BufferSize <= 0)
            {
                return TimeSpan.FromMilliseconds(100);
            }

            var seconds = (double)_config.BufferSize / _config.SampleRate;
            seconds = Math.Clamp(seconds, 0.01, 1.0); // 将间隔控制在 10ms - 1s
            return TimeSpan.FromSeconds(seconds);
        }

        private byte[] GenerateSampleFrame()
        {
            // 使用简单的正弦叠加和噪声模拟实际采集数据
            var channelCount = Math.Max(1, _config.ChannelCount);
            var bufferSize = Math.Max(1, _config.BufferSize);
            var sampleCount = channelCount * bufferSize;
            var data = new byte[sampleCount * sizeof(float)];

            var timestamp = DateTime.UtcNow;
            for (var channel = 0; channel < channelCount; channel++)
            {
                for (var i = 0; i < bufferSize; i++)
                {
                    var index = channel * bufferSize + i;
                    var t = (timestamp.Ticks / TimeSpan.TicksPerMillisecond + i) / 1000.0;

                    var sine = MathF.Sin((float)(2 * Math.PI * (channel + 1) * t)) * 0.5f;
                    var noise = (float)(_random.NextDouble() - 0.5) * 0.05f;
                    var value = sine + noise;

                    var byteIndex = index * sizeof(float);
                    BinaryPrimitives.WriteSingleLittleEndian(data.AsSpan(byteIndex, sizeof(float)), value);
                }
            }

            return data;
        }

        private void PublishData(byte[] payload)
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

            lock (_eventLock)
            {
                DataReceived?.Invoke(this, message);
            }

            if (_messageBus != null)
            {
                _ = _messageBus.PublishAsync($"devices/{DeviceId}/data", message);
            }
        }

        private void RaiseInitializationError(string message)
        {
            var exception = new InvalidOperationException(message);
            ErrorOccurred?.Invoke(this, exception);
            _logger?.LogError($"[{DeviceName}] 初始化失败: {message}", exception, LogCategory.Device);
        }

        // ✅ 初始化数据结构
        private void InitializeDataStructures()
        {
            // 创建数据文件
            _dataFile = new NvhMemoryFile(estimatedGroupCount: 1);
            
            // 从 File 中获取或创建组（组名称固定为 "Signal"）
            var dataGroup = _dataFile.GetOrCreateGroup(GROUP_NAME);

            // 设置组属性
            dataGroup.Properties.Set("DeviceId", DeviceId);
            dataGroup.Properties.Set("DeviceName", DeviceName);
            dataGroup.Properties.Set("SerialNumber", _config.SerialNumber);
            dataGroup.Properties.Set("SampleRate", _config.SampleRate);
            dataGroup.Properties.Set("ChannelCount", _config.ChannelCount);

            // 为每个启用的通道创建数据通道
            _channelMap.Clear();
            foreach (var channelConfig in _config.Channels ?? Enumerable.Empty<DAQChannelConfig>())
            {
                if (!channelConfig.Enabled)
                    continue;

                // 计算预估样本数（假设采集 1 小时，可根据实际需求调整）
                long estimatedSamples = (long)(channelConfig.SampleRate * 20); // 20秒
                
                // 计算 RingBuffer 大小（保存最近 5 秒的数据，用于实时显示）
                int ringBufferSize = CalculateRingBufferSize(channelConfig.SampleRate);
                
                // 创建 float 类型的通道
                var dataChannel = dataGroup.CreateChannel<float>(
                    name: channelConfig.ChannelName ?? $"Channel{channelConfig.ChannelId}",
                    ringBufferSize: ringBufferSize,
                    initialCapacity: (int)Math.Min(estimatedSamples / 10, 1_000_000), // 初始容量为预估的10%
                    estimatedTotalSamples: estimatedSamples
                );

                // 设置通道的 TDMS 属性
                dataChannel.WfStartTime = DateTime.UtcNow; // 将在开始采集时更新
                dataChannel.WfIncrement = 1.0 / channelConfig.SampleRate; // 采样间隔（秒）
                dataChannel.WfStartOffset = 0.0;

                // 设置通道的其他属性
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

                // 保存映射关系
                _channelMap[channelConfig.ChannelId] = dataChannel;
            }

            _logger?.LogInfo($"[{DeviceName}] 已初始化 {_channelMap.Count} 个数据通道", LogCategory.Device);
        }

        // ✅ 计算 RingBuffer 大小（保存最近 5 秒数据）
        private int CalculateRingBufferSize(double sampleRate)
        {
            if (sampleRate <= 0)
                return 262144; // 默认值

            // 计算 5 秒的数据量，向上取整到 2 的幂
            long samples5Seconds = (long)(sampleRate * 5);
            
            // 找到大于等于 samples5Seconds 的最小 2 的幂
            int size = 1;
            while (size < samples5Seconds && size < int.MaxValue / 2)
            {
                size <<= 1;
            }
            
            // 限制最大值为 2^20 (1M) 以避免内存过大
            return Math.Min(size, 1 << 20);
        }

        // ✅ 将采集的数据写入通道
        private void WriteDataToChannels(byte[] payload)
        {
            if (payload == null || payload.Length == 0 || _channelMap.Count == 0)
                return;

            try
            {
                var channelCount = Math.Max(1, _config.ChannelCount);
                var bufferSize = Math.Max(1, _config.BufferSize);

                // 将字节数组转换为 float 数组
                int floatCount = payload.Length / sizeof(float);
                if (floatCount != channelCount * bufferSize)
                {
                    _logger?.LogWarn($"[{DeviceName}] 数据大小不匹配: 期望 {channelCount * bufferSize}, 实际 {floatCount}", LogCategory.Device);
                    return;
                }

                // 解析数据：按通道分离数据
                // 数据格式：Channel0[0], Channel1[0], ..., ChannelN[0], Channel0[1], Channel1[1], ...
                Span<float> floatData = MemoryMarshal.Cast<byte, float>(payload);

                // 为每个通道提取数据并写入
                for (int channelIdx = 0; channelIdx < channelCount; channelIdx++)
                {
                    // 找到对应的通道配置
                    var channelConfig = _config.Channels?.FirstOrDefault(c => c.ChannelId == channelIdx);
                    if (channelConfig == null || !channelConfig.Enabled)
                        continue;

                    // 获取数据通道
                    if (!_channelMap.TryGetValue(channelIdx, out var dataChannel))
                        continue;

                    // 提取该通道的所有样本（交错数据）
                    float[] channelSamples = new float[bufferSize];
                    for (int i = 0; i < bufferSize; i++)
                    {
                        int sourceIndex = i * channelCount + channelIdx;
                        if (sourceIndex < floatData.Length)
                        {
                            channelSamples[i] = floatData[sourceIndex];
                        }
                    }

                    // 批量写入数据（高性能）
                    dataChannel.WriteData<float>(channelSamples);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError($"[{DeviceName}] 写入数据到通道失败: {ex.Message}", ex, LogCategory.Device);
            }
        }

        public override void Dispose()
        {
            _pauseEvent?.Set();
            _pauseEvent?.Dispose();
            _stateLock?.Dispose();
            _acquisitionCts?.Cancel();
            _acquisitionCts?.Dispose();
            
            // ✅ 清理数据结构
            _channelMap?.Clear();
            _dataFile = null;
            
            base.Dispose();
        }
    }
}
