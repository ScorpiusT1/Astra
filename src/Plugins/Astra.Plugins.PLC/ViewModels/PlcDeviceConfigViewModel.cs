using Astra.Core.Configuration.Abstractions;
using Astra.Plugins.PLC.Configs;
using CommunityToolkit.Mvvm.ComponentModel;
using System;

namespace Astra.Plugins.PLC.ViewModels
{
    public class PlcDeviceConfigViewModel : ObservableObject
    {
        protected readonly PlcDeviceConfig _config;

        public PlcDeviceConfigViewModel(IConfig config)
        {
            _config = config as PlcDeviceConfig ?? throw new ArgumentException("配置类型必须为 PlcDeviceConfig", nameof(config));
        }

        public string ConfigName
        {
            get => _config.ConfigName ?? string.Empty;
            set
            {
                if (_config.ConfigName != value)
                {
                    _config.ConfigName = value;
                    OnPropertyChanged();
                }
            }
        }

        public string Manufacturer
        {
            get => _config.Manufacturer ?? string.Empty;
            set
            {
                if (_config.Manufacturer != value)
                {
                    _config.Manufacturer = value;
                    OnPropertyChanged();
                }
            }
        }

        public string Model
        {
            get => _config.Model ?? string.Empty;
            set
            {
                if (_config.Model != value)
                {
                    _config.Model = value;
                    OnPropertyChanged();
                }
            }
        }

        public string SerialNumber
        {
            get => _config.SerialNumber ?? string.Empty;
            set
            {
                if (_config.SerialNumber != value)
                {
                    _config.SerialNumber = value;
                    OnPropertyChanged();
                }
            }
        }

        public string Ip
        {
            get => _config.Ip ?? string.Empty;
            set
            {
                if (_config.Ip != value)
                {
                    _config.Ip = value;
                    OnPropertyChanged();
                }
            }
        }

        public ushort Port
        {
            get => _config.Port;
            set
            {
                if (_config.Port != value)
                {
                    _config.Port = value;
                    OnPropertyChanged();
                }
            }
        }

        public int ConnectTimeoutMs
        {
            get => _config.ConnectTimeoutMs;
            set
            {
                if (_config.ConnectTimeoutMs != value)
                {
                    _config.ConnectTimeoutMs = value;
                    OnPropertyChanged();
                }
            }
        }

        public int ReadWriteTimeoutMs
        {
            get => _config.ReadWriteTimeoutMs;
            set
            {
                if (_config.ReadWriteTimeoutMs != value)
                {
                    _config.ReadWriteTimeoutMs = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool AutoReconnect
        {
            get => _config.AutoReconnect;
            set
            {
                if (_config.AutoReconnect != value)
                {
                    _config.AutoReconnect = value;
                    OnPropertyChanged();
                }
            }
        }

        public int ReconnectIntervalMs
        {
            get => _config.ReconnectIntervalMs;
            set
            {
                if (_config.ReconnectIntervalMs != value)
                {
                    _config.ReconnectIntervalMs = value;
                    OnPropertyChanged();
                }
            }
        }

        public string GroupId
        {
            get => _config.GroupId ?? string.Empty;
            set
            {
                if (_config.GroupId != value)
                {
                    _config.GroupId = value;
                    OnPropertyChanged();
                }
            }
        }

        public string SlotId
        {
            get => _config.SlotId ?? string.Empty;
            set
            {
                if (_config.SlotId != value)
                {
                    _config.SlotId = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsEnabled
        {
            get => _config.IsEnabled;
            set
            {
                if (_config.IsEnabled != value)
                {
                    _config.IsEnabled = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsS7Device => _config is S7SiemensPlcDeviceConfig;

        public ushort Rack
        {
            get => _config is S7SiemensPlcDeviceConfig s7 ? s7.Rack : (ushort)0;
            set
            {
                if (_config is S7SiemensPlcDeviceConfig s7 && s7.Rack != value)
                {
                    s7.Rack = value;
                    OnPropertyChanged();
                }
            }
        }

        public ushort Slot
        {
            get => _config is S7SiemensPlcDeviceConfig s7 ? s7.Slot : (ushort)1;
            set
            {
                if (_config is S7SiemensPlcDeviceConfig s7 && s7.Slot != value)
                {
                    s7.Slot = value;
                    OnPropertyChanged();
                }
            }
        }

    }
}
