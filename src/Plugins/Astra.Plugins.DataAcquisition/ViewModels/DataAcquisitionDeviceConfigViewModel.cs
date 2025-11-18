using Astra.Core.Devices.Configuration;
using Astra.Plugins.DataAcquisition.Devices;
using CommunityToolkit.Mvvm.ComponentModel;
using System;

namespace Astra.Plugins.DataAcquisition.ViewModels
{
    public class DataAcquisitionDeviceConfigViewModel : ObservableObject
    {
        private DataAcquisitionConfig _config;

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

        public int SampleRate
        {
            get => _config?.SampleRate ?? 51200;
            set
            {
                if (_config != null && _config.SampleRate != value)
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

