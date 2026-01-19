using Astra.Core.Configuration;
using Astra.Core.Devices;
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

        public DataAcquisitionChannelConfigViewModel(DataAcquisitionConfig config)
        {
            _config = config;

            // 获取配置管理器服务
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
                        System.Diagnostics.Debug.WriteLine("[DataAcquisitionChannelConfigViewModel] 从服务提供者获取配置管理器失败");
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

            // 监听配置的 Channels 属性变化
            if (_config != null)
            {
                _config.PropertyChanged += Config_PropertyChanged;
            }
        }

        /// <summary>
        /// 加载传感器列表
        /// </summary>
        private async void LoadSensorsAsync()
        {
            if (_configManager == null)
            {
                System.Diagnostics.Debug.WriteLine("[DataAcquisitionChannelConfigViewModel] 配置管理器为空，无法加载传感器列表");
                return;
            }

            try
            {
                System.Diagnostics.Debug.WriteLine("[DataAcquisitionChannelConfigViewModel] 开始加载传感器列表...");
                var result = await _configManager.GetAllConfigsAsync<SensorConfig>();

                if (result.Success && result.Data != null)
                {
                    System.Diagnostics.Debug.WriteLine($"[DataAcquisitionChannelConfigViewModel] 成功加载 {result.Data.Count()} 个传感器配置");

                    // 保存当前通道中已选择的传感器ID和引用映射
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

                    // 创建传感器ID到传感器对象的映射
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

                        // 恢复所有通道的传感器引用（根据保存的 SensorId 从传感器库中查找）
                        if (_config != null)
                        {
                            _config.RestoreSensorReferences(AvailableSensors);
                        }

                        System.Diagnostics.Debug.WriteLine($"[DataAcquisitionChannelConfigViewModel] 传感器列表已更新，当前有 {AvailableSensors.Count} 个传感器");
                    });
                }
                else
                {
                    ToastHelper.ShowError($"[DataAcquisitionChannelConfigViewModel] 加载传感器列表失败: Success={result.Success}");
                }
            }
            catch (Exception ex)
            {
                ToastHelper.ShowError($"[DataAcquisitionChannelConfigViewModel] 加载传感器列表异常: {ex.Message}");
            }
        }

        /// <summary>
        /// 初始化通道集合
        /// </summary>
        private void InitializeChannels()
        {
            if (_config == null)
                return;

            // 取消之前的监听
            if (_config.Channels != null)
            {
                _config.Channels.CollectionChanged -= Channels_CollectionChanged;
            }

            // 确保通道集合已初始化
            if (_config.Channels == null)
            {
                _config.Channels = new ObservableCollection<DAQChannelConfig>();
            }

            // 如果通道集合为空但 ChannelCount > 0，根据 ChannelCount 初始化通道
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

            // 如果删除操作后集合为空，清除选中状态
            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Remove)
            {
                if (_config?.Channels?.Count == 0)
                {
                    SelectedChannel = null;
                }
            }
        }

        /// <summary>
        /// 配置属性变化处理
        /// </summary>
        private void Config_PropertyChanged(object sender, Core.Devices.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(DataAcquisitionConfig.Channels))
            {
                OnPropertyChanged(nameof(Channels));
            }
            else if (e.PropertyName == nameof(DataAcquisitionConfig.SampleRate))
            {
                // 基础配置的采样率变化时，通知 UI 更新通道显示
                // 通道的采样率已由 DataAcquisitionConfig 自动同步
                OnPropertyChanged(nameof(Channels));
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
        /// 是否可以删除通道
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

            // 确保通道集合已初始化
            if (_config.Channels == null)
            {
                _config.Channels = new ObservableCollection<DAQChannelConfig>();
            }

            // 获取下一个通道ID（从集合大小+1计算，确保连续）
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

            // 保存删除前的选中状态
            bool wasSelected = SelectedChannel == channelToDelete;

            // 从集合中删除（ObservableCollection会自动触发CollectionChanged事件）
            bool removed = _config.Channels.Remove(channelToDelete);

            if (!removed)
            {
                System.Diagnostics.Debug.WriteLine("[DataAcquisitionChannelConfigViewModel] 删除通道失败: 从集合中移除失败");
                return; // 如果删除失败，直接返回
            }

            System.Diagnostics.Debug.WriteLine($"[DataAcquisitionChannelConfigViewModel] 通道已从集合中删除，剩余通道数: {_config.Channels.Count}");

            // 如果删除的是当前选中的通道，清除选中状态
            if (wasSelected)
            {
                SelectedChannel = null;
            }

            // 重新编号所有通道
            for (int i = 0; i < _config.Channels.Count; i++)
            {
                _config.Channels[i].ChannelId = i + 1;
                // 如果通道名称是默认格式（通道 X），则更新名称
                if (string.IsNullOrWhiteSpace(_config.Channels[i].ChannelName) ||
                    _config.Channels[i].ChannelName.StartsWith("通道 "))
                {
                    _config.Channels[i].ChannelName = $"通道 {i + 1}";
                }
            }

            // 强制通知 UI 更新（确保ItemsControl刷新）
            // 注意：由于ObservableCollection的CollectionChanged事件已经触发，
            // Channels_CollectionChanged方法会处理通知，但这里再次通知确保UI刷新
            OnPropertyChanged(nameof(Channels));
            OnPropertyChanged(nameof(CanDeleteChannel));

            // 通知命令可执行状态变化
            DeleteChannelCommand.NotifyCanExecuteChanged();

            // 如果还有通道且之前选中的通道被删除了，选中第一个通道
            if (_config.Channels.Count > 0 && wasSelected)
            {
                SelectedChannel = _config.Channels[0];
            }

            System.Diagnostics.Debug.WriteLine($"[DataAcquisitionChannelConfigViewModel] 通道删除完成，当前通道数: {_config.Channels.Count}");
        }

        /// <summary>
        /// 清除传感器绑定命令
        /// </summary>
        [RelayCommand]
        private void ClearSensor(DAQChannelConfig channel)
        {
            if (channel == null)
                return;

            channel.ClearSensor();
        }

        /// <summary>
        /// 打开传感器配置命令
        /// </summary>
        [RelayCommand]
        private void OpenSensorConfig(SensorConfig sensor)
        {
            if (sensor == null)
                return;

            try
            {
                // 尝试获取配置管理器服务
                var appType = System.Type.GetType("Astra.App, Astra");
                if (appType != null)
                {
                    var serviceProviderProperty = appType.GetProperty("ServiceProvider", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    var serviceProvider = serviceProviderProperty?.GetValue(null) as IServiceProvider;

                    // 尝试获取配置视图模型服务来打开传感器配置
                    // 这里可以根据实际系统的导航方式来实现
                    // 例如：通过消息总线、事件或服务来打开配置窗口
                    System.Diagnostics.Debug.WriteLine($"[DataAcquisitionChannelConfigViewModel] 打开传感器配置: {sensor.ConfigName} (ID: {sensor.SensorId})");

                    // TODO: 根据实际系统架构实现打开传感器配置界面的逻辑
                    // 例如：
                    // - 使用消息总线发送打开配置的消息
                    // - 使用导航服务打开配置窗口
                    // - 使用对话框服务显示配置对话框
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DataAcquisitionChannelConfigViewModel] 打开传感器配置失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 选中通道变化时的处理
        /// </summary>
        partial void OnSelectedChannelChanged(DAQChannelConfig value)
        {
            OnPropertyChanged(nameof(CanDeleteChannel));
            // 通知删除命令的可执行状态变化
            DeleteChannelCommand?.NotifyCanExecuteChanged();
        }
    }
}
