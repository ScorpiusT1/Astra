using Astra.Core.Devices.Attributes;
using Astra.Core.Logs;
using Astra.Core.Logs.Extensions;
using Astra.Core.Plugins.Messaging;
using Astra.Plugins.DataAcquisition.ViewModels;
using Astra.Plugins.DataAcquisition.Views;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Runtime.InteropServices;

namespace Astra.Plugins.DataAcquisition.Devices
{
    /// <summary>
    /// BRC数据采集设备
    /// </summary>
    [DeviceDebugUI(typeof(DataAcquisitionDeviceDebugView), typeof(DataAcquisitionDeviceDebugViewModel))]
    public class BRCDataAcquisitionDevice : DataAcquisitionDeviceBase
    {
        private readonly BRCDataAcquisitionDeviceConnection _brcConnection;

        // 数据缓冲区
        private double[] _dataBuffer;
        private readonly Dictionary<int, float[]> _channelWriteBuffers = new();

        /// <summary>
        /// 运行时使用的构造函数（由 DI / 工厂创建）
        /// </summary>
        public BRCDataAcquisitionDevice(
            DataAcquisitionConfig config,
            IMessageBus messageBus = null,
            Microsoft.Extensions.Logging.ILogger logger = null)
            : base(new BRCDataAcquisitionDeviceConnection(config, logger), config, messageBus, logger)
        {
            _brcConnection = (BRCDataAcquisitionDeviceConnection)_connection;
        }

        /// <summary>
        /// 供 JSON 反序列化使用的构造函数。
        /// 注意参数名要与 JSON 中的属性名 "currentConfig" 对应，
        /// 这样 Json.NET 才会把脚本里的配置对象传进来，而不是传 null。
        /// </summary>
        [JsonConstructor]
        public BRCDataAcquisitionDevice(DataAcquisitionConfig currentConfig)
            : this(currentConfig, null, null)
        {
        }

        /// <summary>
        /// 当上位机通过配置管理器更新采集卡配置（点击保存）且不需要重启设备时，
        /// 基类会调用此方法应用“可热更新”的配置属性。
        /// 这里同步把最新配置传递给连接层，使 BRCDataAcquisitionDeviceConnection
        /// 在后续 DeviceExists/Connect/Configure 调用中使用最新参数。
        /// </summary>
        /// <param name="newConfig">最新的采集配置</param>
        /// <param name="changedProps">发生变更的属性名称列表</param>
        protected override void ApplyHotUpdateProperties(DataAcquisitionConfig newConfig, List<string> changedProps)
        {
            base.ApplyHotUpdateProperties(newConfig, changedProps);

            // 将最新配置同步给连接管理类，保证扫描设备和属性配置使用最新参数
            _brcConnection?.UpdateConfig(newConfig);
        }

        /// <summary>
        /// 获取当前已解析的硬件模块信息（未连接时返回 null）。
        /// </summary>
        public SDKs.ModuleInfo GetModuleInfo() => _brcConnection.GetModuleInfo();

        protected override async Task OnInitializeAsync()
        {
            EnsureRuntimeBuffers();
            await Task.CompletedTask;
        }

        protected override Task OnStartHardwareAsync(CancellationToken token)
        {
            var brcDevice = _brcConnection.GetBrcDevice();
            if (brcDevice == null)
            {
                RaiseInitializationError("BRC设备实例无效");
                return Task.CompletedTask;
            }

            var moduleInfo = _brcConnection.GetModuleInfo();
            if (moduleInfo == null)
            {
                RaiseInitializationError("模块信息无效");
                return Task.CompletedTask;
            }

            // 每次启动前按最新配置校准缓冲，避免仅首次初始化导致配置变更后缓冲未更新。
            EnsureRuntimeBuffers();

            // 将调试界面可能修改过的通道参数（耦合方式、激励电流等）重新推送到硬件。
            _brcConnection.ApplyChannelSettings();

            brcDevice.Start();

            return Task.CompletedTask;
        }

        protected override Task OnStopHardwareAsync()
        {
            try
            {
                var brcDevice = _brcConnection.GetBrcDevice();
                brcDevice?.Stop();
            }
            catch (Exception ex)
            {
                Logger?.LogWarn($"[{DeviceName}] 停止BRC采集失败: {ex.Message}", LogCategory.Device);
            }

            return Task.CompletedTask;
        }

        protected override Task AcquireAndProcessFrameAsync(CancellationToken token)
        {
            var brcDevice = _brcConnection.GetBrcDevice();
            if (brcDevice == null)
            {
                RaiseInitializationError("BRC设备实例无效");
                return Task.CompletedTask;
            }

            var enabledChannels = _config.Channels?.Where(c => c.Enabled).ToList()
                ?? Enumerable.Range(1, _config.ChannelCount).Select(i => new Configs.DAQChannelConfig { ChannelId = i, Enabled = true }).ToList();
            var bufferSize = _config.BufferSize;
            var timeout = TimeSpan.FromMilliseconds(3000);

            var bufferMemory = new Memory<double>(_dataBuffer);
            brcDevice.GetChannelsData(bufferMemory, timeout);

            WriteDataToChannels(bufferMemory.Span, enabledChannels, bufferSize);

            var payload = ConvertToByteArray(bufferMemory.Span);
            PublishData(payload);
            return Task.CompletedTask;
        }

        private byte[] ConvertToByteArray(Span<double> data)
        {
            var bytes = new byte[data.Length * sizeof(float)];
            var floatSpan = MemoryMarshal.Cast<byte, float>(bytes);
            for (int i = 0; i < data.Length; i++)
            {
                floatSpan[i] = (float)data[i];
            }
            return bytes;
        }

        private void WriteDataToChannels(Span<double> data, List<Configs.DAQChannelConfig> enabledChannels, int bufferSize)
        {
            if (data.Length == 0 || _channelMap.Count == 0)
                return;

            try
            {
                var channelCount = enabledChannels.Count;

                for (int channelIdx = 0; channelIdx < channelCount; channelIdx++)
                {
                    var channelConfig = enabledChannels[channelIdx];
                    var channelId = channelConfig.ChannelId;

                    if (!_channelMap.TryGetValue(channelId, out var dataChannel))
                        continue;

                    if (!_channelWriteBuffers.TryGetValue(channelId, out var channelSamples) || channelSamples.Length != bufferSize)
                    {
                        channelSamples = new float[bufferSize];
                        _channelWriteBuffers[channelId] = channelSamples;
                    }

                    for (int i = 0; i < bufferSize; i++)
                    {
                        // BRC采集卡返回的是按通道分段的数据：
                        // [ch1(0..N-1), ch2(0..N-1), ...]
                        int sourceIndex = channelIdx * bufferSize + i;
                        if (sourceIndex < data.Length)
                        {
                            channelSamples[i] = (float)data[sourceIndex];
                        }
                    }

                    dataChannel.WriteData<float>(channelSamples);
                }
            }
            catch (Exception ex)
            {
                Logger?.LogError($"[{DeviceName}] 写入数据到通道失败: {ex.Message}", ex, LogCategory.Device);
            }
        }

        public override void Dispose()
        {
            _channelWriteBuffers.Clear();
            base.Dispose();
        }

        private void EnsureRuntimeBuffers()
        {
            var enabledChannels = _config.Channels?.Where(c => c.Enabled).ToList()
                ?? Enumerable.Range(1, _config.ChannelCount)
                    .Select(i => new Configs.DAQChannelConfig { ChannelId = i, Enabled = true })
                    .ToList();

            var bufferSize = _config.BufferSize;
            var expectedDataLength = enabledChannels.Count * bufferSize;
            if (_dataBuffer == null || _dataBuffer.Length != expectedDataLength)
            {
                _dataBuffer = new double[expectedDataLength];
            }

            // 每次按所有配置通道重建写缓存。
            var allChannels = _config.Channels?.ToList()
                ?? Enumerable.Range(1, _config.ChannelCount)
                    .Select(i => new Configs.DAQChannelConfig { ChannelId = i, Enabled = true })
                    .ToList();

            _channelWriteBuffers.Clear();
            foreach (var channel in allChannels)
            {
                _channelWriteBuffers[channel.ChannelId] = new float[bufferSize];
            }
        }
    }
}

