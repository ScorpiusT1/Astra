using Astra.Core.Devices;
using Astra.Core.Devices.Attributes;
using Astra.Core.Devices.Base;
using Astra.Core.Logs;
using Astra.Core.Plugins.Messaging;
using Astra.Plugins.DataAcquisition.Abstractions;
using Astra.Plugins.DataAcquisition.ViewModels;
using Astra.Plugins.DataAcquisition.Views;
using System.Buffers.Binary;

namespace Astra.Plugins.DataAcquisition.Devices
{
    [DeviceConfigUI(typeof(DataAcquisitionDeviceConfigView), typeof(DataAcquisitionDeviceConfigViewModel))]
    [DeviceDebugUI(typeof(DataAcquisitionDeviceDebugView), typeof(DataAcquisitionDeviceDebugViewModel))]
    public class DataAcquisitionDevice : DeviceBase<DataAcquisitionConfig>, IDataAcquisition
    {
        private readonly DataAcquisitionDeviceConnection _acquisitionConnection;
        private readonly IMessageBus _messageBus;
        private readonly ILogger _logger;
        private readonly SemaphoreSlim _stateLock = new(1, 1);
        private readonly object _eventLock = new();
        private CancellationTokenSource _acquisitionCts;
        private Task _acquisitionTask;
        private ManualResetEventSlim _pauseEvent = new(true);
        private AcquisitionState _state = AcquisitionState.Idle;
        private readonly Random _random = new();
        private bool _initialized;

        public DataAcquisitionDevice(
            DataAcquisitionConfig config,
            IMessageBus messageBus = null,
            ILogger logger = null)
            : base(new DataAcquisitionDeviceConnection(config, logger), config)
        {
            _acquisitionConnection = (DataAcquisitionDeviceConnection)_connection;
            _messageBus = messageBus;
            _logger = logger;
        }

        public AcquisitionState CurrentState => _state;

        public event EventHandler<DeviceMessage> DataReceived;

        public event EventHandler<Exception> ErrorOccurred;

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
                {
                    return;
                }

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

                var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                _acquisitionCts = linkedCts;
                _pauseEvent.Set();

                _acquisitionTask = Task.Run(() => RunAcquisitionLoopAsync(linkedCts.Token), CancellationToken.None);
                _state = AcquisitionState.Running;
                _logger?.Info($"[{DeviceName}] 数据采集已启动", LogCategory.Device);
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
                _logger?.Info($"[{DeviceName}] 数据采集已停止", LogCategory.Device);
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
            _logger?.Error($"[{DeviceName}] 初始化失败: {message}", exception, LogCategory.Device);
        }
        public override void Dispose()
        {
            _pauseEvent?.Set();
            _pauseEvent?.Dispose();
            _stateLock?.Dispose();
            _acquisitionCts?.Cancel();
            _acquisitionCts?.Dispose();
            base.Dispose();
        }
    }
}
