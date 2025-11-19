using Astra.Core.Devices;
using Astra.Plugins.DataAcquisition.Abstractions;
using Astra.Plugins.DataAcquisition.Devices;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace Astra.Plugins.DataAcquisition.ViewModels
{
    public partial class DataAcquisitionDeviceDebugViewModel : ObservableObject
    {
        private DataAcquisitionDevice _device;
        private AcquisitionState _currentState;
        private string _statusMessage = "就绪";
        private long _totalFramesReceived;
        private long _totalBytesReceived;
        private DateTime _lastDataTime;
        private bool _isMonitoring;
        private CancellationTokenSource _monitoringCts;

        public DataAcquisitionDevice Device
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
            set => SetProperty(ref _currentState, value);
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
        public double SampleRate => (_device?.CurrentConfig as DataAcquisitionConfig)?.SampleRate ?? 0.0;
        public int ChannelCount => (_device?.CurrentConfig as DataAcquisitionConfig)?.ChannelCount ?? 0;
        public int BufferSize => (_device?.CurrentConfig as DataAcquisitionConfig)?.BufferSize ?? 0;

        public ObservableCollection<string> LogMessages { get; } = new();

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

        public DataAcquisitionDeviceDebugViewModel()
        {
        }

        public DataAcquisitionDeviceDebugViewModel(DataAcquisitionDevice device)
        {
            Device = device;
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
