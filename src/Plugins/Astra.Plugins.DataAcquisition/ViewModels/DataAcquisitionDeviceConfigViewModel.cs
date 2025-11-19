using Astra.Core.Devices.Configuration;
using Astra.Plugins.DataAcquisition.Devices;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;

namespace Astra.Plugins.DataAcquisition.ViewModels
{
    public class DataAcquisitionDeviceConfigViewModel : ObservableObject
    {
        private DataAcquisitionConfig _config;

        /// <summary>
        /// 采样率选项列表（常见采样频率，单位：Hz）
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
        /// 通道数量选项列表
        /// </summary>
        public IReadOnlyList<int> ChannelCountOptions { get; } = new List<int>
        {
            2, 4, 8, 12, 16,20, 24, 28,32
        };

        public DataAcquisitionConfig Config
        {
            get => _config;
            set
            {
                if (SetProperty(ref _config, value))
                {
                    OnConfigChanged();
                }
            }
        }

        public string DeviceName
        {
            get => _config?.DeviceName ?? string.Empty;
            set
            {
                if (_config != null && _config.DeviceName != value)
                {
                    _config.DeviceName = value;
                    OnPropertyChanged();
                }
            }
        }

        public string SerialNumber
        {
            get => _config?.SerialNumber ?? string.Empty;
            set
            {
                if (_config != null && _config.SerialNumber != value)
                {
                    _config.SerialNumber = value;
                    OnPropertyChanged();
                }
            }
        }

        public double SampleRate
        {
            get => _config?.SampleRate ?? 51200.0;
            set
            {
                if (_config != null && Math.Abs(_config.SampleRate - value) > double.Epsilon)
                {
                    _config.SampleRate = value;
                    OnPropertyChanged();
                }
            }
        }

        public int ChannelCount
        {
            get => _config?.ChannelCount ?? 8;
            set
            {
                if (_config != null && _config.ChannelCount != value)
                {
                    _config.ChannelCount = value;
                    OnPropertyChanged();
                }
            }
        }

        public int BufferSize
        {
            get => _config?.BufferSize ?? 8192;
            set
            {
                if (_config != null && _config.BufferSize != value)
                {
                    _config.BufferSize = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool AutoStart
        {
            get => _config?.AutoStart ?? true;
            set
            {
                if (_config != null && _config.AutoStart != value)
                {
                    _config.AutoStart = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsEnabled
        {
            get => _config?.IsEnabled ?? true;
            set
            {
                if (_config != null && _config.IsEnabled != value)
                {
                    _config.IsEnabled = value;
                    OnPropertyChanged();
                }
            }
        }

        public string GroupId
        {
            get => _config?.GroupId ?? "G0";
            set
            {
                if (_config != null && _config.GroupId != value)
                {
                    _config.GroupId = value;
                    OnPropertyChanged();
                }
            }
        }

        public string SlotId
        {
            get => _config?.SlotId ?? "S0";
            set
            {
                if (_config != null && _config.SlotId != value)
                {
                    _config.SlotId = value;
                    OnPropertyChanged();
                }
            }
        }

        public string DeviceId => _config?.DeviceId ?? string.Empty;

        public DataAcquisitionDeviceConfigViewModel(DataAcquisitionConfig config)
        {
            Config = config;
        }

        private void OnConfigChanged()
        {
            if (_config != null)
            {
                // 订阅配置变更事件
                _config.PropertyChanged += (sender, e) =>
                {
                    OnPropertyChanged(e.PropertyName);
                };
            }

            OnPropertyChanged(nameof(DeviceName));
            OnPropertyChanged(nameof(SerialNumber));
            OnPropertyChanged(nameof(SampleRate));
            OnPropertyChanged(nameof(ChannelCount));
            OnPropertyChanged(nameof(BufferSize));
            OnPropertyChanged(nameof(AutoStart));
            OnPropertyChanged(nameof(IsEnabled));
            OnPropertyChanged(nameof(GroupId));
            OnPropertyChanged(nameof(SlotId));
            OnPropertyChanged(nameof(DeviceId));
        }
    }
}

