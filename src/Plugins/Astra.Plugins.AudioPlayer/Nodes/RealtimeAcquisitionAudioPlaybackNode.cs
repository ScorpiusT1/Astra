using Astra.Contract.Communication.Abstractions;
using Astra.Core.Devices;
using Astra.Core.Devices.Interfaces;
using Astra.Core.Devices.Management;
using Astra.Core.Nodes.Management;
using Astra.Core.Nodes.Models;
using Astra.Core.Plugins.Messaging;
using Astra.Plugins.DataAcquisition.Devices;
using Astra.Plugins.DataAcquisition.Providers;
using Astra.UI.Abstractions.Attributes;
using Astra.UI.PropertyEditors;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using NVHDataBridge.IO.WAV;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;

namespace Astra.Plugins.AudioPlayer.Nodes
{
    /// <summary>
    /// 订阅数据采集设备在消息总线上发布的实时帧（与 <see cref="DataAcquisitionDeviceBase.PublishData"/> 一致），
    /// 将指定通道转为单声道浮点音频并实时播放。需与采集节点并行；监听在采集卡停止采集（Idle）时结束，与采样时长一致。
    /// </summary>
    public class RealtimeAcquisitionAudioPlaybackNode : Node
    {
        private string _dataAcquisitionDeviceName = string.Empty;
        private string _channelName = string.Empty;

        [Display(Name = "采集卡", GroupName = "播放", Order = 1, Description = "首项为未选择；选定后须与多采集节点中的采集卡设备名一致")]
        [Editor(typeof(ComboBoxPropertyEditor))]
        [ItemsSource(typeof(AudioPlayerDesignTimeOptions), nameof(AudioPlayerDesignTimeOptions.GetAcquisitionDeviceNames), DisplayMemberPath = ".")]
        public string DataAcquisitionDeviceName
        {
            get => string.IsNullOrEmpty(_dataAcquisitionDeviceName)
                ? AudioPlayerDesignTimeOptions.UnselectedLabel
                : _dataAcquisitionDeviceName;
            set
            {
                var v = value ?? string.Empty;
                if (string.Equals(v, AudioPlayerDesignTimeOptions.UnselectedLabel, StringComparison.Ordinal))
                {
                    v = string.Empty;
                }

                if (string.Equals(_dataAcquisitionDeviceName, v, StringComparison.Ordinal))
                {
                    return;
                }

                _dataAcquisitionDeviceName = v;
                OnPropertyChanged();
                _channelName = string.Empty;
                OnPropertyChanged(nameof(ChannelName));
                OnPropertyChanged(nameof(ChannelOptions));
            }
        }

        /// <summary>
        /// 选定采集卡后填充通道下拉；未选采集卡时仅「未选择」（与 ValueLimitCheckNode 的通道列表行为一致）。
        /// </summary>
        public IEnumerable<string> ChannelOptions =>
            AudioPlayerDesignTimeOptions.GetChannelNamesForDevice(
                string.IsNullOrEmpty(_dataAcquisitionDeviceName) ? null : _dataAcquisitionDeviceName);

        [Display(Name = "通道", GroupName = "播放", Order = 2, Description = "未选采集卡时仅显示未选择；选定后首项为组内默认首通道")]
        [Editor(typeof(ComboBoxPropertyEditor))]
        [ItemsSource(nameof(ChannelOptions), DisplayMemberPath = ".")]
        public string ChannelName
        {
            get
            {
                if (string.IsNullOrEmpty(_dataAcquisitionDeviceName))
                {
                    return AudioPlayerDesignTimeOptions.UnselectedLabel;
                }

                return string.IsNullOrEmpty(_channelName)
                    ? AudioPlayerDesignTimeOptions.UseFirstChannelInGroupLabel
                    : _channelName;
            }
            set
            {
                var v = value ?? string.Empty;
                if (string.Equals(v, AudioPlayerDesignTimeOptions.UnselectedLabel, StringComparison.Ordinal) ||
                    string.Equals(v, AudioPlayerDesignTimeOptions.UseFirstChannelInGroupLabel, StringComparison.Ordinal))
                {
                    v = string.Empty;
                }

                if (string.Equals(_channelName, v, StringComparison.Ordinal))
                {
                    return;
                }

                _channelName = v;
                OnPropertyChanged();
            }
        }

        [Display(Name = "输出增益", GroupName = "播放", Order = 3, Description = "将采集值放大/缩小到扬声器合适电平（依传感器量纲调整）")]
        public double Gain { get; set; } = 1.0;

        /// <summary>
        /// 持久化保存 WASAPI 设备 ID；null 或空表示系统默认输出。
        /// </summary>
        [JsonProperty("PlaybackMmDeviceId", Order = 40)]
        [Display(Name = "播放设备", GroupName = "播放", Order = 4, Description = "WASAPI 输出；首项为系统默认播放设备")]
        [Editor(typeof(ComboBoxPropertyEditor))]
        [ItemsSource(typeof(AudioPlayerDesignTimeOptions), nameof(AudioPlayerDesignTimeOptions.GetPlaybackDeviceOptions),
            DisplayMemberPath = "DisplayName", SelectedValuePath = "MmDeviceId")]
        public string? PlaybackMmDeviceId { get; set; }

        /// <summary>
        /// 旧版序列化字段（仅反序列化）：「友好名\tMmDeviceId」整串。
        /// </summary>
        [JsonProperty("PlaybackDeviceSelection", NullValueHandling = NullValueHandling.Ignore)]
        private string? LegacyPlaybackDeviceSelection { get; set; }

        // Newtonsoft ShouldSerialize* 约定
        private bool ShouldSerializeLegacyPlaybackDeviceSelection() => false;

        [OnDeserialized]
        private void OnDeserialized(StreamingContext context)
        {
            if (!string.IsNullOrEmpty(PlaybackMmDeviceId) || string.IsNullOrEmpty(LegacyPlaybackDeviceSelection))
            {
                return;
            }

            PlaybackMmDeviceId = ExtractMmDeviceIdFromLegacyPlaybackSelection(LegacyPlaybackDeviceSelection);
        }

        protected override async Task<ExecutionResult> ExecuteCoreAsync(NodeContext context, CancellationToken cancellationToken)
        {
            var log = context.CreateExecutionLogger($"实时采集监听:{Name}");
            var bus = context.ServiceProvider?.GetService<IMessageBus>();
            if (bus == null)
            {
                log.Error("未解析到 IMessageBus，无法订阅采集数据。");
                return ExecutionResult.Failed("未解析到 IMessageBus");
            }

            if (string.IsNullOrEmpty(_dataAcquisitionDeviceName))
            {
                log.Warn("未选择采集卡。");
                return ExecutionResult.Skip("未选择采集卡");
            }

            if (!DataAcquisitionCardProvider.TryGetDeviceIdByDisplayName(_dataAcquisitionDeviceName, out var deviceId))
            {
                log.Warn($"无法解析设备 ID: {_dataAcquisitionDeviceName}");
                return ExecutionResult.Skip("无法解析采集卡设备 ID");
            }

            var deviceManager = context.ServiceProvider?.GetService<IDeviceManager>();
            var channelIndex = ResolveChannelIndex(_dataAcquisitionDeviceName, _channelName, deviceManager);
            if (channelIndex < 0)
            {
                log.Warn("无法解析通道索引。");
                return ExecutionResult.Skip("无法解析通道");
            }

            var topic = $"devices/{deviceId}/data";
            var playbackMmDeviceId = string.IsNullOrWhiteSpace(PlaybackMmDeviceId)
                ? null
                : PlaybackMmDeviceId.Trim();
            RealtimeAudioPlayer? player = null;
            var playerLock = new object();
            var firstFrameReceived = false;
            var executionController = context.GetMetadata<IWorkflowExecutionController>("WorkflowExecutionController");

            async Task WaitIfPausedAsync()
            {
                if (executionController != null)
                {
                    await executionController.WaitIfPausedAsync(cancellationToken).ConfigureAwait(false);
                }
            }

            void OnMessage(DeviceMessage message)
            {
                try
                {
                    var data = message.Data;
                    if (data == null || data.Length < sizeof(float))
                    {
                        return;
                    }

                    var bufferSize = GetIntProperty(message, "BufferSize", 0);
                    var channelCount = GetIntProperty(message, "ChannelCount", 0);
                    var sampleRate = GetIntProperty(message, "SampleRate", 0);
                    if (bufferSize <= 0 || channelCount <= 0 || sampleRate <= 0)
                    {
                        return;
                    }

                    var totalFloats = data.Length / sizeof(float);
                    if (totalFloats != (long)bufferSize * channelCount)
                    {
                        return;
                    }

                    if (channelIndex >= channelCount)
                    {
                        return;
                    }

                    lock (playerLock)
                    {
                        if (player == null)
                        {
                            player = new RealtimeAudioPlayer(sampleRate, channels: 1, bitsPerSample: 32, playbackMmDeviceId: playbackMmDeviceId);
                            player.Start();
                        }

                        var span = MemoryMarshal.Cast<byte, float>(data.AsSpan());
                        var offset = channelIndex * bufferSize;
                        var slice = span.Slice(offset, bufferSize);
                        var chunk = new float[bufferSize];
                        slice.CopyTo(chunk);
                        if (Math.Abs(Gain - 1.0) > 1e-9)
                        {
                            var g = (float)Gain;
                            for (var i = 0; i < chunk.Length; i++)
                            {
                                chunk[i] *= g;
                            }
                        }

                        player.AddSamples(chunk);
                    }

                    firstFrameReceived = true;
                }
                catch
                {
                    // 单帧失败不终止监听
                }
            }

            bus.Subscribe<DeviceMessage>(topic, OnMessage);
            try
            {
                log.Info($"开始监听 {topic}，通道索引={channelIndex}；采集停止后结束。");

                while (!cancellationToken.IsCancellationRequested)
                {
                    await WaitIfPausedAsync().ConfigureAwait(false);

                    if (ShouldStopListening(deviceManager, deviceId, firstFrameReceived))
                    {
                        break;
                    }

                    await Task.Delay(50, cancellationToken).ConfigureAwait(false);
                }
            }
            finally
            {
                bus.Unsubscribe<DeviceMessage>(topic, OnMessage);
                lock (playerLock)
                {
                    player?.Dispose();
                    player = null;
                }
            }

            if (!firstFrameReceived)
            {
                log.Warn("未收到任何采集帧。请确认上游已启动采集，且设备正在发布数据。");
                return ExecutionResult.Skip("未收到采集数据（请确认采集已启动）");
            }

            log.Info("实时监听结束。");
            return ExecutionResult.Successful("实时采集监听结束");
        }

        /// <summary>
        /// 采集已结束时停止监听：在至少收到一帧后，设备变为 Idle 表示采样结束；Error 也结束；未收到帧前不因 Idle 退出（避免早于采集启动）。
        /// </summary>
        private static bool ShouldStopListening(IDeviceManager? deviceManager, string deviceId, bool receivedAnyFrame)
        {
            if (deviceManager == null)
            {
                return false;
            }

            try
            {
                var r = deviceManager.GetDevice(deviceId);
                if (!r.Success || r.Data is not IDataAcquisition daq)
                {
                    return false;
                }

                var state = daq.GetState();
                if (state == AcquisitionState.Error)
                {
                    return true;
                }

                if (receivedAnyFrame && state == AcquisitionState.Idle)
                {
                    return true;
                }
            }
            catch
            {
            }

            return false;
        }

        private static int ResolveChannelIndex(string deviceDisplayName, string? channelName, IDeviceManager? deviceManager)
        {
            if (deviceManager == null)
            {
                return -1;
            }

            try
            {
                var all = deviceManager.GetAllDevices();
                if (!all.Success || all.Data == null)
                {
                    return -1;
                }

                foreach (var device in all.Data)
                {
                    if (device.Type != DeviceType.DataAcquisition)
                    {
                        continue;
                    }

                    if (!string.Equals(device.DeviceName?.Trim(), deviceDisplayName.Trim(), StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (device is not DataAcquisitionDeviceBase daq)
                    {
                        return -1;
                    }

                    var names = daq.GetConfiguredEnabledChannelNames();
                    if (names.Count == 0)
                    {
                        return 0;
                    }

                    if (string.IsNullOrWhiteSpace(channelName))
                    {
                        return 0;
                    }

                    for (var i = 0; i < names.Count; i++)
                    {
                        if (string.Equals(names[i], channelName.Trim(), StringComparison.OrdinalIgnoreCase))
                        {
                            return i;
                        }
                    }

                    return -1;
                }
            }
            catch
            {
            }

            return -1;
        }

        private static string? ExtractMmDeviceIdFromLegacyPlaybackSelection(string stored)
        {
            if (string.IsNullOrWhiteSpace(stored))
            {
                return null;
            }

            if (string.Equals(stored, AudioPlayerDesignTimeOptions.DefaultPlaybackDeviceLabel, StringComparison.Ordinal))
            {
                return null;
            }

            var tab = stored.IndexOf('\t');
            if (tab > 0 && tab < stored.Length - 1)
            {
                var id = stored[(tab + 1)..].Trim();
                return string.IsNullOrEmpty(id) ? null : id;
            }

            return null;
        }

        private static int GetIntProperty(DeviceMessage message, string key, int defaultValue)
        {
            if (message.Properties == null || !message.Properties.TryGetValue(key, out var v) || v == null)
            {
                return defaultValue;
            }

            try
            {
                return Convert.ToInt32(v);
            }
            catch
            {
                return defaultValue;
            }
        }
    }
}
