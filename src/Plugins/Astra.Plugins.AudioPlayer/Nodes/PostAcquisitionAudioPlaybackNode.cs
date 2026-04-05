using Astra.Contract.Communication.Abstractions;
using Astra.Plugins.AudioPlayer.Helpers;
using Astra.Plugins.AudioPlayer.Models;
using Astra.Core.Devices;
using Astra.Core.Devices.Interfaces;
using Astra.Core.Devices.Management;
using Astra.Core.Constants;
using Astra.Core.Nodes.Models;
using Astra.Core.Nodes.Management;
using Astra.UI.Abstractions.Attributes;
using Microsoft.Extensions.DependencyInjection;
using Astra.UI.PropertyEditors;
using Newtonsoft.Json;
using NVHDataBridge.IO.WAV;
using NVHDataBridge.Models;
using System.ComponentModel.DataAnnotations;
using System.Runtime.Serialization;

namespace Astra.Plugins.AudioPlayer.Nodes
{
    /// <summary>
    /// 在多采集节点完成之后，从 Raw 数据工件中读取指定通道并播放（顺序连线：多采集 → 本节点）。
    /// 数据与上游多采集节点写入 Raw 工件的规则一致。
    /// 可通过 <see cref="Gain"/> 调整送播放器的幅值。
    /// </summary>
    public class PostAcquisitionAudioPlaybackNode : Node
    {
        private string _dataAcquisitionDeviceName = string.Empty;
        private string _channelName = string.Empty;

        [JsonIgnore]
        private string? _autoPlaybackDevChSuffix;

        [Display(Name = "采集卡", GroupName = "播放", Order = 1, Description = "须与上游多采集节点中勾选的采集卡名称一致")]
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
                SyncDisplayNameFromPlaybackSelection();
            }
        }

        public IEnumerable<string> ChannelOptions =>
            AudioPlayerDesignTimeOptions.GetChannelNamesForDevice(
                string.IsNullOrEmpty(_dataAcquisitionDeviceName) ? null : _dataAcquisitionDeviceName);

        [Display(Name = "通道", GroupName = "播放", Order = 2, Description = "未选采集卡时仅未选择；选定后可选组内首通道或具体通道名")]
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
                SyncDisplayNameFromPlaybackSelection();
            }
        }

        private void SyncDisplayNameFromPlaybackSelection()
        {
            if (string.IsNullOrWhiteSpace(_dataAcquisitionDeviceName))
            {
                ApplyAutoChannelSuffixToDisplayName(ref _autoPlaybackDevChSuffix, "");
                return;
            }

            var dev = _dataAcquisitionDeviceName.Trim();
            var frag = string.IsNullOrWhiteSpace(_channelName)
                ? dev
                : $"{dev}/{_channelName.Trim()}";
            ApplyAutoChannelSuffixToDisplayName(ref _autoPlaybackDevChSuffix, frag);
        }

        [OnDeserialized]
        private void OnDeserialized(StreamingContext context)
        {
            SyncDisplayNameFromPlaybackSelection();
        }

        [Display(Name = "输出增益", GroupName = "播放", Order = 3, Description = "在送播放器前将全部样本乘以该系数")]
        public double Gain { get; set; } = 1.0;

        [JsonProperty("PlaybackMmDeviceId", Order = 40)]
        [Display(Name = "播放设备", GroupName = "播放", Order = 4, Description = "WASAPI 输出；首项为系统默认播放设备")]
        [Editor(typeof(ComboBoxPropertyEditor))]
        [ItemsSource(typeof(AudioPlayerDesignTimeOptions), nameof(AudioPlayerDesignTimeOptions.GetPlaybackDeviceOptions),
            DisplayMemberPath = "DisplayName", SelectedValuePath = "MmDeviceId")]
        public string? PlaybackMmDeviceId { get; set; }

        protected override async Task<ExecutionResult> ExecuteCoreAsync(NodeContext context, CancellationToken cancellationToken)
        {
            var log = context.CreateExecutionLogger($"采集后播放:{Name}");
            var executionController = context.GetMetadata<IWorkflowExecutionController>(ExecutionContextMetadataKeys.WorkflowExecutionController);

            async Task WaitIfPausedAsync()
            {
                if (executionController != null)
                {
                    await executionController.WaitIfPausedAsync(cancellationToken).ConfigureAwait(false);
                }
            }

            if (string.IsNullOrEmpty(_dataAcquisitionDeviceName))
            {
                log.Warn("未选择采集卡。");
                return ExecutionResult.Skip("未选择采集卡");
            }

            if (!AcquisitionRawArtifactHelper.TryResolveRawArtifactKey(
                    context,
                    Id,
                    _dataAcquisitionDeviceName,
                    out var artifactKey,
                    out var resolveError))
            {
                log.Warn(resolveError);
                return ExecutionResult.Skip(resolveError);
            }

            if (!context.TryGetArtifact<NvhMemoryFile>(artifactKey, out var nvhFile) || nvhFile == null)
            {
                log.Warn($"无法从工件存储读取 Raw 数据，键={artifactKey}。请确认上游多采集已成功写入数据。");
                return ExecutionResult.Skip("无法读取采集 Raw 数据（请确认上游多采集已完成并产出数据）");
            }

            if (!nvhFile.TryGetGroup(AcquisitionRawArtifactHelper.NvhSignalGroupName, out var group) || group == null)
            {
                group = nvhFile.Groups.Values.FirstOrDefault();
            }

            if (group == null)
            {
                log.Warn("NVH 数据中无可用的数据组。");
                return ExecutionResult.Skip("数据中无可用的数据组");
            }

            var deviceManager = context.ServiceProvider?.GetService<IDeviceManager>();
            if (!TryResolveStorageChannelName(_dataAcquisitionDeviceName, _channelName, deviceManager, out var storageChannelName, out var channelError))
            {
                log.Warn(channelError);
                return ExecutionResult.Skip(channelError);
            }

            if (!group.TryGetChannel(storageChannelName, out var channelBase) || channelBase == null)
            {
                log.Warn($"通道不存在: {storageChannelName}");
                return ExecutionResult.Skip($"通道不存在: {storageChannelName}");
            }

            var samples = ExtractSamplesPeek(channelBase);
            if (samples == null || samples.Length == 0)
            {
                log.Warn("通道样本为空。");
                return ExecutionResult.Skip("通道无样本数据");
            }

            var sampleRate = ResolveSampleRate(channelBase, group);
            if (sampleRate <= 0)
            {
                sampleRate = 44100;
            }

            if (Math.Abs(Gain - 1.0) > 1e-12)
            {
                var g = (float)Gain;
                for (var i = 0; i < samples.Length; i++)
                {
                    samples[i] *= g;
                }
            }

            var playbackMmDeviceId = string.IsNullOrWhiteSpace(PlaybackMmDeviceId) ? null : PlaybackMmDeviceId.Trim();

            log.Info($"播放通道「{storageChannelName}」，样本数={samples.Length}，采样率={sampleRate} Hz。");

            const int chunkSamples = AstraSharedConstants.AudioDefaults.ChunkSamples;
            var maxBufferedSeconds = 2.0;

            using var player = new RealtimeAudioPlayer(sampleRate, channels: 1, bitsPerSample: 32, playbackMmDeviceId: playbackMmDeviceId);
            player.Start();

            try
            {
                for (var offset = 0; offset < samples.Length; offset += chunkSamples)
                {
                    await WaitIfPausedAsync().ConfigureAwait(false);
                    cancellationToken.ThrowIfCancellationRequested();

                    var len = Math.Min(chunkSamples, samples.Length - offset);
                    var part = new float[len];
                    Array.Copy(samples, offset, part, 0, len);

                    while (player.BufferedDuration > maxBufferedSeconds && !cancellationToken.IsCancellationRequested)
                    {
                        await WaitIfPausedAsync().ConfigureAwait(false);
                        await Task.Delay(20, cancellationToken).ConfigureAwait(false);
                    }

                    player.AddSamples(part);
                }

                await WaitIfPausedAsync().ConfigureAwait(false);
                await player.WaitForBufferEmptyAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                log.Warn("播放被取消。");
                return ExecutionResult.Cancel("播放被取消");
            }

            log.Info("播放结束。");
            return ExecutionResult.Successful("采集数据播放完成");
        }

        private static bool TryResolveStorageChannelName(
            string deviceDisplayName,
            string? channelNameUi,
            IDeviceManager? deviceManager,
            out string storageChannelName,
            out string error)
        {
            storageChannelName = string.Empty;
            error = string.Empty;

            if (deviceManager == null)
            {
                error = "未解析到 IDeviceManager，无法解析通道名";
                return false;
            }

            try
            {
                var all = deviceManager.GetAllDevices();
                if (!all.Success || all.Data == null)
                {
                    error = "无法枚举设备";
                    return false;
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

                    if (device is not IDataAcquisition daq)
                    {
                        error = "设备未实现数据采集接口，无法枚举已启用通道";
                        return false;
                    }

                    var names = daq.GetConfiguredEnabledChannelNames();
                    if (names.Count == 0)
                    {
                        error = "采集卡未配置启用通道";
                        return false;
                    }

                    if (string.IsNullOrWhiteSpace(channelNameUi))
                    {
                        storageChannelName = names[0];
                        return true;
                    }

                    for (var i = 0; i < names.Count; i++)
                    {
                        if (string.Equals(names[i], channelNameUi.Trim(), StringComparison.OrdinalIgnoreCase))
                        {
                            storageChannelName = names[i];
                            return true;
                        }
                    }

                    error = $"找不到通道「{channelNameUi}」";
                    return false;
                }
            }
            catch
            {
                error = "解析通道名时异常";
                return false;
            }

            error = $"未找到采集卡设备: {deviceDisplayName}";
            return false;
        }

        private static int ResolveSampleRate(NvhMemoryChannelBase channel, NvhMemoryGroup group)
        {
            if (channel.Properties.TryGet<int>("SampleRate", out var sr) && sr > 0)
            {
                return sr;
            }

            if (group.Properties.TryGet<int>("SampleRate", out var gsr) && gsr > 0)
            {
                return gsr;
            }

            return 0;
        }

        private static float[]? ExtractSamplesPeek(NvhMemoryChannelBase channel)
        {
            if (channel is NvhMemoryChannel<float> fc)
            {
                var span = fc.PeekAll();
                if (span.Length == 0)
                {
                    return Array.Empty<float>();
                }

                var arr = new float[span.Length];
                span.CopyTo(arr);
                return arr;
            }

            if (channel is NvhMemoryChannel<double> dc)
            {
                var span = dc.PeekAll();
                if (span.Length == 0)
                {
                    return Array.Empty<float>();
                }

                var arr = new float[span.Length];
                for (var i = 0; i < span.Length; i++)
                {
                    arr[i] = (float)span[i];
                }

                return arr;
            }

            return null;
        }
    }
}
