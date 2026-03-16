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
    public class DataAcquisitionDevice : DataAcquisitionDeviceBase
    {
        private readonly DataAcquisitionDeviceConnection _acquisitionConnection;
        private readonly Random _random = new();

        public DataAcquisitionDevice(
            DataAcquisitionConfig config,
            IMessageBus messageBus = null,
            Microsoft.Extensions.Logging.ILogger logger = null)
            : base(new DataAcquisitionDeviceConnection(config, logger), config, messageBus, logger)
        {
            _acquisitionConnection = (DataAcquisitionDeviceConnection)_connection;
        }

        protected override async Task AcquireAndProcessFrameAsync(CancellationToken token)
        {
            var frameInterval = CalculateFrameInterval();

            var payload = GenerateSampleFrame();
            PublishData(payload);

            WriteDataToChannels(payload);

            if (frameInterval > TimeSpan.Zero)
            {
                await Task.Delay(frameInterval, token).ConfigureAwait(false);
            }
        }

        private TimeSpan CalculateFrameInterval()
        {
            if (_config.SampleRate <= 0 || _config.BufferSize <= 0)
            {
                return TimeSpan.FromMilliseconds(100);
            }

            var seconds = (double)_config.BufferSize / _config.SampleRate;
            seconds = Math.Clamp(seconds, 0.01, 1.0);
            return TimeSpan.FromSeconds(seconds);
        }

        private byte[] GenerateSampleFrame()
        {
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

        private void WriteDataToChannels(byte[] payload)
        {
            if (payload == null || payload.Length == 0 || _channelMap.Count == 0)
                return;

            try
            {
                var channelCount = Math.Max(1, _config.ChannelCount);
                var bufferSize = Math.Max(1, _config.BufferSize);

                int floatCount = payload.Length / sizeof(float);
                if (floatCount != channelCount * bufferSize)
                {
                    Logger?.LogWarn($"[{DeviceName}] 数据大小不匹配: 期望 {channelCount * bufferSize}, 实际 {floatCount}", LogCategory.Device);
                    return;
                }

                Span<float> floatData = MemoryMarshal.Cast<byte, float>(payload);

                for (int channelIdx = 0; channelIdx < channelCount; channelIdx++)
                {
                    var channelConfig = _config.Channels?.FirstOrDefault(c => c.ChannelId == channelIdx);
                    if (channelConfig == null || !channelConfig.Enabled)
                        continue;

                    if (!_channelMap.TryGetValue(channelIdx, out var dataChannel))
                        continue;

                    float[] channelSamples = new float[bufferSize];
                    for (int i = 0; i < bufferSize; i++)
                    {
                        int sourceIndex = i * channelCount + channelIdx;
                        if (sourceIndex < floatData.Length)
                        {
                            channelSamples[i] = floatData[sourceIndex];
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
    }
}
