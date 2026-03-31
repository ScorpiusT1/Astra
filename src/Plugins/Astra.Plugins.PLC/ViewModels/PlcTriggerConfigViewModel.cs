using System;
using System.ComponentModel;
using Astra.Core.Configuration.Abstractions;
using Astra.Plugins.PLC;
using Astra.Plugins.PLC.Configs;
using Astra.Plugins.PLC.Providers;
using Astra.UI.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Astra.Plugins.PLC.ViewModels
{
    public partial class PlcTriggerConfigViewModel : ObservableObject, IDisposable
    {
        [ObservableProperty]
        private PlcTriggerConfig _config;

        private readonly LinkedDropdownState<PlcDeviceConfig> _plcLinked;
        private readonly LinkedDropdownState<IOConfig> _ioLinked;

        public DropdownState PlcDeviceDropdown => _plcLinked.Dropdown;
        public DropdownState IoPointDropdown => _ioLinked.Dropdown;

        /// <summary>
        /// 单参 <see cref="IConfig"/> 构造，供宿主通过反射创建 ViewModel。
        /// </summary>
        public PlcTriggerConfigViewModel(IConfig config)
        {
            _config = config as PlcTriggerConfig
                ?? throw new ArgumentException("配置类型必须为 PlcTriggerConfig", nameof(config));

            var mgr = PlcPlugin.GetConfigurationManager()
                ?? throw new InvalidOperationException("IConfigurationManager 未初始化");

            _plcLinked = new LinkedDropdownState<PlcDeviceConfig>(
                configManager: mgr,
                getCurrentValue: () => Config.PlcDeviceName?.Trim() ?? string.Empty,
                fetchItemsAsync: async () => await PlcDeviceProvider.GetPlcDeviceNamesAsync(),
                onValueChanged: v =>
                {
                    var value = v ?? string.Empty;
                    if (!string.Equals(Config.PlcDeviceName, value, StringComparison.Ordinal))
                        Config.PlcDeviceName = value;
                    // PLC 设备变更时级联刷新 IO 列表
                    _ = _ioLinked.TriggerRefreshAsync();
                });

            _ioLinked = new LinkedDropdownState<IOConfig>(
                configManager: mgr,
                getCurrentValue: () => Config.IoPointName?.Trim() ?? string.Empty,
                fetchItemsAsync: async () =>
                    await PlcIoProvider.GetIoNamesForPlcDeviceAsync(Config.PlcDeviceName?.Trim() ?? string.Empty),
                onValueChanged: v =>
                {
                    var value = v ?? string.Empty;
                    if (!string.Equals(Config.IoPointName, value, StringComparison.Ordinal))
                        Config.IoPointName = value;
                });

            _config.PropertyChanged += OnConfigPropertyChanged;

            _ = _plcLinked.TriggerRefreshAsync();
            _ = _ioLinked.TriggerRefreshAsync();
        }

        private void OnConfigPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // Config 被外部修改时（如加载配置）同步回显到 UI，并级联刷新 IO 列表
            if (e.PropertyName == nameof(PlcTriggerConfig.PlcDeviceName))
            {
                PlcDeviceDropdown.ApplySelection(Config.PlcDeviceName);
                _ = _ioLinked.TriggerRefreshAsync();
            }

            if (e.PropertyName == nameof(PlcTriggerConfig.IoPointName))
            {
                IoPointDropdown.ApplySelection(Config.IoPointName);
            }
        }

        public void Dispose()
        {
            _config.PropertyChanged -= OnConfigPropertyChanged;
            _plcLinked.Dispose();
            _ioLinked.Dispose();
        }
    }
}
