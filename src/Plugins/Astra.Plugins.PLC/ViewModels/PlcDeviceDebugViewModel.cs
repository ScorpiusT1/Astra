using Astra.Contract.Communication.Abstractions;
using Astra.Core.Devices.Interfaces;
using Astra.Core.Foundation.Common;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Astra.Plugins.PLC.ViewModels
{
    public class PlcDeviceDebugViewModel : ObservableObject
    {
        private readonly IDevice _device;
        private readonly IPLC _plc;

        private string _address = "DB1.DBX0.0";
        private string _readValue = string.Empty;
        private string _writeValue = "0";
        private string _status = "就绪";
        private string _readStatus = "读取未执行";
        private string _writeStatus = "写入未执行";

        public PlcDeviceDebugViewModel(IDevice device)
        {
            _device = device ?? throw new ArgumentNullException(nameof(device));
            _plc = device as IPLC ?? throw new ArgumentException("设备未实现 IPLC 接口", nameof(device));

            ConnectCommand = new AsyncRelayCommand(ConnectAsync);
            DisconnectCommand = new AsyncRelayCommand(DisconnectAsync);
            ReadCommand = new AsyncRelayCommand(ReadAsync);
            WriteCommand = new AsyncRelayCommand(WriteAsync);
        }

        public string DeviceName => _device.DeviceName;
        public string DeviceId => _device.DeviceId;
        public string Protocol => _plc.Protocol;
        public bool IsOnline => _device.IsOnline;

        public string Address
        {
            get => _address;
            set
            {
                if (_address != value)
                {
                    _address = value;
                    OnPropertyChanged();
                }
            }
        }

        public string ReadValue
        {
            get => _readValue;
            set
            {
                if (_readValue != value)
                {
                    _readValue = value;
                    OnPropertyChanged();
                }
            }
        }

        public string WriteValue
        {
            get => _writeValue;
            set
            {
                if (_writeValue != value)
                {
                    _writeValue = value;
                    OnPropertyChanged();
                }
            }
        }

        public string Status
        {
            get => _status;
            set
            {
                if (_status != value)
                {
                    _status = value;
                    OnPropertyChanged();
                }
            }
        }

        public string ReadStatus
        {
            get => _readStatus;
            set
            {
                if (_readStatus != value)
                {
                    _readStatus = value;
                    OnPropertyChanged();
                }
            }
        }

        public string WriteStatus
        {
            get => _writeStatus;
            set
            {
                if (_writeStatus != value)
                {
                    _writeStatus = value;
                    OnPropertyChanged();
                }
            }
        }

        public ObservableCollection<string> Logs { get; } = new();

        public ICommand ConnectCommand { get; }
        public ICommand DisconnectCommand { get; }
        public ICommand ReadCommand { get; }
        public ICommand WriteCommand { get; }

        private async Task ConnectAsync()
        {
            var result = await _device.ConnectAsync();
            SetStatus(result, "连接");
            OnPropertyChanged(nameof(IsOnline));
        }

        private async Task DisconnectAsync()
        {
            var result = await _device.DisconnectAsync();
            SetStatus(result, "断开");
            OnPropertyChanged(nameof(IsOnline));
        }

        private Task ReadAsync()
        {
            var result = _plc.Read<object>(Address);
            if (result.Success)
            {
                ReadValue = result.Data?.ToString() ?? string.Empty;
            }

            ReadStatus = result.Success
                ? $"读取 {Address} 成功"
                : $"读取 {Address} 失败: {result.ErrorMessage}";
            AddLog(ReadStatus);
            return Task.CompletedTask;
        }

        private Task WriteAsync()
        {
            var value = ParseWriteValue(WriteValue);
            var result = _plc.Write(Address, value);
            WriteStatus = result.Success
                ? $"写入 {Address} 成功"
                : $"写入 {Address} 失败: {result.ErrorMessage}";
            AddLog(WriteStatus);
            return Task.CompletedTask;
        }

        private object ParseWriteValue(string raw)
        {
            if (bool.TryParse(raw, out var b)) return b;
            if (int.TryParse(raw, out var i)) return i;
            if (double.TryParse(raw, out var d)) return d;
            return raw;
        }

        private void SetStatus(OperationResult result, string action)
        {
            Status = result.Success ? $"{action}成功" : $"{action}失败: {result.ErrorMessage}";
            AddLog(Status);
        }

        private void AddLog(string message)
        {
            Logs.Add($"[{DateTime.Now:HH:mm:ss}] {message}");
            if (Logs.Count > 300)
            {
                Logs.RemoveAt(0);
            }
        }
    }
}
