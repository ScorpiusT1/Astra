using Astra.Contract.Communication.Abstractions;
using Astra.Core.Devices;
using Astra.Core.Devices.Interfaces;
using Astra.Plugins.DataAcquisition.Devices;
using Astra.Plugins.DataAcquisition.SDKs;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ScottPlot;
using System.Buffers;
using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using System.Windows;

namespace Astra.Plugins.DataAcquisition.ViewModels
{
    public partial class DataAcquisitionDeviceDebugViewModel : ObservableObject
    {
        private BRCDataAcquisitionDevice _device;
        private AcquisitionState _currentState;
        private string _statusMessage = "就绪";
        private long _totalFramesReceived;
        private long _totalBytesReceived;
        private DateTime _lastDataTime;
        private bool _isMonitoring;
        private CancellationTokenSource _monitoringCts;
        private readonly object _waveformBufferLock = new();
        private readonly Dictionary<int, List<double>> _waveformSampleBuffers = new();

        // 通道调试信息（是否启用、名称等）
        public partial class ChannelDebugItem : ObservableObject
        {
            [ObservableProperty]
            private int _channelId;

            [ObservableProperty]
            private string _name;

            [ObservableProperty]
            private bool _isEnabled;

            [ObservableProperty]
            private ScottPlot.Color _color;

            // 供外部（视图模型）监听通道启用状态变化，及时同步曲线可见性。
            public event Action<ChannelDebugItem, bool>? IsEnabledChanged;

            partial void OnIsEnabledChanged(bool value)
            {
                IsEnabledChanged?.Invoke(this, value);
            }
        }

        public ObservableCollection<ChannelDebugItem> Channels { get; } = new();

        [ObservableProperty]
        private ChannelDebugItem? _selectedChannel;

        // 波形数据更新事件，由视图订阅以刷新 ScottPlot
        public event Action<Dictionary<int, double[]>>? WaveformUpdated;
        // 通道可见性变化事件：用于无新数据时也能立即切换曲线显示/隐藏。
        public event Action<int, bool>? ChannelVisibilityChanged;

        public BRCDataAcquisitionDevice Device
        {
            get => _device;
            set
            {
                if (SetProperty(ref _device, value))
                {
                    OnDeviceChanged();
                }
            }
        }

        public AcquisitionState CurrentState
        {
            get => _currentState;
            set
            {
                if (SetProperty(ref _currentState, value))
                {
                    // 采集状态变化时更新按钮可用性
                    StartAcquisitionAsyncCommand?.NotifyCanExecuteChanged();
                    StopAcquisitionAsyncCommand?.NotifyCanExecuteChanged();
                }
            }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public long TotalFramesReceived
        {
            get => _totalFramesReceived;
            set => SetProperty(ref _totalFramesReceived, value);
        }

        public long TotalBytesReceived
        {
            get => _totalBytesReceived;
            set => SetProperty(ref _totalBytesReceived, value);
        }

        public DateTime LastDataTime
        {
            get => _lastDataTime;
            set => SetProperty(ref _lastDataTime, value);
        }

        public string DeviceName => _device?.DeviceName ?? "未知设备";
        public string DeviceId => _device?.DeviceId ?? string.Empty;
        public string SerialNumber => (_device?.CurrentConfig as DataAcquisitionConfig)?.SerialNumber ?? string.Empty;

        // 配置中的采样率（只读，不允许通过调试界面写回配置）
        public double SampleRate => (_device?.CurrentConfig as DataAcquisitionConfig)?.SampleRate ?? 0.0;
        public int ChannelCount => (_device?.CurrentConfig as DataAcquisitionConfig)?.ChannelCount ?? 0;
        public int BufferSize => (_device?.CurrentConfig as DataAcquisitionConfig)?.BufferSize ?? 0;

        public ObservableCollection<string> LogMessages { get; } = new();

        [ObservableProperty]
        private string _couplingMode = "AC";

        public IReadOnlyList<string> CouplingModes { get; } = new[] { "AC", "DC" };

        /// <summary>
        /// 调试界面中的激励电流（mA），仅用于调试显示/控制，不写回配置。
        /// </summary>
        [ObservableProperty]
        private double _excitationCurrent;

        /// <summary>
        /// 激励电流选项（mA）：0 和 4。
        /// </summary>
        public IReadOnlyList<double> ExcitationCurrentOptions { get; } = new[] { 0d, 4d };

        /// <summary>
        /// 调试界面使用的采样率（默认取配置的采样率，但修改不会写回配置）
        /// </summary>
        [ObservableProperty]
        private double _debugSampleRate;

        /// <summary>
        /// 可选采样率列表，用于调试界面下拉选择（与配置界面保持一致）
        /// </summary>
        public IReadOnlyList<double> SampleRateOptions { get; } = new List<double>
        {
            1024.0,
            1280.0,
            1563.0,
            1920.0,
            2560.0,
            3072.0,
            3413.333,
            3657.143,
            3938.462,
            4266.667,
            4654.545,
            5120.0,
            5688.889,
            6400.0,
            7314.286,
            8533.333,
            10240.0,
            12800.0,
            17066.667,
            25600.0,
            48000.0,
            51200.0
        };

        /// <summary>
        /// 调试界面中的采样点数（用于波形显示），默认等于采样率，可独立编辑。
        /// </summary>
        [ObservableProperty]
        private int _debugSampleCount;

        // 手动命令属性（避免依赖 SourceGenerator）
        public IAsyncRelayCommand StartAcquisitionAsyncCommand { get; }
        public IAsyncRelayCommand StopAcquisitionAsyncCommand { get; }
        public IAsyncRelayCommand ScanSerialNumbersCommand { get; }

        public ObservableCollection<string> AvailableSerialNumbers { get; } = new();

        [ObservableProperty]
        private string _selectedSerialNumber;


        [RelayCommand]
        public void ResetStatistics()
        {
            TotalFramesReceived = 0;
            TotalBytesReceived = 0;
            LastDataTime = DateTime.MinValue;
            StatusMessage = "统计已重置";
            AddLogMessage("统计信息已重置");
        }

        [RelayCommand]
        public void ClearLogs()
        {
            LogMessages.Clear();
            AddLogMessage("日志已清空");
        }

        public DataAcquisitionDeviceDebugViewModel(IDevice device)
        {
            Device = device as BRCDataAcquisitionDevice;
            DebugSampleRate = SampleRate;

            // 采样点默认与采样率相同（取整）
            DebugSampleCount = DebugSampleRate > 0 ? (int)DebugSampleRate : BufferSize;

            // 根据当前耦合方式初始化激励电流
            if (string.Equals(CouplingMode, "AC", StringComparison.OrdinalIgnoreCase))
                ExcitationCurrent = 4.0;
            else
                ExcitationCurrent = 0.0;

            // 初始化采集控制命令（互斥：运行中只能停止，空闲时只能开始）
            StartAcquisitionAsyncCommand = new AsyncRelayCommand(
                StartAcquisitionAsync,
                () => CurrentState != AcquisitionState.Running);

            StopAcquisitionAsyncCommand = new AsyncRelayCommand(
                StopAcquisitionAsync,
                () => CurrentState == AcquisitionState.Running);

            // 扫描序列号命令
            ScanSerialNumbersCommand = new AsyncRelayCommand(ScanSerialNumbersAsync);

            AvailableSerialNumbers?.Clear();

            if (_device?.CurrentConfig is DataAcquisitionConfig cfg)
            {
                if (!string.IsNullOrWhiteSpace(cfg.SerialNumber))
                {
                    AvailableSerialNumbers.Add(cfg.SerialNumber);
                    SelectedSerialNumber = cfg.SerialNumber;
                }
            }
        }

        /// <summary>
        /// 当调试界面中的采样率修改时，同步更新采样点默认值（仍可手动再调整）。
        /// </summary>
        /// <param name="value">新的采样率</param>
        partial void OnDebugSampleRateChanged(double value)
        {
            if (value > 0)
            {
                DebugSampleCount = (int)value;
            }
            else
            {
                DebugSampleCount = BufferSize;
            }
        }

        partial void OnDebugSampleCountChanged(int value)
        {
            lock (_waveformBufferLock)
            {
                _waveformSampleBuffers.Clear();
            }
        }

        partial void OnCouplingModeChanged(string value)
        {
            // 选择 AC 时，激励电流默认 4mA；选择 DC 时默认 0mA
            if (string.Equals(value, "AC", StringComparison.OrdinalIgnoreCase))
            {
                ExcitationCurrent = 4.0;
            }
            else if (string.Equals(value, "DC", StringComparison.OrdinalIgnoreCase))
            {
                ExcitationCurrent = 0.0;
            }
        }

        private void OnDeviceChanged()
        {
            if (_device != null)
            {
                // 订阅设备事件
                _device.DataReceived += OnDataReceived;
                _device.ErrorOccurred += OnErrorOccurred;

                // 更新状态
                UpdateState();

                InitializeChannels();
            }
        }

        private void InitializeChannels()
        {
            Channels.Clear();

            var colorCategory = new ScottPlot.Palettes.Category10();

            if (_device?.CurrentConfig is DataAcquisitionConfig cfg && cfg.Channels != null)
            {
                foreach (var ch in cfg.Channels)
                {
                    // 调试界面名称与配置保持一致，不再按 ID/序号二次重命名。
                    string name = ch.ChannelName;
                    if (string.IsNullOrWhiteSpace(name))
                        name = $"CH{ch.ChannelId}";

                    int colorIndex =  (ch.ChannelId - 1) % colorCategory.Colors.Length; // 通道 ID 从 1 开始，调整为 0 基索引

                    Channels.Add(new ChannelDebugItem
                    {
                        ChannelId = ch.ChannelId,
                        Name = name,
                        IsEnabled = ch.Enabled,
                        Color = colorCategory.GetColor(colorIndex) // 根据通道 ID 分配颜色
                    });
                }
            }

            foreach (var channel in Channels)
            {
                channel.IsEnabledChanged += OnChannelDebugItemIsEnabledChanged;
            }

            SelectedChannel = Channels.FirstOrDefault();           
        }

        private void OnChannelDebugItemIsEnabledChanged(ChannelDebugItem channel, bool isEnabled)
        {
            if (_device?.CurrentConfig is DataAcquisitionConfig cfg && cfg.Channels != null)
            {
                var configChannel = cfg.Channels.FirstOrDefault(c => c.ChannelId == channel.ChannelId);
                if (configChannel != null)
                {
                    configChannel.Enabled = isEnabled;
                }
            }

            ChannelVisibilityChanged?.Invoke(channel.ChannelId, isEnabled);
        }


        private void SyncChannelCount()
        {
            if (_device?.CurrentConfig is not DataAcquisitionConfig cfg)
                return;

            int count = cfg.Channels?.Count ?? 0;
            if (cfg.ChannelCount != count)
            {
                cfg.ChannelCount = count;
            }

            OnPropertyChanged(nameof(ChannelCount));
        }


        private void OnDataReceived(object sender, DeviceMessage message)
        {
            TotalFramesReceived++;
            TotalBytesReceived += message.Data?.Length ?? 0;
            LastDataTime = message.Timestamp;

            Application.Current?.Dispatcher.Invoke(() =>
            {
                StatusMessage = $"已接收 {TotalFramesReceived} 帧，总计 {TotalBytesReceived / 1024.0:F2} KB";
            });

            // 解析波形数据并分发到视图
            try
            {
                if (_device?.CurrentConfig is not DataAcquisitionConfig cfg)
                    return;

                int frameSampleCountPerChannel = Math.Max(1, cfg.BufferSize);

                if (message.Data == null || message.Data.Length == 0)
                    return;

                // 设备上报数据按“启用通道顺序分段”组织：
                // [ch1(0..N-1), ch2(0..N-1), ...]
                var configuredEnabledChannels = cfg.Channels?.Where(c => c.Enabled).ToList()
                    ?? Enumerable.Range(1, Math.Max(1, cfg.ChannelCount))
                        .Select(i => new Configs.DAQChannelConfig { ChannelId = i, Enabled = true })
                        .ToList();

                int channelCount = configuredEnabledChannels.Count;
                if (channelCount <= 0)
                    return;

                Span<byte> payloadSpan = message.Data;
                Span<float> floatData = MemoryMarshal.Cast<byte, float>(payloadSpan);

                if (floatData.Length < channelCount * frameSampleCountPerChannel)
                    frameSampleCountPerChannel = floatData.Length / channelCount;

                if (frameSampleCountPerChannel <= 0)
                    return;

                var dataByChannel = new Dictionary<int, double[]>();
                int targetCount = DebugSampleCount > 0 ? DebugSampleCount : frameSampleCountPerChannel;

                for (int ch = 0; ch < channelCount; ch++)
                {
                    int channelId = configuredEnabledChannels[ch].ChannelId;

                    // 热路径使用池化数组，避免每帧每通道分配临时 double[]。
                    var rentedFrameSamples = ArrayPool<double>.Shared.Rent(frameSampleCountPerChannel);
                    try
                    {
                        for (int i = 0; i < frameSampleCountPerChannel; i++)
                        {
                            int idx = ch * frameSampleCountPerChannel + i;
                            rentedFrameSamples[i] = idx < floatData.Length ? floatData[idx] : 0d;
                        }

                        lock (_waveformBufferLock)
                        {
                            if (!_waveformSampleBuffers.TryGetValue(channelId, out var channelBuffer))
                            {
                                channelBuffer = new List<double>(Math.Max(targetCount, frameSampleCountPerChannel) * 2);
                                _waveformSampleBuffers[channelId] = channelBuffer;
                            }

                            // List<T>.AddRange 不支持 Span，这里按索引追加，避免再创建新数组。
                            for (int i = 0; i < frameSampleCountPerChannel; i++)
                            {
                                channelBuffer.Add(rentedFrameSamples[i]);
                            }

                            // 只有累计到目标点数后才刷新UI，支持 bufferSize < DebugSampleCount 的场景。
                            if (channelBuffer.Count >= targetCount)
                            {
                                var ys = new double[targetCount];
                                channelBuffer.CopyTo(0, ys, 0, targetCount);
                                channelBuffer.RemoveRange(0, targetCount);
                                dataByChannel[channelId] = ys;
                            }
                        }
                    }
                    finally
                    {
                        ArrayPool<double>.Shared.Return(rentedFrameSamples, clearArray: false);
                    }
                }

                if (dataByChannel.Count > 0)
                {
                    Application.Current?.Dispatcher.Invoke(() =>
                    {
                        WaveformUpdated?.Invoke(dataByChannel);
                    });
                }
            }
            catch (Exception ex)
            {
                AddLogMessage($"解析波形数据失败: {ex.Message}");
            }
        }

        private void OnErrorOccurred(object sender, Exception exception)
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                AddLogMessage($"错误: {exception.Message}");
                StatusMessage = $"错误: {exception.Message}";
            });
        }

        public void UpdateState()
        {
            if (_device != null)
            {
                CurrentState = _device.GetState();

                StatusMessage = CurrentState switch
                {
                    AcquisitionState.Idle => "空闲",
                    AcquisitionState.Running => "运行中",
                    AcquisitionState.Paused => "已暂停",
                    AcquisitionState.Error => "错误",
                    _ => "未知状态"
                };
            }
        }

        public async Task StartMonitoringAsync()
        {
            if (_isMonitoring)
                return;

            _isMonitoring = true;
            _monitoringCts = new CancellationTokenSource();

            await Task.Run(async () =>
            {
                while (!_monitoringCts.Token.IsCancellationRequested)
                {
                    Application.Current?.Dispatcher.Invoke(() =>
                    {
                        UpdateState();
                    });

                    await Task.Delay(1000, _monitoringCts.Token).ConfigureAwait(false);
                }
            }, _monitoringCts.Token).ConfigureAwait(false);
        }

        public void StopMonitoring()
        {
            if (!_isMonitoring)
                return;

            _isMonitoring = false;
            _monitoringCts?.Cancel();
            _monitoringCts?.Dispose();
            _monitoringCts = null;
        }

        public async Task StartAcquisitionAsync()
        {
            if (_device == null)
                return;

            try
            {
                await _device.StartAcquisitionAsync().ConfigureAwait(false);
                Application.Current?.Dispatcher.Invoke(UpdateState);
                AddLogMessage("开始采集");
            }
            catch (Exception ex)
            {
                AddLogMessage($"开始采集失败: {ex.Message}");
            }
        }

        public async Task StopAcquisitionAsync()
        {
            if (_device == null)
                return;

            try
            {
                await _device.StopAcquisitionAsync().ConfigureAwait(false);
                Application.Current?.Dispatcher.Invoke(UpdateState);
                AddLogMessage("停止采集");
            }
            catch (Exception ex)
            {
                AddLogMessage($"停止采集失败: {ex.Message}");
            }
        }

        private async Task ScanSerialNumbersAsync()
        {
            AvailableSerialNumbers.Clear();

            List<string> snList = new List<string>();

            await Task.Run(() =>
            {
                var moduleInfos = BRCSDK.ScanModules();

                snList = moduleInfos.Select(m => m.DeviceId).ToList();
            });
           

            foreach(var sn in snList)
            {
                AvailableSerialNumbers.Add(sn);
            }

            SelectedSerialNumber = AvailableSerialNumbers.FirstOrDefault();

            //if (_device?.CurrentConfig is DataAcquisitionConfig cfg)
            //{
            //    if (!string.IsNullOrWhiteSpace(cfg.SerialNumber))
            //    {
            //        AvailableSerialNumbers.Add(cfg.SerialNumber);
            //        SelectedSerialNumber = cfg.SerialNumber;
            //    }
            //}

            //// 在此处接入真实硬件扫描逻辑，将扫描得到的 SN 添加到 AvailableSerialNumbers 中
            //await Task.CompletedTask;
        }

        public void AddLogMessage(string message)
        {
            var logEntry = $"[{DateTime.Now:HH:mm:ss.fff}] {message}";
            LogMessages.Add(logEntry);

            // 限制日志数量
            if (LogMessages.Count > 1000)
            {
                LogMessages.RemoveAt(0);
            }
        }
    }
}
