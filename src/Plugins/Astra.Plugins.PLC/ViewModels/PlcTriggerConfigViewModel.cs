using System.Collections.ObjectModel;
using System.ComponentModel;
using Astra.Core.Configuration.Abstractions;
using Astra.Plugins.PLC.Configs;
using Astra.Plugins.PLC.Providers;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Astra.Plugins.PLC.ViewModels
{
    public partial class PlcTriggerConfigViewModel : ObservableObject
    {
        [ObservableProperty]
        private PlcTriggerConfig _config;

        public ObservableCollection<string> PlcDeviceNames { get; } = new();

        public ObservableCollection<string> IoPointNames { get; } = new();

        /// <summary>
        /// 单参 <see cref="IConfig"/> 构造（与 IO 配置界面相同），供宿主通过反射创建 ViewModel，不依赖 IConfigurationManager 的 DI 注入。
        /// </summary>
        public PlcTriggerConfigViewModel(IConfig config)
        {
            _config = config as PlcTriggerConfig ?? throw new ArgumentException("配置类型必须为 PlcTriggerConfig", nameof(config));
            _config.PropertyChanged += OnConfigPropertyChanged;
            RefreshPlcDeviceNames();
            RefreshIoPointNames();
        }

        private void OnConfigPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(PlcTriggerConfig.PlcDeviceName))
            {
                RefreshIoPointNames();
            }
        }

        private void RefreshPlcDeviceNames()
        {
            PlcDeviceNames.Clear();
            foreach (var n in PlcDeviceProvider.GetPlcDeviceNames())
            {
                PlcDeviceNames.Add(n);
            }
        }

        private void RefreshIoPointNames()
        {
            IoPointNames.Clear();
            var plc = Config.PlcDeviceName?.Trim() ?? string.Empty;
            foreach (var n in PlcIoProvider.GetIoNamesForPlcDevice(plc))
            {
                IoPointNames.Add(n);
            }
        }
    }
}
