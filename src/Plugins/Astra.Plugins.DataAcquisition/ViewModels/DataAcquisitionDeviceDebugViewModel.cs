using Astra.Core.Devices;
using Astra.Core.Devices.Interfaces;
using Astra.Plugins.DataAcquisition.Abstractions;
using Astra.Plugins.DataAcquisition.Devices;
using Astra.Plugins.DataAcquisition.SDKs;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
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

        // 通道调试信息（是否启用、名称等）
        public partial class ChannelDebugItem : ObservableObject
        {
            [ObservableProperty]
            private int _channelId;

            [ObservableProperty]
            private string _name;

            [ObservableProperty]
            private bool _isEnabled;
        }

        public ObservableCollection<ChannelDebugItem> Channels { get; } = new();

        // 波形数据更新事件，由视图订阅以刷新 ScottPlot
        public event Action<Dictionary<int, double[]>> WaveformUpdated;

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

            if (_device?.CurrentConfig is DataAcquisitionConfig cfg && cfg.Channels != null)
            {
                int index = 0;
                foreach (var ch in cfg.Channels)
                {
                    index++;

                    // 默认名来自配置；如果为空或是“通道 X”这种通用前缀，则根据顺序强制生成“通道 1、通道 2 ...”
                    string name;
                    if (string.IsNullOrWhiteSpace(ch.ChannelName))
                    {
                        name = $"通道 {index}";
                    }
                    //else if (ch.ChannelName.StartsWith("通道 "))
                    //{
                    //    name = $"通道 {index}";
                    //}
                    else
                    {
                        name = ch.ChannelName;
                    }

                    Channels.Add(new ChannelDebugItem
                    {
                        ChannelId = ch.ChannelId,
                        Name = name,
                        IsEnabled = ch.Enabled
                    });
                }
            }
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

                int channelCount = Math.Max(1, cfg.ChannelCount);
                int bufferSize = Math.Max(1, cfg.BufferSize);

                if (message.Data == null || message.Data.Length == 0)
                    return;

                var enabledChannels = Channels
                    .Where(c => c.IsEnabled)
                    .Select(c => c.ChannelId)
                    .ToHashSet();

                if (enabledChannels.Count == 0)
                    return;

                Span<byte> payloadSpan = message.Data;
                Span<float> floatData = MemoryMarshal.Cast<byte, float>(payloadSpan);

                if (floatData.Length < channelCount * bufferSize)
                    bufferSize = floatData.Length / channelCount;

                var dataByChannel = new Dictionary<int, double[]>();

                for (int ch = 0; ch < channelCount; ch++)
                {
                    int channelId = ch + 1; // 通道 ID 从 1 开始
                    if (!enabledChannels.Contains(channelId))
                        continue;

                    // 实际用于显示的采样点数：默认等于 bufferSize，可被 DebugSampleCount 限制
                    int effectiveCount = bufferSize;
                    if (DebugSampleCount > 0)
                        effectiveCount = Math.Min(bufferSize, DebugSampleCount);

                    var ys = new double[effectiveCount];
                    for (int i = 0; i < effectiveCount; i++)
                    {
                        int idx = i + ch * effectiveCount;
                        if (idx < floatData.Length)
                            ys[i] = floatData[idx];
                    }

                    dataByChannel[channelId] = ys;
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
