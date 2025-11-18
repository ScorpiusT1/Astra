using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Astra.Core.Devices.Attributes;
using Astra.Core.Devices.Configuration;
using Astra.Core.Devices.Management;
using Astra.Core.Plugins.Abstractions;
using Astra.Core.Plugins.Host;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;

namespace Astra.ViewModels
{
    public partial class ConfigViewModel : ObservableObject
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IDeviceManager _deviceManager;
        private readonly IPluginHost _pluginHost;
        private ContentControl _configContentRegion;

        [ObservableProperty]
        private string _title = "配置管理";

        [ObservableProperty]
        private ObservableCollection<TreeNodeViewModel> _treeNodes = new();

        [ObservableProperty]
        private TreeNodeViewModel _selectedNode;

        // 设备配置类型信息缓存
        private readonly Dictionary<Type, DeviceConfigInfo> _deviceConfigTypes = new();

        public ConfigViewModel()
        {
            // 从服务提供者获取依赖
            _serviceProvider = App.ServiceProvider;
            _deviceManager = _serviceProvider?.GetService<IDeviceManager>();

            System.Diagnostics.Debug.WriteLine($"[ConfigViewModel] 构造函数：ServiceProvider={_serviceProvider != null}, DeviceManager={_deviceManager != null}");

            // 从服务提供者获取 PluginHost（已由 PluginLoadTask 注册为单例）
            _pluginHost = _serviceProvider?.GetService<IPluginHost>();

            // 如果获取不到，记录警告（但不影响功能，因为可以扫描所有已加载的程序集）
            if (_pluginHost == null)
            {
                System.Diagnostics.Debug.WriteLine("[ConfigViewModel] 警告：无法从服务提供者获取 IPluginHost，将扫描所有已加载的程序集");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[ConfigViewModel] PluginHost 已获取，已加载插件数量: {_pluginHost.LoadedPlugins.Count}");
            }

            // 订阅设备注册事件，当设备注册时自动刷新配置树
            if (_deviceManager != null)
            {
                _deviceManager.DeviceRegistered += OnDeviceRegistered;
                _deviceManager.DeviceUnregistered += OnDeviceUnregistered;
                System.Diagnostics.Debug.WriteLine("[ConfigViewModel] 已订阅设备注册/注销事件");

                // 立即检查一次设备数量
                var deviceCount = _deviceManager.GetDeviceCount();
                System.Diagnostics.Debug.WriteLine($"[ConfigViewModel] 当前已注册设备数量: {deviceCount}");

                // 输出 DeviceManager 实例的哈希码，用于验证是否是同一个实例
                System.Diagnostics.Debug.WriteLine($"[ConfigViewModel] DeviceManager 实例哈希码: {_deviceManager.GetHashCode()}");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("[ConfigViewModel] 警告：DeviceManager 为 null，无法订阅设备事件");
            }

            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {              
                InitializeConfigTree();
            });
        }

        /// <summary>
        /// 设备注册事件处理
        /// </summary>
        private void OnDeviceRegistered(object sender, DeviceRegisteredEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"[ConfigViewModel] OnDeviceRegistered: 设备已注册 - DeviceId={e.DeviceId}, DeviceType={e.DeviceType}");

            // 在 UI 线程上刷新配置树
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                System.Diagnostics.Debug.WriteLine("[ConfigViewModel] OnDeviceRegistered: 开始刷新配置树");
                InitializeConfigTree();
            });
        }

        /// <summary>
        /// 设备注销事件处理
        /// </summary>
        private void OnDeviceUnregistered(object sender, DeviceUnregisteredEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"[ConfigViewModel] OnDeviceUnregistered: 设备已注销 - DeviceId={e.DeviceId}");

            // 在 UI 线程上刷新配置树
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                System.Diagnostics.Debug.WriteLine("[ConfigViewModel] OnDeviceUnregistered: 开始刷新配置树");
                InitializeConfigTree();
            });
        }

        /// <summary>
        /// 设置配置内容区域（用于显示配置界面）
        /// </summary>
        public void SetConfigContentRegion(ContentControl contentControl)
        {
            _configContentRegion = contentControl;
        }

        /// <summary>
        /// 刷新配置树命令
        /// </summary>
        [RelayCommand]
        private void RefreshConfigTree()
        {
            InitializeConfigTree();
        }

        /// <summary>
        /// 初始化配置树
        /// </summary>
        private void InitializeConfigTree()
        {
            TreeNodes.Clear();
            _deviceConfigTypes.Clear();

            // 扫描所有设备配置类型
            ScanDeviceConfigTypes();

            // 按设备类型分组构建树
            BuildConfigTree();
        }

        /// <summary>
        /// 扫描所有插件中的设备配置类型
        /// </summary>
        private void ScanDeviceConfigTypes()
        {
            try
            {
                // 1. 扫描主程序集中的设备配置类型
                ScanAssemblyForDeviceConfigs(Assembly.GetExecutingAssembly());

                // 2. 扫描所有已加载插件的程序集
                if (_pluginHost != null)
                {
                    foreach (var plugin in _pluginHost.LoadedPlugins)
                    {
                        var pluginAssembly = plugin.GetType().Assembly;
                        ScanAssemblyForDeviceConfigs(pluginAssembly);
                    }
                }

                // 3. 扫描当前应用程序域中的所有程序集（作为后备方案）
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    try
                    {
                        ScanAssemblyForDeviceConfigs(assembly);
                    }
                    catch
                    {
                        // 忽略无法扫描的程序集
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"扫描设备配置类型时发生错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 扫描指定程序集中的设备配置类型
        /// </summary>
        private void ScanAssemblyForDeviceConfigs(Assembly assembly)
        {
            try
            {
                var types = assembly.GetTypes()
                    .Where(t => !t.IsAbstract &&
                                !t.IsInterface &&
                                typeof(DeviceConfig).IsAssignableFrom(t) &&
                                t != typeof(DeviceConfig));

                foreach (var type in types)
                {
                    if (_deviceConfigTypes.ContainsKey(type))
                        continue;

                    // 获取 DeviceConfigUIAttribute 特性
                    var uiAttribute = type.GetCustomAttribute<DeviceConfigUIAttribute>();

                    var configInfo = new DeviceConfigInfo
                    {
                        ConfigType = type,
                        ViewType = uiAttribute?.ViewType,
                        ViewModelType = uiAttribute?.ViewModelType,
                        DeviceType = GetDeviceTypeFromConfig(type)
                    };

                    _deviceConfigTypes[type] = configInfo;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"扫描程序集 {assembly.FullName} 时发生错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 从配置类型获取设备类型
        /// </summary>
        private Astra.Core.Devices.DeviceType GetDeviceTypeFromConfig(Type configType)
        {
            try
            {
                // 尝试创建实例并获取 Type 属性
                var instance = Activator.CreateInstance(configType) as DeviceConfig;
                if (instance != null)
                {
                    return instance.Type;
                }
            }
            catch
            {
                // 如果无法创建实例，尝试从类型名称推断
            }

            // 从类型名称推断设备类型
            var typeName = configType.Name;
            if (typeName.Contains("DataAcquisition"))
                return Astra.Core.Devices.DeviceType.DataAcquisition;
            if (typeName.Contains("CAN"))
                return Astra.Core.Devices.DeviceType.CAN;
            if (typeName.Contains("SerialPort"))
                return Astra.Core.Devices.DeviceType.SerialPort;
            if (typeName.Contains("PLC"))
                return Astra.Core.Devices.DeviceType.PLC;
            if (typeName.Contains("Modbus"))
                return Astra.Core.Devices.DeviceType.Modbus;

            return Astra.Core.Devices.DeviceType.Custom;
        }

        /// <summary>
        /// 构建配置树
        /// </summary>
        private void BuildConfigTree()
        {
            System.Diagnostics.Debug.WriteLine("[ConfigViewModel] 开始构建配置树...");

            // 1. 获取已注册的设备实例
            var registeredDevices = GetRegisteredDevices();
            System.Diagnostics.Debug.WriteLine($"[ConfigViewModel] 已注册设备数量: {registeredDevices.Count}");

            // 2. 按设备类型分组（先按已注册的设备，再按配置类型）
            var deviceTypeGroups = new Dictionary<Astra.Core.Devices.DeviceType, List<object>>();

            // 2.1 添加已注册的设备实例
            foreach (var device in registeredDevices)
            {
                var deviceType = device.Type;
                if (!deviceTypeGroups.ContainsKey(deviceType))
                {
                    deviceTypeGroups[deviceType] = new List<object>();
                }
                deviceTypeGroups[deviceType].Add(device);
                System.Diagnostics.Debug.WriteLine($"[ConfigViewModel] 添加设备到树: {device.DeviceName} (Type: {deviceType})");
            }

            // 2.2 添加配置类型（如果该类型还没有设备实例）
            var groupedConfigs = _deviceConfigTypes.Values
                .GroupBy(c => c.DeviceType)
                .OrderBy(g => g.Key);

            foreach (var group in groupedConfigs)
            {
                if (!deviceTypeGroups.ContainsKey(group.Key))
                {
                    deviceTypeGroups[group.Key] = new List<object>();
                }

                // 只为没有设备实例的配置类型添加配置类型节点
                if (deviceTypeGroups[group.Key].Count == 0)
                {
                    foreach (var configInfo in group)
                    {
                        deviceTypeGroups[group.Key].Add(configInfo);
                    }
                }
            }

            // 3. 构建树节点
            foreach (var kvp in deviceTypeGroups.OrderBy(g => g.Key))
            {
                var deviceType = kvp.Key;
                var items = kvp.Value;

                if (items.Count == 0)
                    continue;

                var deviceTypeNode = new TreeNodeViewModel
                {
                    Header = GetDeviceTypeDisplayName(deviceType),
                    Icon = GetDeviceTypeIcon(deviceType),
                    IsExpanded = false,
                    ShowAddButton = true,
                    AddDeviceType = deviceType.ToString(),
                    Tag = deviceType
                };

                // 为每个设备实例或配置类型创建子节点
                foreach (var item in items)
                {
                    TreeNodeViewModel itemNode;

                    if (item is Astra.Core.Devices.Interfaces.IDevice device)
                    {
                        // 设备实例节点
                        var deviceConfig = GetDeviceConfig(device);
                        var configInfo = deviceConfig != null ? GetConfigInfoForDevice(deviceConfig) : null;

                        itemNode = new TreeNodeViewModel
                        {
                            Header = device.DeviceName ?? device.DeviceId ?? "未知设备",
                            Icon = GetDeviceTypeIcon(device.Type),
                            Tag = new DeviceInstanceInfo
                            {
                                Device = device,
                                Config = deviceConfig,
                                ConfigInfo = configInfo
                            },
                            NodeId = device.DeviceId
                        };
                    }
                    else if (item is DeviceConfigInfo configInfo)
                    {
                        // 配置类型节点（用于添加新设备）
                        itemNode = new TreeNodeViewModel
                        {
                            Header = GetConfigTypeDisplayName(configInfo.ConfigType),
                            Icon = "📋",
                            Tag = configInfo,
                            NodeId = configInfo.ConfigType.FullName
                        };
                    }
                    else
                    {
                        continue;
                    }

                    deviceTypeNode.Children.Add(itemNode);
                }

                TreeNodes.Add(deviceTypeNode);
                System.Diagnostics.Debug.WriteLine($"[ConfigViewModel] 添加设备类型节点: {deviceTypeNode.Header}，包含 {deviceTypeNode.Children.Count} 个子节点");
            }

            System.Diagnostics.Debug.WriteLine($"[ConfigViewModel] 配置树构建完成，共 {TreeNodes.Count} 个设备类型节点");
        }

        /// <summary>
        /// 获取已注册的设备实例
        /// </summary>
        private List<Astra.Core.Devices.Interfaces.IDevice> GetRegisteredDevices()
        {
            var devices = new List<Astra.Core.Devices.Interfaces.IDevice>();

            System.Diagnostics.Debug.WriteLine("[ConfigViewModel] GetRegisteredDevices: 开始获取设备");

            if (_deviceManager == null)
            {
                System.Diagnostics.Debug.WriteLine("[ConfigViewModel] GetRegisteredDevices: DeviceManager 为 null");
                return devices;
            }

            try
            {
                // 先检查设备数量
                var deviceCount = _deviceManager.GetDeviceCount();
                System.Diagnostics.Debug.WriteLine($"[ConfigViewModel] GetRegisteredDevices: DeviceManager.GetDeviceCount() = {deviceCount}");

                // 获取所有设备
                var result = _deviceManager.GetAllDevices();
                System.Diagnostics.Debug.WriteLine($"[ConfigViewModel] GetRegisteredDevices: GetAllDevices() 结果 - Success={result.Success}, ErrorMessage={result.ErrorMessage}");

                if (result.Success && result.Data != null)
                {
                    devices.AddRange(result.Data);
                    System.Diagnostics.Debug.WriteLine($"[ConfigViewModel] GetRegisteredDevices: 成功获取 {devices.Count} 个设备");

                    // 输出设备详情用于调试
                    if (devices.Count > 0)
                    {
                        foreach (var device in devices)
                        {
                            System.Diagnostics.Debug.WriteLine($"  - 设备: {device.DeviceName ?? "未命名"} (ID: {device.DeviceId ?? "无ID"}, Type: {device.Type})");
                        }
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("[ConfigViewModel] GetRegisteredDevices: 设备列表为空");
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[ConfigViewModel] GetRegisteredDevices: 获取设备失败 - Success={result.Success}, ErrorMessage={result.ErrorMessage}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ConfigViewModel] GetRegisteredDevices: 获取已注册设备时发生异常: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"  异常类型: {ex.GetType().Name}");
                System.Diagnostics.Debug.WriteLine($"  堆栈: {ex.StackTrace}");
            }

            return devices;
        }

        /// <summary>
        /// 从设备获取配置对象
        /// </summary>
        private DeviceConfig GetDeviceConfig(Astra.Core.Devices.Interfaces.IDevice device)
        {
            try
            {
                // 尝试通过反射获取 CurrentConfig 属性
                var deviceType = device.GetType();
                System.Diagnostics.Debug.WriteLine($"[ConfigViewModel] 尝试获取设备配置，设备类型: {deviceType.Name}");

                // 方法1：检查是否实现了 IConfigurable<TConfig>
                var configurableInterface = deviceType.GetInterfaces()
                    .FirstOrDefault(i => i.IsGenericType &&
                                         i.GetGenericTypeDefinition() == typeof(Astra.Core.Devices.Interfaces.IConfigurable<>));

                if (configurableInterface != null)
                {
                    System.Diagnostics.Debug.WriteLine($"[ConfigViewModel] 设备实现了 IConfigurable<{configurableInterface.GetGenericArguments()[0].Name}>");
                    var currentConfigProperty = configurableInterface.GetProperty("CurrentConfig");
                    if (currentConfigProperty != null)
                    {
                        var config = currentConfigProperty.GetValue(device) as DeviceConfig;
                        if (config != null)
                        {
                            System.Diagnostics.Debug.WriteLine($"[ConfigViewModel] 成功通过接口获取配置: {config.GetType().Name}");
                            return config;
                        }
                    }
                }

                // 方法2：直接查找 CurrentConfig 属性
                var directProperty = deviceType.GetProperty("CurrentConfig",
                    System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic);

                if (directProperty != null)
                {
                    System.Diagnostics.Debug.WriteLine($"[ConfigViewModel] 找到 CurrentConfig 属性");
                    var config = directProperty.GetValue(device) as DeviceConfig;
                    if (config != null)
                    {
                        System.Diagnostics.Debug.WriteLine($"[ConfigViewModel] 成功直接获取配置: {config.GetType().Name}");
                        return config;
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[ConfigViewModel] 未找到 CurrentConfig 属性");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ConfigViewModel] 获取设备配置时发生错误: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"  堆栈: {ex.StackTrace}");
            }

            System.Diagnostics.Debug.WriteLine($"[ConfigViewModel] 无法获取设备配置，返回 null");
            return null;
        }

        /// <summary>
        /// 为设备配置获取配置信息
        /// </summary>
        private DeviceConfigInfo GetConfigInfoForDevice(DeviceConfig config)
        {
            var configType = config.GetType();

            if (_deviceConfigTypes.TryGetValue(configType, out var configInfo))
            {
                return configInfo;
            }

            // 如果缓存中没有，创建一个新的配置信息
            var uiAttribute = configType.GetCustomAttribute<DeviceConfigUIAttribute>();
            return new DeviceConfigInfo
            {
                ConfigType = configType,
                ViewType = uiAttribute?.ViewType,
                ViewModelType = uiAttribute?.ViewModelType,
                DeviceType = config.Type
            };
        }

        /// <summary>
        /// 获取设备状态图标
        /// </summary>
        private string GetDeviceStatusIcon(Astra.Core.Devices.Interfaces.IDevice device)
        {
            if (device.IsOnline)
                return "🟢";
            else if (device.Status == Astra.Core.Devices.DeviceStatus.Error)
                return "🔴";
            else
                return "⚪";
        }

        /// <summary>
        /// 获取设备类型显示名称
        /// </summary>
        private string GetDeviceTypeDisplayName(Astra.Core.Devices.DeviceType deviceType)
        {
            return deviceType switch
            {
                Astra.Core.Devices.DeviceType.DataAcquisition => "数据采集设备",
                Astra.Core.Devices.DeviceType.CAN => "CAN 设备",
                Astra.Core.Devices.DeviceType.SerialPort => "串口设备",
                Astra.Core.Devices.DeviceType.PLC => "PLC 设备",
                Astra.Core.Devices.DeviceType.Modbus => "Modbus 设备",
                _ => "自定义设备"
            };
        }

        /// <summary>
        /// 获取设备类型图标
        /// </summary>
        private string GetDeviceTypeIcon(Astra.Core.Devices.DeviceType deviceType)
        {
            return deviceType switch
            {
                Astra.Core.Devices.DeviceType.DataAcquisition => "📊",
                Astra.Core.Devices.DeviceType.CAN => "🔌",
                Astra.Core.Devices.DeviceType.SerialPort => "📡",
                Astra.Core.Devices.DeviceType.PLC => "⚙️",
                Astra.Core.Devices.DeviceType.Modbus => "🔧",
                _ => "📦"
            };
        }

        /// <summary>
        /// 获取配置类型显示名称
        /// </summary>
        private string GetConfigTypeDisplayName(Type configType)
        {
            // 移除 "Config" 后缀
            var name = configType.Name;
            if (name.EndsWith("Config"))
            {
                name = name.Substring(0, name.Length - 6);
            }
            return name;
        }

        /// <summary>
        /// 节点选择命令
        /// </summary>
        [RelayCommand]
        private void NodeSelected(TreeNodeViewModel node)
        {
            if (node == null || node.Tag == null)
                return;

            SelectedNode = node;

            // 如果选择的是设备实例节点，加载设备配置界面
            if (node.Tag is DeviceInstanceInfo deviceInstance)
            {
                if (deviceInstance.ConfigInfo != null && deviceInstance.Config != null)
                {
                    LoadConfigView(deviceInstance.ConfigInfo, deviceInstance.Config);
                }
            }
            // 如果选择的是配置类型节点，加载配置界面（用于添加新设备）
            else if (node.Tag is DeviceConfigInfo configInfo)
            {
                LoadConfigView(configInfo);
            }
        }

        /// <summary>
        /// 加载配置界面
        /// </summary>
        private void LoadConfigView(DeviceConfigInfo configInfo, DeviceConfig deviceConfig = null)
        {
            if (_configContentRegion == null)
                return;

            try
            {
                UserControl configView = null;

                // 1. 尝试使用特性指定的 View 类型
                if (configInfo.ViewType != null)
                {
                    configView = Activator.CreateInstance(configInfo.ViewType) as UserControl;
                }

                // 2. 如果 View 不存在，创建一个默认的配置界面
                if (configView == null)
                {
                    configView = CreateDefaultConfigView(configInfo);
                }

                // 3. 设置 ViewModel（如果指定了）
                if (configView != null && configInfo.ViewModelType != null)
                {
                    var viewModel = Activator.CreateInstance(configInfo.ViewModelType, deviceConfig);
                    configView.DataContext = viewModel;

                    // 如果提供了设备配置对象，尝试设置到 ViewModel
                    if (deviceConfig != null)
                    {
                        try
                        {
                            var viewModelType = viewModel.GetType();
                            var configProperty = viewModelType.GetProperty("Config") ??
                                                viewModelType.GetProperty("DeviceConfig");
                            if (configProperty != null && configProperty.PropertyType.IsAssignableFrom(deviceConfig.GetType()))
                            {
                                configProperty.SetValue(viewModel, deviceConfig);
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"设置设备配置到 ViewModel 时发生错误: {ex.Message}");
                        }
                    }
                }

                // 4. 显示配置界面
                if (configView != null)
                {
                    _configContentRegion.Content = configView;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"加载配置界面时发生错误: {ex.Message}");
                _configContentRegion.Content = new TextBlock
                {
                    Text = $"无法加载配置界面: {ex.Message}",
                    Margin = new Thickness(20)
                };
            }
        }

        /// <summary>
        /// 创建默认配置界面
        /// </summary>
        private UserControl CreateDefaultConfigView(DeviceConfigInfo configInfo)
        {
            // 创建一个简单的默认界面，显示配置类型信息
            var stackPanel = new StackPanel
            {
                Margin = new Thickness(20),
                Orientation = Orientation.Vertical
            };

            stackPanel.Children.Add(new TextBlock
            {
                Text = $"配置类型: {configInfo.ConfigType.Name}",
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 10)
            });

            stackPanel.Children.Add(new TextBlock
            {
                Text = $"设备类型: {configInfo.DeviceType}",
                FontSize = 14,
                Margin = new Thickness(0, 0, 0, 10)
            });

            if (configInfo.ViewType != null)
            {
                stackPanel.Children.Add(new TextBlock
                {
                    Text = $"视图类型: {configInfo.ViewType.Name}",
                    FontSize = 12,
                    Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Gray)
                });
            }

            var userControl = new UserControl
            {
                Content = stackPanel
            };

            return userControl;
        }

        /// <summary>
        /// 添加设备配置命令
        /// </summary>
        [RelayCommand]
        private void AddDeviceConfig(TreeNodeViewModel node)
        {
            if (node == null || node.Tag == null)
                return;

            // 如果节点是设备类型节点，显示添加配置的对话框
            if (node.Tag is Astra.Core.Devices.DeviceType deviceType)
            {
                // 获取该设备类型的第一个配置类型
                var configInfo = _deviceConfigTypes.Values
                    .FirstOrDefault(c => c.DeviceType == deviceType);

                if (configInfo != null)
                {
                    // 创建新的配置实例
                    try
                    {
                        DeviceConfig? newConfig = Activator.CreateInstance(configInfo.ConfigType) as DeviceConfig;

                        if (newConfig != null)
                        {
                            // 设置默认值
                            newConfig.DeviceName = $"新{GetConfigTypeDisplayName(configInfo.ConfigType)}";

                            // 配置类型节点（用于添加新设备）
                            var itemNode = new TreeNodeViewModel
                            {
                                Header = GetConfigTypeDisplayName(configInfo.ConfigType),
                                Icon = GetDeviceTypeIcon(newConfig.Type),
                                Tag = new DeviceInstanceInfo() { Config = newConfig, ConfigInfo = configInfo },
                            };

                            node.Children.Add(itemNode);
                            SelectedNode = itemNode;
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"创建设备配置时发生错误: {ex.Message}");
                        MessageBox.Show($"无法创建设备配置: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        /// <summary>
        /// 设备配置信息
        /// </summary>
        private class DeviceConfigInfo
        {
            public Type ConfigType { get; set; }
            public Type ViewType { get; set; }
            public Type ViewModelType { get; set; }
            public Astra.Core.Devices.DeviceType DeviceType { get; set; }
        }

        /// <summary>
        /// 设备实例信息
        /// </summary>
        private class DeviceInstanceInfo
        {
            public Astra.Core.Devices.Interfaces.IDevice Device { get; set; }
            public DeviceConfig Config { get; set; }
            public DeviceConfigInfo ConfigInfo { get; set; }
        }
    }
}
