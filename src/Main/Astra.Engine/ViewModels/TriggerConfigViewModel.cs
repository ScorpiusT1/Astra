using Astra.Core.Triggers.Enums;
using Astra.Engine.Triggers;
using Astra.Core.Configuration.Abstractions;
using CommunityToolkit.Mvvm.ComponentModel;
using System.ComponentModel;

namespace Astra.Engine.ViewModels
{
    public partial class TriggerConfigViewModel : ObservableObject
    {
        private readonly IConfigurationManager? _configurationManager;

        [ObservableProperty]
        private TriggerConfig _config;

        public TriggerConfigViewModel(TriggerConfig config, IConfigurationManager configurationManager)
        {
            _config = config ?? new TriggerConfig();
            _configurationManager = configurationManager;

            _config.PropertyChanged += OnConfigPropertyChanged;
        }

        public TriggerSource[] TriggerSources => (TriggerSource[])System.Enum.GetValues(typeof(TriggerSource));

        public WorkMode[] WorkModes => (WorkMode[])System.Enum.GetValues(typeof(WorkMode));

        /// <summary>
        /// 是否为 PLC 监控触发源，用于界面显示 PLC 相关配置区域。
        /// </summary>
        public bool IsPlcSource => Config?.Source == TriggerSource.PLCMonitor;

        private void OnConfigPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(TriggerConfig.Source))
            {
                OnPropertyChanged(nameof(IsPlcSource));
            }
        }
    }
}

