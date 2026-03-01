using Astra.Core.Configuration;
using Astra.Core.Devices;
using Astra.Core.Devices.Specifications;
using Astra.Plugins.DataAcquisition.Configs;
using Astra.Plugins.DataAcquisition.Devices;
using Astra.UI.Helpers;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandyControl.Controls;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;

namespace Astra.Plugins.DataAcquisition.ViewModels
{
    public partial class DataAcquisitionChannelConfigViewModel : ObservableObject
    {
        private DataAcquisitionConfig _config;
        private readonly IConfigurationManager _configManager;
        private IDeviceSpecification _deviceSpecification;

        private IReadOnlyList<CouplingMode> _couplingModeOptions;
        private IReadOnlyList<double> _triggerLevelOptions;

        /// <summary>
        /// 规格�?定义的可用耦合方式（AC/DC等）
        /// </summary>
        public IReadOnlyList<CouplingMode> CouplingModeOptions
        {
            get => _couplingModeOptions;
            private set
            {
                if (SetProperty(ref _couplingModeOptions, value))
                {
                    OnPropertyChanged(nameof(HasCouplingModeOptions));
                }
            }
        }

        /// <summary>
        /// 规格�?定义的可用触发电平（单位：mA�?
        /// </summary>
        public IReadOnlyList<double> TriggerLevelOptions
        {
            get => _triggerLevelOptions;
            private set
            {
                if (SetProperty(ref _triggerLevelOptions, value))
                {
                    OnPropertyChanged(nameof(HasTriggerLevelOptions));
                }
            }
        }

        /// <summary>
        /// �?否存在耦合方式约束（用于控制界面显示）
        /// </summary>
        public bool HasCouplingModeOptions => CouplingModeOptions != null && CouplingModeOptions.Count > 0;

        /// <summary>
        /// �?否存在触发电平约束（用于控制界面显示�?
        /// </summary>
        public bool HasTriggerLevelOptions => TriggerLevelOptions != null && TriggerLevelOptions.Count > 0;

        [ObservableProperty]
        private DAQChannelConfig _selectedChannel;

        [ObservableProperty]
        private ObservableCollection<SensorConfig> _availableSensors = new ObservableCollection<SensorConfig>();

        [ObservableProperty]
        private ObservableCollection<SensorAxis> _availableAxes = new ObservableCollection<SensorAxis>
        {
            SensorAxis.X,
            SensorAxis.Y,
            SensorAxis.Z
        };

        /// <summary>
        /// 采样率选项列表（常见采样�?�率，单位：Hz�?
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

        public DataAcquisitionChannelConfigViewModel(DataAcquisitionConfig config)
        {
            _config = config;

            // 初�?�化规格选项
            UpdateSpecificationOptions();

            // 获取配置管理器服�?
            try
            {
                var appType = System.Type.GetType("Astra.App, Astra");

                if (appType != null)
                {
                    var serviceProviderProperty = appType.GetProperty("ServiceProvider", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    var serviceProvider = serviceProviderProperty?.GetValue(null) as IServiceProvider;
                    _configManager = serviceProvider?.GetService<IConfigurationManager>();

                    if (_configManager == null)
                    {
                        System.Diagnostics.Debug.WriteLine("[DataAcquisitionChannelConfigViewModel] 从服务提供者获取配�?管理器失�?");
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("[DataAcquisitionChannelConfigViewModel] 找不到Astra.App类型");
                }
            }
            catch
            {
                // 静默处理获取失败
            }

            InitializeChannels();
            LoadSensorsAsync();

            // 监听配置的属性变�?
            if (_config != null)
            {
                _config.PropertyChanged += Config_PropertyChanged;
            }
        }

        /// <summary>
        /// 更新规格选项（当设�?�型号变化时调用�?
        /// </summary>
        private void UpdateSpecificationOptions()
        {
            // 从�?��?��?�格注册表中获取当前设�?�的规格，用于通道级别的约束选项
            _deviceSpecification = DeviceSpecificationRegistry.GetSpecification(
                DeviceType.DataAcquisition,
                _config?.Manufacturer ?? string.Empty,
                _config?.Model ?? string.Empty
            );

            // 初�?�化耦合方式和触发电平选项（�?�果规格�?有定义）
            IReadOnlyList<CouplingMode> couplingModes = null;
            IReadOnlyList<double> triggerLevels = null;

            if (_deviceSpecification != null)
            {
                var couplingModesList = _deviceSpecification.GetConstraint<List<CouplingMode>>("AllowedCouplingModes", null);
                if (couplingModesList != null)
                {
                    couplingModes = couplingModesList.AsReadOnly();
                }

                var triggerLevelsList = _deviceSpecification.GetConstraint<List<double>>("AllowedTriggerLevels", null);
                if (triggerLevelsList != null)
                {
                    triggerLevels = triggerLevelsList.AsReadOnly();
                }
            }

            // 更新属性（会自动触�? HasCouplingModeOptions �? HasTriggerLevelOptions 的变更通知�?
            CouplingModeOptions = couplingModes;
            TriggerLevelOptions = triggerLevels;
        }

        /// <summary>
        /// 加载传感器列�?
        /// </summary>
        private async void LoadSensorsAsync()
        {
            if (_configManager == null)
            {
                System.Diagnostics.Debug.WriteLine("[DataAcquisitionChannelConfigViewModel] 配置管理器为空，无法加载传感器列�?");
                return;
            }

            try
            {
                System.Diagnostics.Debug.WriteLine("[DataAcquisitionChannelConfigViewModel] 开始加载传感器列表...");
                var result = await _configManager.GetAllAsync<SensorConfig>();

                if (result.Success && result.Data != null)
                {
                    System.Diagnostics.Debug.WriteLine($"[DataAcquisitionChannelConfigViewModel] 成功加载 {result.Data.Count()} �?传感器配�?");

                    // 保存当前通道�?已选择的传感器ID和引用映�?
                    var channelSensorMap = new Dictionary<string, DAQChannelConfig>();
                    if (_config?.Channels != null)
                    {
                        foreach (var channel in _config.Channels)
                        {
                            if (channel?.Sensor != null && !string.IsNullOrEmpty(channel.Sensor.ConfigId))
                            {
                                channelSensorMap[channel.Sensor.ConfigId] = channel;
                               
                            }
                        }
                    }

                    // 创建传感器ID到传感器对象的映�?
                    var sensorIdMap = result.Data.ToDictionary(s => s.ConfigId, s => s);

                    // 在UI线程上更新传感器列表
                    System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                    {
                        // 清空并重新填充传感器列表
                        AvailableSensors.Clear();
                        foreach (var sensor in result.Data.OrderBy(s => s.ConfigName))
                        {
                            AvailableSensors.Add(sensor);
                        }

                        // 恢�?�所有通道的传感器引用（根�?保存�? SensorId 从传感器库中查找�?
                        if (_config != null)
                        {
                            _config.RestoreSensorReferences(AvailableSensors);
                        }

                        System.Diagnostics.Debug.WriteLine($"[DataAcquisitionChannelConfigViewModel] 传感器列表已更新，当前有 {AvailableSensors.Count} �?传感�?");
                    });
                }
                else
                {
                    ToastHelper.ShowError($"[DataAcquisitionChannelConfigViewModel] 加载传感器列表失�?: Success={result.Success}");
                }
            }
            catch (Exception ex)
            {
                ToastHelper.ShowError($"[DataAcquisitionChannelConfigViewModel] 加载传感器列表异�?: {ex.Message}");
            }
        }

        /// <summary>
        /// 初�?�化通道集合
        /// </summary>
        private void InitializeChannels()
        {
            if (_config == null)
                return;

            // 取消之前的监�?
            if (_config.Channels != null)
            {
                _config.Channels.CollectionChanged -= Channels_CollectionChanged;
            }

            // �?保通道集合已初始化
            if (_config.Channels == null)
            {
                _config.Channels = new ObservableCollection<DAQChannelConfig>();
            }

            // 如果通道集合为空�? ChannelCount > 0，根�? ChannelCount 初�?�化通道
            if (_config.Channels.Count == 0 && _config.ChannelCount > 0)
            {
                for (int i = 0; i < _config.ChannelCount; i++)
                {
                    var channel = new DAQChannelConfig
                    {
                        ChannelId = i + 1,
                        ChannelName = $"通道 {i + 1}",
                        SampleRate = _config.SampleRate,
                        Enabled = true
                    };

                    _config.Channels.Add(channel);
                    OnPropertyChanged(nameof(CanDeleteChannel));
                }
            }

            // 监听通道集合变化
            _config.Channels.CollectionChanged += Channels_CollectionChanged;

            // 通知 UI Channels 属性已更新
            OnPropertyChanged(nameof(Channels));
        }

        /// <summary>
        /// 通道集合变化处理
        /// </summary>
        private void Channels_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            // 当集合变化时，强制通知UI刷新
            OnPropertyChanged(nameof(Channels));
            OnPropertyChanged(nameof(CanDeleteChannel));

            // 如果删除操作后集合为空，清除选中状�?
            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Remove)
            {
                if (_config?.Channels?.Count == 0)
                {
                    SelectedChannel = null;
                }
            }
        }

        /// <summary>
        /// 配置属性变化�?�理
        /// </summary>
        private void Config_PropertyChanged(object sender, Core.Devices.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(DataAcquisitionConfig.Channels))
            {
                OnPropertyChanged(nameof(Channels));
            }
            else if (e.PropertyName == nameof(DataAcquisitionConfig.SampleRate))
            {
                // 基�?�配置的采样率变化时，通知 UI 更新通道显示
                // 通道的采样率已由 DataAcquisitionConfig �?动同�?
                OnPropertyChanged(nameof(Channels));
            }
            else if (e.PropertyName == nameof(DataAcquisitionConfig.Manufacturer) || 
                     e.PropertyName == nameof(DataAcquisitionConfig.Model))
            {
                // 当�?��?�厂家或型号变化时，重新从�?�格�?读取选项
                UpdateSpecificationOptions();
            }
        }

        /// <summary>
        /// 配置对象
        /// </summary>
        public DataAcquisitionConfig Config
        {
            get => _config;
            set
            {
                if (_config != null)
                {
                    _config.PropertyChanged -= Config_PropertyChanged;
                }

                if (SetProperty(ref _config, value))
                {
                    if (_config != null)
                    {
                        _config.PropertyChanged += Config_PropertyChanged;
                    }
                    InitializeChannels();
                }
            }
        }

        /// <summary>
        /// 通道配置集合
        /// </summary>
        public ObservableCollection<DAQChannelConfig> Channels => _config?.Channels;

        /// <summary>
        /// �?否可以删除通道
        /// </summary>
        public bool CanDeleteChannel => SelectedChannel != null && Channels?.Count > 0;

        /// <summary>
        /// 添加通道命令
        /// </summary>
        [RelayCommand]
        private void AddChannel()
        {
            if (_config == null)
                return;

            // �?保通道集合已初始化
            if (_config.Channels == null)
            {
                _config.Channels = new ObservableCollection<DAQChannelConfig>();
            }

            // 获取下一�?通道ID（从集合大小+1计算，确保连�?�?
            int nextChannelId = _config.Channels.Count + 1;

            // 创建新通道
            var newChannel = new DAQChannelConfig
            {
                ChannelId = nextChannelId,
                ChannelName = $"通道 {nextChannelId}",
                SampleRate = _config.SampleRate,
                Enabled = true
            };

            _config.Channels.Add(newChannel);
            SelectedChannel = newChannel;

            // 通知 UI 更新
            OnPropertyChanged(nameof(Channels));
            OnPropertyChanged(nameof(CanDeleteChannel));
        }

        /// <summary>
        /// 删除通道命令
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanDeleteChannel))]
        private void DeleteChannel(DAQChannelConfig channel)
        {
            if (_config == null || _config.Channels == null)
            {
                System.Diagnostics.Debug.WriteLine("[DataAcquisitionChannelConfigViewModel] 删除通道失败: Config或Channels为null");
                return;
            }

            var channelToDelete = channel ?? SelectedChannel;
            if (channelToDelete == null)
            {
                System.Diagnostics.Debug.WriteLine("[DataAcquisitionChannelConfigViewModel] 删除通道失败: 没有指定要删除的通道");
                return;
            }

            System.Diagnostics.Debug.WriteLine($"[DataAcquisitionChannelConfigViewModel] 开始删除通道: ChannelId={channelToDelete.ChannelId}, ChannelName={channelToDelete.ChannelName}");

            // 保存删除前的选中状�?
            bool wasSelected = SelectedChannel == channelToDelete;

            // 从集合中删除（ObservableCollection会自动触发CollectionChanged事件�?
            bool removed = _config.Channels.Remove(channelToDelete);

            if (!removed)
            {
                System.Diagnostics.Debug.WriteLine("[DataAcquisitionChannelConfigViewModel] 删除通道失败: 从集合中移除失败");
                return; // 如果删除失败，直接返�?
            }

            System.Diagnostics.Debug.WriteLine($"[DataAcquisitionChannelConfigViewModel] 通道已从集合�?删除，剩余通道�?: {_config.Channels.Count}");

            // 如果删除的是当前选中的通道，清除选中状�?
            if (wasSelected)
            {
                SelectedChannel = null;
            }

            // 重新编号所有通道
            for (int i = 0; i < _config.Channels.Count; i++)
            {
                _config.Channels[i].ChannelId = i + 1;
                // 如果通道名称�?默�?�格式（通道 X），则更新名�?
                if (string.IsNullOrWhiteSpace(_config.Channels[i].ChannelName) ||
                    _config.Channels[i].ChannelName.StartsWith("通道 "))
                {
                    _config.Channels[i].ChannelName = $"通道 {i + 1}";
                }
            }

            // 强制通知 UI 更新（确保ItemsControl刷新�?
            // 注意：由于ObservableCollection的CollectionChanged事件已经触发�?
            // Channels_CollectionChanged方法会�?�理通知，但这里再�?�通知�?保UI刷新
            OnPropertyChanged(nameof(Channels));
            OnPropertyChanged(nameof(CanDeleteChannel));

            // 通知命令�?执�?�状态变�?
            DeleteChannelCommand.NotifyCanExecuteChanged();

            // 如果还有通道且之前选中的通道�?删除了，选中�?一�?通道
            if (_config.Channels.Count > 0 && wasSelected)
            {
                SelectedChannel = _config.Channels[0];
            }

            System.Diagnostics.Debug.WriteLine($"[DataAcquisitionChannelConfigViewModel] 通道删除完成，当前通道�?: {_config.Channels.Count}");
        }

        /// <summary>
        /// 清除传感器绑定命�?
        /// </summary>
        [RelayCommand]
        private void ClearSensor(DAQChannelConfig channel)
        {
            if (channel == null)
                return;

            channel.ClearSensor();
        }

        /// <summary>
        /// 打开传感器配�?命令
        /// </summary>
        [RelayCommand]
        private void OpenSensorConfig(SensorConfig sensor)
        {
            if (sensor == null)
                return;

            try
            {
                // 尝试获取配置管理器服�?
                var appType = System.Type.GetType("Astra.App, Astra");
                if (appType != null)
                {
                    var serviceProviderProperty = appType.GetProperty("ServiceProvider", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    var serviceProvider = serviceProviderProperty?.GetValue(null) as IServiceProvider;

                    // 尝试获取配置视图模型服务来打开传感器配�?
                    // 这里�?以根�?实际系统的�?�航方式来实�?
                    // 例�?�：通过消息总线、事件或服务来打开配置窗口
                    System.Diagnostics.Debug.WriteLine($"[DataAcquisitionChannelConfigViewModel] 打开传感器配�?: {sensor.ConfigName} (ID: {sensor.ConfigId})");

                    // TODO: 根据实际系统架构实现打开传感器配�?界面的逻辑
                    // 例�?�：
                    // - 使用消息总线发送打开配置的消�?
                    // - 使用导航服务打开配置窗口
                    // - 使用对话框服务显示配�?对话�?
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DataAcquisitionChannelConfigViewModel] 打开传感器配�?失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 选中通道变化时的处理
        /// </summary>
        partial void OnSelectedChannelChanged(DAQChannelConfig value)
        {
            OnPropertyChanged(nameof(CanDeleteChannel));
            // 通知删除命令的可执�?�状态变�?
            DeleteChannelCommand?.NotifyCanExecuteChanged();
        }
    }
}
