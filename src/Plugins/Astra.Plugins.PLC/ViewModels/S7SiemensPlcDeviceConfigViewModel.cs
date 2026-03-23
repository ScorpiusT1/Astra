using Astra.Core.Configuration.Abstractions;
using Astra.Plugins.PLC.Configs;
using System;

namespace Astra.Plugins.PLC.ViewModels
{
    public class S7SiemensPlcDeviceConfigViewModel : PlcDeviceConfigViewModel
    {
        private readonly S7SiemensPlcDeviceConfig _s7Config;

        public S7SiemensPlcDeviceConfigViewModel(IConfig config) : base(config)
        {
            _s7Config = config as S7SiemensPlcDeviceConfig ?? throw new ArgumentException("配置类型必须为 S7SiemensPlcDeviceConfig", nameof(config));
        }

        public ushort Rack
        {
            get => _s7Config.Rack;
            set
            {
                if (_s7Config.Rack != value)
                {
                    _s7Config.Rack = value;
                    OnPropertyChanged();
                }
            }
        }

        public ushort Slot
        {
            get => _s7Config.Slot;
            set
            {
                if (_s7Config.Slot != value)
                {
                    _s7Config.Slot = value;
                    OnPropertyChanged();
                }
            }
        }
    }
}
