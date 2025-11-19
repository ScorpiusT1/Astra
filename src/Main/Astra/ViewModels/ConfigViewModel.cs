using Astra.Core.Configuration;
using Astra.Core.Devices.Attributes;
using Astra.Core.Devices.Configuration;
using Astra.Core.Devices.Interfaces;
using Astra.Core.Devices.Management;
using Astra.Core.Foundation.Common;
using Astra.Core.Plugins.Abstractions;
using Astra.Core.Plugins.Messaging;
using Astra.Core.Logs;
using Astra.UI.Helpers;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;

namespace Astra.ViewModels
{
    public partial class ConfigViewModel : ObservableObject
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ConfigurationManager _configurationManager;
        private readonly IDeviceManager _deviceManager;
        private readonly IPluginHost _pluginHost;
        private ContentControl _configContentRegion;
        private System.Windows.Controls.TreeView _treeView;

        [ObservableProperty]
        private string _title = "配置管理";

        [ObservableProperty]
        private ObservableCollection<TreeNodeViewModel> _treeNodes = new();

        [ObservableProperty]
        private TreeNodeViewModel _selectedNode;

        // 设备配置类型信息缓存
        private readonly Dictionary<Type, DeviceConfigInfo> _deviceConfigTypes = new();

        // 待删除的设备ID列表（点击保存时才从设备管理器注销）
        private readonly HashSet<string> _pendingDeviceUnregisters = new HashSet<string>();

        /// <summary>
        /// SelectedNode 改变时的处理，自动同步节点的 IsSelected 状态
        /// </summary>
        partial void OnSelectedNodeChanged(TreeNodeViewModel value)
        {
            // 清除所有节点的 IsSelected 状态
            foreach (var rootNode in TreeNodes)
            {
                ClearNodeSelection(rootNode);
            }

            // 设置新选中节点的 IsSelected 状态
            if (value != null)
            {
                value.IsSelected = true;
                
                // 加载对应的配置界面
                NodeSelected(value);
            }
            else
            {
                // 如果没有选中节点，清除配置区域内容
                if (_configContentRegion != null)
                {
                    _configContentRegion.Content = null;
                }
            }
        }

        /// <summary>
        /// 递归清除节点的选中状态
        /// </summary>
        private void ClearNodeSelection(TreeNodeViewModel node)
        {
            if (node == null)
                return;

            node.IsSelected = false;

            if (node.Children != null)
            {
                foreach (var child in node.Children)
                {
                    ClearNodeSelection(child);
                }
            }
        }

        public ConfigViewModel()
        {
            // 从服务提供者获取依赖
            _serviceProvider = App.ServiceProvider;
            _configurationManager = _serviceProvider?.GetService<ConfigurationManager>();
            _deviceManager = _serviceProvider?.GetService<IDeviceManager>();

            System.Diagnostics.Debug.WriteLine($"[ConfigViewModel] 构造函数：ServiceProvider={_serviceProvider != null}, ConfigurationManager={_configurationManager != null}, DeviceManager={_deviceManager != null}");

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

            // 订阅配置管理器的事件（配置变更时刷新配置树）
            if (_configurationManager != null)
            {
                _configurationManager.ConfigChanged += OnConfigurationChanged;
                System.Diagnostics.Debug.WriteLine("[ConfigViewModel] 已订阅配置管理器变更事件");
            }

            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {              
                InitializeConfigTree();
            });
        }

        // 防止配置树刷新时的递归调用标志
        private bool _isRefreshingTree = false;

        /// <summary>
        /// 配置变更事件处理
        /// </summary>
        private void OnConfigurationChanged(object sender, ConfigChangedEventArgs e)
        {
            // 如果正在刷新配置树，跳过此次事件（避免递归）
            if (_isRefreshingTree)
            {
                System.Diagnostics.Debug.WriteLine($"[ConfigViewModel] OnConfigurationChanged: 正在刷新配置树，跳过此次事件 - ConfigId={e.ConfigId}");
                return;
            }

            System.Diagnostics.Debug.WriteLine($"[ConfigViewModel] OnConfigurationChanged: 配置已变更 - ConfigId={e.ConfigId}, ConfigType={e.ConfigType}");

            // 在 UI 线程上刷新配置树
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                System.Diagnostics.Debug.WriteLine("[ConfigViewModel] OnConfigurationChanged: 开始刷新配置树");
                _isRefreshingTree = true;
                try
                {
                InitializeConfigTree();
                }
                finally
                {
                    _isRefreshingTree = false;
                }
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
        /// 设置树视图（用于设置焦点）
        /// </summary>
        public void SetTreeView(System.Windows.Controls.TreeView treeView)
        {
            _treeView = treeView;
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
            // 如果正在刷新，跳过（避免递归）
            if (_isRefreshingTree)
            {
                System.Diagnostics.Debug.WriteLine("[ConfigViewModel] InitializeConfigTree: 正在刷新中，跳过");
                return;
            }

            _isRefreshingTree = true;
            try
            {
                // 保存当前展开状态（按设备类型）
                var expandedDeviceTypes = new HashSet<Astra.Core.Devices.DeviceType>();
                foreach (var node in TreeNodes)
                {
                    if (node.Tag is Astra.Core.Devices.DeviceType deviceType && node.IsExpanded)
                    {
                        expandedDeviceTypes.Add(deviceType);
                    }
                }

                TreeNodes.Clear();
                _deviceConfigTypes.Clear();

                // 按设备类型分组构建树（配置来源：ConfigurationManager）
                BuildConfigTree();

                // 恢复展开状态
                foreach (var node in TreeNodes)
                {
                    if (node.Tag is Astra.Core.Devices.DeviceType deviceType && expandedDeviceTypes.Contains(deviceType))
                    {
                        node.IsExpanded = true;
                    }
                }
            }
            finally
            {
                _isRefreshingTree = false;
            }
        }

        /// <summary>
        /// 按需获取指定设备类型的配置类型（仅在需要时扫描）
        /// </summary>
        private List<DeviceConfigInfo> GetConfigTypesForDeviceType(Astra.Core.Devices.DeviceType deviceType)
        {
            var result = new List<DeviceConfigInfo>();

            try
            {
                // 扫描所有程序集，查找匹配的配置类型
                var assembliesToScan = new List<Assembly>();

                // 1. 主程序集
                assembliesToScan.Add(Assembly.GetExecutingAssembly());

                // 2. 已加载插件的程序集
                if (_pluginHost != null)
                {
                    foreach (var plugin in _pluginHost.LoadedPlugins)
                    {
                        assembliesToScan.Add(plugin.GetType().Assembly);
                    }
                }

                // 3. 当前应用程序域中的所有程序集（作为后备方案）
                assembliesToScan.AddRange(AppDomain.CurrentDomain.GetAssemblies().Distinct());

                foreach (var assembly in assembliesToScan.Distinct())
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
                            // 检查是否已经处理过
                    if (_deviceConfigTypes.ContainsKey(type))
                            {
                                if (_deviceConfigTypes[type].DeviceType == deviceType)
                                {
                                    result.Add(_deviceConfigTypes[type]);
                                }
                        continue;
                            }

                            // 获取设备类型
                            Astra.Core.Devices.DeviceType configDeviceType;
                            try
                            {
                                // 尝试创建实例并获取 Type 属性
                                var instance = Activator.CreateInstance(type) as DeviceConfig;
                                configDeviceType = instance?.Type ?? GetDeviceTypeFromConfigName(type.Name);
                            }
                            catch
                            {
                                // 如果无法创建实例，从类型名称推断
                                configDeviceType = GetDeviceTypeFromConfigName(type.Name);
                            }

                            // 只返回匹配的设备类型
                            if (configDeviceType == deviceType)
                            {
                    // 获取 DeviceConfigUIAttribute 特性
                    var uiAttribute = type.GetCustomAttribute<DeviceConfigUIAttribute>();

                    var configInfo = new DeviceConfigInfo
                    {
                        ConfigType = type,
                        ViewType = uiAttribute?.ViewType,
                        ViewModelType = uiAttribute?.ViewModelType,
                                    DeviceType = configDeviceType
                    };

                    _deviceConfigTypes[type] = configInfo;
                                result.Add(configInfo);
                            }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"扫描程序集 {assembly.FullName} 时发生错误: {ex.Message}");
            }
        }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"获取设备类型配置类型时发生错误: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// 从配置类型名称推断设备类型
        /// </summary>
        private Astra.Core.Devices.DeviceType GetDeviceTypeFromConfigName(string typeName)
        {
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
        /// 构建配置树（基于 ConfigurationManager 中的配置）
        /// </summary>
        private void BuildConfigTree()
        {
            System.Diagnostics.Debug.WriteLine("[ConfigViewModel] 开始构建配置树...");

            // 1. 从 ConfigurationManager 获取所有设备配置（配置独立于设备）
            var deviceConfigs = GetAllDeviceConfigs();
            System.Diagnostics.Debug.WriteLine($"[ConfigViewModel] 配置数量: {deviceConfigs.Count}");

            // 2. 按设备类型分组（只基于配置）
            var deviceTypeGroups = new Dictionary<Astra.Core.Devices.DeviceType, List<DeviceConfig>>();

            // 2.1 添加所有配置（配置是独立的，不依赖设备实例是否存在）
            foreach (var deviceConfig in deviceConfigs)
            {
                var deviceType = deviceConfig.Type;
                if (!deviceTypeGroups.ContainsKey(deviceType))
                {
                    deviceTypeGroups[deviceType] = new List<DeviceConfig>();
                }
                deviceTypeGroups[deviceType].Add(deviceConfig);
                System.Diagnostics.Debug.WriteLine($"[ConfigViewModel] 添加配置到树: {deviceConfig.DeviceName} (Type: {deviceType})");
            }

            // 2.2 处理没有配置的设备类型（显示配置类型节点，用于添加新配置）
            // 从已存在的配置中推断所有设备类型
            var existingDeviceTypes = deviceConfigs.Select(c => c.Type).Distinct().ToHashSet();
            
            // 获取所有可能的设备类型（从枚举）
            var allDeviceTypes = Enum.GetValues(typeof(Astra.Core.Devices.DeviceType))
                .Cast<Astra.Core.Devices.DeviceType>();

            foreach (var deviceType in allDeviceTypes)
            {
                if (!deviceTypeGroups.ContainsKey(deviceType))
                {
                    deviceTypeGroups[deviceType] = new List<DeviceConfig>();
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

                // 为每个配置创建子节点（配置独立于设备）
                // 按创建时间排序，确保顺序稳定（与保存时的顺序一致）
                var sortedItems = items.OrderBy(c => c.CreatedAt).ThenBy(c => c.DeviceName).ToList();
                foreach (var deviceConfig in sortedItems)
                {
                    var configInfo = GetConfigInfoForDevice(deviceConfig);

                    // 检查配置对应的设备是否存在（仅用于显示状态，配置来源仍然是 ConfigurationManager）
                    IDevice device = null;
                    if (_deviceManager != null)
                    {
                        var deviceResult = _deviceManager.GetDevice(deviceConfig.DeviceId);
                        if (deviceResult.Success && deviceResult.Data != null)
                        {
                            device = deviceResult.Data;
                        }
                    }

                    var itemNode = new TreeNodeViewModel
                    {
                        Header = deviceConfig.DeviceName ?? deviceConfig.DeviceId ?? "未知配置",
                        Icon = GetDeviceTypeIcon(deviceConfig.Type),
                            Tag = new DeviceInstanceInfo
                            {
                            Device = device, // 设备可能为 null（配置存在但设备还未创建）
                            Config = deviceConfig, // 配置来源：ConfigurationManager
                                ConfigInfo = configInfo
                            },
                        NodeId = Guid.NewGuid().ToString(),
                        ShowDeleteButton = true // 子节点可以删除
                    };

                    deviceTypeNode.Children.Add(itemNode);
                }

                // 如果没有配置，添加配置类型节点（用于添加新配置）
                if (items.Count == 0)
                {
                    // 按需获取该设备类型的配置类型（仅在需要时扫描）
                    var configTypesForThisDeviceType = GetConfigTypesForDeviceType(deviceType);

                    foreach (var configInfo in configTypesForThisDeviceType)
                    {
                        var itemNode = new TreeNodeViewModel
                        {
                            Header = GetConfigTypeDisplayName(configInfo.ConfigType),
                            Icon = "📋",
                            Tag = configInfo,
                            NodeId = Guid.NewGuid().ToString(),
                            ShowDeleteButton = false, // 配置类型节点不能删除
                        };
                        deviceTypeNode.Children.Add(itemNode);
                    }
                }

                TreeNodes.Add(deviceTypeNode);
                System.Diagnostics.Debug.WriteLine($"[ConfigViewModel] 添加设备类型节点: {deviceTypeNode.Header}，包含 {deviceTypeNode.Children.Count} 个子节点");
            }

            System.Diagnostics.Debug.WriteLine($"[ConfigViewModel] 配置树构建完成，共 {TreeNodes.Count} 个设备类型节点");
        }

        /// <summary>
        /// 按照树节点的顺序获取设备配置（保持子节点顺序）
        /// </summary>
        private List<DeviceConfig> GetDeviceConfigsInTreeOrder()
        {
            var deviceConfigs = new List<DeviceConfig>();

            if (_configurationManager == null)
            {
                return deviceConfigs;
            }

            try
            {
                // 按照当前树节点的顺序获取配置
                foreach (var deviceTypeNode in TreeNodes)
                {
                    if (deviceTypeNode.Children != null)
                    {
                        foreach (var childNode in deviceTypeNode.Children)
                        {
                            if (childNode.Tag is DeviceInstanceInfo deviceInstance && deviceInstance.Config != null)
                            {
                                // 从 ConfigurationManager 获取最新的配置（确保数据是最新的）
                                var configResult = _configurationManager.GetConfig(deviceInstance.Config.ConfigId);
                                if (configResult.Success && configResult.Data is DeviceConfig deviceConfig)
                                {
                                    deviceConfigs.Add(deviceConfig);
                                }
                                else if (deviceInstance.Config != null)
                                {
                                    // 如果从 ConfigurationManager 获取不到，使用树节点中的配置
                                    deviceConfigs.Add(deviceInstance.Config);
                                }
                            }
                        }
                    }
                }

                // 如果树是空的或者没有从树中获取到配置，则从 ConfigurationManager 获取所有配置
                // 并按创建时间排序（确保顺序稳定）
                if (deviceConfigs.Count == 0)
                {
                    var allConfigs = _configurationManager.GetAllConfigs()
                        .OfType<DeviceConfig>()
                        .OrderBy(c => c.CreatedAt)
                        .ThenBy(c => c.DeviceName)
                        .ToList();
                    
                    deviceConfigs.AddRange(allConfigs);
                }

                System.Diagnostics.Debug.WriteLine($"[ConfigViewModel] GetDeviceConfigsInTreeOrder: 按树节点顺序获取了 {deviceConfigs.Count} 个配置");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ConfigViewModel] GetDeviceConfigsInTreeOrder: 发生错误: {ex.Message}");
                // 发生错误时，回退到从 ConfigurationManager 获取所有配置
                var allConfigs = _configurationManager.GetAllConfigs()
                    .OfType<DeviceConfig>()
                    .OrderBy(c => c.CreatedAt)
                    .ThenBy(c => c.DeviceName)
                    .ToList();
                deviceConfigs.AddRange(allConfigs);
            }

            return deviceConfigs;
        }

        /// <summary>
        /// 获取所有设备配置（从 ConfigurationManager 获取，配置独立于设备）
        /// </summary>
        private List<DeviceConfig> GetAllDeviceConfigs()
        {
            var deviceConfigs = new List<DeviceConfig>();

            if (_configurationManager == null)
            {
                System.Diagnostics.Debug.WriteLine("[ConfigViewModel] GetAllDeviceConfigs: ConfigurationManager 为 null");
                return deviceConfigs;
            }

            try
            {
                // 从 ConfigurationManager 获取所有设备配置（配置是独立的，不依赖设备实例）
                var allConfigs = _configurationManager.GetAllConfigs();
                
                foreach (var config in allConfigs)
                {
                    // 只处理设备配置
                    if (config is DeviceConfig deviceConfig)
                    {
                        deviceConfigs.Add(deviceConfig);
                    }
                }

                System.Diagnostics.Debug.WriteLine($"[ConfigViewModel] GetAllDeviceConfigs: 从 ConfigurationManager 成功获取 {deviceConfigs.Count} 个设备配置");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ConfigViewModel] GetAllDeviceConfigs: 获取设备配置时发生异常: {ex.Message}");
            }

            return deviceConfigs;
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
                // 按需获取该设备类型的配置类型（仅在需要时扫描）
                var configTypes = GetConfigTypesForDeviceType(deviceType);
                var configInfo = configTypes.FirstOrDefault();

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

                            // 将新配置注册到 ConfigurationManager
                            if (_configurationManager != null)
                            {
                                // 获取配置文件路径并注册
                                var configFilePath = GetConfigFilePath(deviceType, newConfig.GetType());
                                var registerResult = _configurationManager.RegisterConfig(newConfig, configFilePath);
                                if (!registerResult.Success)
                                {
                                    MessageBoxHelper.ShowError($"无法注册配置: {registerResult.ErrorMessage}", "错误");
                                    return;
                                }
                            }

                            // 配置类型节点（用于添加新设备）
                            var itemNode = new TreeNodeViewModel
                            {
                                Header = newConfig.DeviceName ?? GetConfigTypeDisplayName(configInfo.ConfigType),
                                Icon = GetDeviceTypeIcon(newConfig.Type),
                                Tag = new DeviceInstanceInfo() { Config = newConfig, ConfigInfo = configInfo },
                                ShowDeleteButton = true, // 子节点可以删除
                                NodeId = Guid.NewGuid().ToString() // 为新添加的节点生成唯一ID
                            };

                            node.Children.Add(itemNode);
                            SelectedNode = itemNode;
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"创建设备配置时发生错误: {ex.Message}");
                        MessageBoxHelper.ShowError($"无法创建设备配置: {ex.Message}", "错误");
                    }
                }
            }
        }

        /// <summary>
        /// 拖拽放置命令（用于重排序）
        /// </summary>
        [RelayCommand]
        private void DragDropNode(object parameter)
        {
            if (parameter == null)
                return;

            try
            {
                // 使用反射获取 Source 和 Target 属性
                var sourceProperty = parameter.GetType().GetProperty("Source");
                var targetProperty = parameter.GetType().GetProperty("Target");

                if (sourceProperty == null || targetProperty == null)
                    return;

                var sourceNode = sourceProperty.GetValue(parameter) as TreeNodeViewModel;
                var targetNode = targetProperty.GetValue(parameter) as TreeNodeViewModel;

                if (sourceNode == null || targetNode == null)
                    return;

                // 如果源节点和目标节点相同，不做任何操作
                if (sourceNode == targetNode)
                    return;

                // 查找源节点和目标节点的父节点
                TreeNodeViewModel sourceParent = null;
                TreeNodeViewModel targetParent = null;
                int sourceIndex = -1;
                int targetIndex = -1;

                // 在树中查找源节点和目标节点的位置
                foreach (var rootNode in TreeNodes)
                {
                    // 查找源节点
                    if (FindNodeAndParent(rootNode, sourceNode, ref sourceParent, ref sourceIndex))
                    {
                        break;
                    }
                }

                foreach (var rootNode in TreeNodes)
                {
                    // 查找目标节点
                    if (FindNodeAndParent(rootNode, targetNode, ref targetParent, ref targetIndex))
                    {
                        break;
                    }
                }

                // 如果找不到源节点或目标节点，不允许移动
                if (sourceIndex < 0 || targetIndex < 0)
                    return;

                // 如果源节点或目标节点是根节点（parent == null），不允许移动根节点
                if (sourceParent == null || targetParent == null)
                    return;

                // 如果源节点和目标节点不在同一个父节点下，不做任何操作（只允许同一父节点下的重排序）
                if (sourceParent != targetParent)
                    return;

                // 如果源索引和目标索引相同，不做任何操作
                if (sourceIndex == targetIndex)
                    return;

                // 执行移动操作
                var parentChildren = sourceParent.Children;
                
                // 先保存源节点
                var nodeToMove = parentChildren[sourceIndex];
                
                // 移除源节点
                parentChildren.RemoveAt(sourceIndex);

                // 重新计算目标索引（因为已经移除了源节点）
                // 拖拽行为：
                // - 从上往下拖：源节点出现在目标节点之后
                // - 从下往上拖：源节点出现在目标节点之前
                int newTargetIndex;
                if (sourceIndex < targetIndex)
                {
                    // 如果源节点在目标节点之前（从上往下拖）
                    // 例如：原始列表 [A(source=0), B(1), C(target=2), D(3)]
                    // 移除A后变成 [B(0), C(1), D(2)]，C的原位置是索引2，现在在索引1
                    // 要在C之后插入A，应该插入到索引2（即原targetIndex位置）
                    newTargetIndex = targetIndex;
                }
                else
                {
                    // 如果源节点在目标节点之后（从下往上拖）
                    // 例如：原始列表 [A(0), B(target=1), C(source=2)]
                    // 移除C后变成 [A(0), B(1)]，targetIndex不变还是1
                    // 要在B之前插入C，应该插入到targetIndex位置
                    newTargetIndex = targetIndex;
                }

                // 确保索引有效
                if (newTargetIndex < 0)
                    newTargetIndex = 0;
                if (newTargetIndex > parentChildren.Count)
                    newTargetIndex = parentChildren.Count;

                parentChildren.Insert(newTargetIndex, nodeToMove);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"拖拽节点时发生错误: {ex.Message}");
                MessageBoxHelper.ShowError($"无法移动节点: {ex.Message}", "错误");
            }
        }

        /// <summary>
        /// 查找节点及其父节点
        /// </summary>
        private bool FindNodeAndParent(TreeNodeViewModel root, TreeNodeViewModel target, ref TreeNodeViewModel parent, ref int index)
        {
            // 使用 ReferenceEquals 确保是同一个对象引用，避免误删其他节点
            if (ReferenceEquals(root, target))
            {
                // 目标节点是根节点，父节点为 null
                parent = null;
                index = TreeNodes.IndexOf(root);
                return true;
            }

            // 在子节点中查找
            for (int i = 0; i < root.Children.Count; i++)
            {
                // 使用 ReferenceEquals 确保是同一个对象引用
                if (ReferenceEquals(root.Children[i], target))
                {
                    parent = root;
                    index = i;
                    return true;
                }

                // 递归查找
                if (FindNodeAndParent(root.Children[i], target, ref parent, ref index))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 设置焦点到指定的节点
        /// </summary>
        private void FocusOnNode(TreeNodeViewModel node)
        {
            if (_treeView == null || node == null)
                return;

            try
            {
                // 查找对应的 TreeViewItem
                var treeViewItem = FindTreeViewItem(_treeView, node);
                if (treeViewItem != null)
                {
                    // 确保 TreeViewItem 处于选中状态
                    treeViewItem.IsSelected = true;
                    // 将 TreeViewItem 滚动到视图中
                    treeViewItem.BringIntoView();
                    // 设置焦点到 TreeViewItem
                    treeViewItem.Focus();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"设置焦点到节点时发生错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 在 TreeView 中查找对应的 TreeViewItem
        /// </summary>
        private System.Windows.Controls.TreeViewItem FindTreeViewItem(System.Windows.Controls.ItemsControl parent, TreeNodeViewModel target)
        {
            if (parent == null || target == null)
                return null;

            // 确保容器已生成
            parent.UpdateLayout();

            foreach (var item in parent.Items)
            {
                var container = parent.ItemContainerGenerator.ContainerFromItem(item) as System.Windows.Controls.TreeViewItem;
                if (container != null)
                {
                    if (ReferenceEquals(item, target))
                    {
                        return container;
                    }

                    // 如果容器有子节点，需要确保子容器已生成
                    if (container.HasItems)
                    {
                        container.UpdateLayout();
                    }

                    // 递归查找子节点
                    var found = FindTreeViewItem(container, target);
                    if (found != null)
                    {
                        return found;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// 删除节点命令
        /// </summary>
        [RelayCommand]
        private void DeleteNode(TreeNodeViewModel node)
        {
            if (node == null)
                return;

            try
            {
                // 确认删除
                if (!MessageBoxHelper.Confirm($"确定要删除 \"{node.Header}\" 吗？", "确认删除"))
                    return;

                // 注意：删除节点时只操作 TreeNodes 对象和 ConfigurationManager，不注销设备
                // 只有在点击保存配置时，才需要从设备管理器注销设备

                // 获取要删除的配置和设备信息
                string deviceIdToDelete = null;
                if (node.Tag is DeviceInstanceInfo deviceInstance)
                {
                    if (deviceInstance.Config != null)
                    {
                        deviceIdToDelete = deviceInstance.Config.DeviceId;
                    }
                    else if (deviceInstance.Device != null)
                    {
                        deviceIdToDelete = deviceInstance.Device.DeviceId;
                    }
                }

                // 从树中移除节点
                TreeNodeViewModel parent = null;
                int index = -1;

                foreach (var rootNode in TreeNodes)
                {
                    if (FindNodeAndParent(rootNode, node, ref parent, ref index))
                    {
                        break;
                    }
                }

                // 如果删除的是当前选中的节点，需要在删除前确定下一个选中的节点
                bool isSelectedNode = ReferenceEquals(SelectedNode, node);
                TreeNodeViewModel nextSelectedNode = null;

                if (isSelectedNode && parent != null && index >= 0)
                {
                    // 在删除前，尝试选择相邻节点
                    var children = parent.Children;
                    
                    // 优先选择上一个节点（向上移动）
                    if (index > 0)
                    {
                        nextSelectedNode = children[index - 1];
                    }
                    // 如果没有上一个节点，选择下一个节点（向下移动）
                    else if (index < children.Count - 1)
                    {
                        // 注意：删除后，index+1 位置的节点会移动到 index 位置
                        nextSelectedNode = children[index + 1];
                    }
                    // 如果上下都没有节点，nextSelectedNode 保持为 null
                }
                else if (isSelectedNode && index >= 0)
                {
                    // 删除的是根节点
                    // 优先选择上一个节点（向上移动）
                    if (index > 0)
                    {
                        nextSelectedNode = TreeNodes[index - 1];
                    }
                    // 如果没有上一个节点，选择下一个节点（向下移动）
                    else if (index < TreeNodes.Count - 1)
                    {
                        // 注意：删除后，index+1 位置的节点会移动到 index 位置
                        nextSelectedNode = TreeNodes[index + 1];
                    }
                    // 如果上下都没有节点，nextSelectedNode 保持为 null
                }

                // 执行删除操作
                if (parent != null && index >= 0)
                {
                    // 从父节点的子节点集合中移除
                    parent.Children.RemoveAt(index);
                }
                else if (index >= 0)
                {
                    // 从根节点集合中移除
                    TreeNodes.RemoveAt(index);
                }

                // 从 ConfigurationManager 注销配置
                if (!string.IsNullOrWhiteSpace(deviceIdToDelete) && _configurationManager != null)
                {
                    var unregisterResult = _configurationManager.UnregisterConfig(deviceIdToDelete);
                    if (!unregisterResult.Success)
                    {
                        System.Diagnostics.Debug.WriteLine($"从 ConfigurationManager 注销配置失败: {unregisterResult.ErrorMessage}");
                    }

                    // 如果设备已注册到设备管理器，记录到待删除列表（保存时才真正注销）
                    if (_deviceManager != null && _deviceManager.DeviceExists(deviceIdToDelete))
                    {
                        _pendingDeviceUnregisters.Add(deviceIdToDelete);
                    }
                }

                // 如果删除的是当前选中的节点，选择下一个节点
                if (isSelectedNode)
                {
                    if (nextSelectedNode != null)
                    {
                        // 选择相邻节点
                        SelectedNode = nextSelectedNode;
                        
                        // 触发节点选择命令，加载对应的配置界面
                        NodeSelected(nextSelectedNode);
                       
                    }
                    else
                    {
                        // 如果没有相邻节点，清除选中状态
                        SelectedNode = null;

                        if (_configContentRegion != null)
                        {
                            _configContentRegion.Content = null;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"删除节点时发生错误: {ex.Message}");
                MessageBoxHelper.ShowError($"无法删除节点: {ex.Message}", "错误");
            }
        }

        /// <summary>
        /// 保存配置命令
        /// </summary>
        [RelayCommand]
        private void SaveConfigurations()
        {
            try
            {
                if (_configurationManager == null)
                {
                    MessageBoxHelper.ShowError("配置管理器未初始化", "错误");
                    return;
                }

                var successCount = 0;
                var errorCount = 0;
                var errors = new List<string>();

            // 1. 处理待删除的设备（从设备管理器注销）
            foreach (var deviceId in _pendingDeviceUnregisters.ToList())
            {
                if (_deviceManager != null)
                {
                    var result = _deviceManager.UnregisterDevice(deviceId);
                    if (result.Success)
                    {
                        successCount++;
                        System.Diagnostics.Debug.WriteLine($"[ConfigViewModel] 设备 {deviceId} 已从设备管理器注销");
                    }
                    else
                    {
                        errorCount++;
                        errors.Add($"注销设备 {deviceId} 失败: {result.ErrorMessage}");
                        System.Diagnostics.Debug.WriteLine($"[ConfigViewModel] 注销设备 {deviceId} 失败: {result.ErrorMessage}");
                    }
                }
            }

            // 清空待删除列表
            _pendingDeviceUnregisters.Clear();

            // 2. 将 ConfigurationManager 中的配置应用到已注册的设备（如果设备存在）
                // 按照树节点的顺序获取配置（保持子节点顺序）
                var allDeviceConfigs = GetDeviceConfigsInTreeOrder();

                foreach (var config in allDeviceConfigs)
                {
                    if (_deviceManager == null)
                        continue;

                    // 检查设备是否已注册
                    if (_deviceManager.DeviceExists(config.DeviceId))
                    {
                        // 设备已存在，检查是否需要更新配置
                        var deviceResult = _deviceManager.GetDevice(config.DeviceId);
                        if (deviceResult.Success && deviceResult.Data != null)
                        {
                            // 尝试应用配置到设备
                            var device = deviceResult.Data;
                            
                            // 使用反射查找 IConfigurable<TConfig> 接口
                            var configurableInterface = device.GetType().GetInterfaces()
                                .FirstOrDefault(i => i.IsGenericType &&
                                                     i.GetGenericTypeDefinition() == typeof(IConfigurable<>));
                            
                            if (configurableInterface != null)
                            {
                                try
                                {
                                    // 通过反射调用 ApplyConfig 方法
                                    var applyConfigMethod = configurableInterface.GetMethod("ApplyConfig");

                                    if (applyConfigMethod != null)
                                    {
                                        var applyResult = applyConfigMethod.Invoke(device, new object[] { config }) as OperationResult;
                                        if (applyResult != null && !applyResult.Success)
                                        {
                                            errorCount++;
                                            errors.Add($"应用配置到设备 {config.DeviceId} 失败: {applyResult.ErrorMessage}");
                                        }
                                        else
                                        {
                                            successCount++;
                                            System.Diagnostics.Debug.WriteLine($"[ConfigViewModel] 设备 {config.DeviceId} 配置已应用");
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    errorCount++;
                                    errors.Add($"应用配置到设备 {config.DeviceId} 时发生异常: {ex.Message}");
                                    System.Diagnostics.Debug.WriteLine($"[ConfigViewModel] 应用配置到设备 {config.DeviceId} 时发生异常: {ex.Message}");
                                }
                            }
                            else
                            {
                                System.Diagnostics.Debug.WriteLine($"[ConfigViewModel] 设备 {config.DeviceId} 不支持配置接口，跳过配置应用");
                            }
                        }
                    }
                    else
                    {
                        // 设备不存在，需要根据配置创建设备
                        try
                        {
                            var device = CreateDeviceFromConfig(config);
                            if (device != null)
                            {
                                // 注册设备到设备管理器
                                var registerResult = _deviceManager.RegisterDevice(device);
                                if (registerResult.Success)
                                {
                                    successCount++;
                                    System.Diagnostics.Debug.WriteLine($"[ConfigViewModel] 设备 {config.DeviceId} 已创建并注册");
                                }
                                else
                                {
                                    errorCount++;
                                    errors.Add($"创建设备 {config.DeviceId} 失败: {registerResult.ErrorMessage}");
                                    System.Diagnostics.Debug.WriteLine($"[ConfigViewModel] 创建设备 {config.DeviceId} 失败: {registerResult.ErrorMessage}");
                                }
                            }
                            else
                            {
                                errorCount++;
                                errors.Add($"无法为配置 {config.DeviceId} 创建设备：找不到对应的设备类");
                                System.Diagnostics.Debug.WriteLine($"[ConfigViewModel] 无法为配置 {config.DeviceId} 创建设备：找不到对应的设备类");
                            }
                        }
                        catch (Exception ex)
                        {
                            errorCount++;
                            errors.Add($"创建设备 {config.DeviceId} 时发生异常: {ex.Message}");
                            System.Diagnostics.Debug.WriteLine($"[ConfigViewModel] 创建设备 {config.DeviceId} 时发生异常: {ex.Message}");
                        }
                    }
                }

                // 3. 保存配置文件（按设备类型分组保存）
                try
                {
                    SaveConfigFiles(allDeviceConfigs);
                }
                catch (Exception ex)
                {
                    errorCount++;
                    errors.Add($"保存配置文件失败: {ex.Message}");
                    System.Diagnostics.Debug.WriteLine($"[ConfigViewModel] 保存配置文件失败: {ex.Message}");
                }

                // 4. 显示保存结果
                if (errorCount == 0)
                {
                    ToastHelper.ShowSuccess($"配置保存成功，成功处理 {successCount} 项", "保存成功");
                    System.Diagnostics.Debug.WriteLine($"[ConfigViewModel] 配置保存成功，处理了 {successCount} 项");
                }
                else
                {
                    var errorMessage = $"配置保存完成，但有 {errorCount} 项失败：" + string.Join("；", errors.Take(3));
                    if (errors.Count > 3)
                    {
                        errorMessage += $"等共 {errors.Count} 项错误";
                    }
                    ToastHelper.ShowError(errorMessage, "保存完成（部分失败）");
                }

                // 5. 刷新配置树
                InitializeConfigTree();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"保存配置时发生错误: {ex.Message}");
                ToastHelper.ShowError($"保存配置失败: {ex.Message}", "错误");
            }
        }

        /// <summary>
        /// 保存配置文件（按设备类型分组保存）
        /// </summary>
        private void SaveConfigFiles(List<DeviceConfig> allDeviceConfigs)
        {
            if (allDeviceConfigs == null || allDeviceConfigs.Count == 0)
                return;

            // 按设备类型分组
            var configsByType = allDeviceConfigs.GroupBy(c => c.Type).ToList();

            foreach (var group in configsByType)
            {
                var deviceType = group.Key;
                // 按创建时间排序，确保保存顺序稳定（与树节点顺序一致）
                var configs = group.OrderBy(c => c.CreatedAt).ThenBy(c => c.DeviceName).ToList();

                try
                {
                    // 优先从 ConfigurationManager 获取配置文件路径
                    string configFilePath = null;
                    
                    // 尝试从第一个配置获取已注册的路径
                    var firstConfig = configs.First();
                    if (_configurationManager != null)
                    {
                        // 先尝试根据配置类型获取路径
                        configFilePath = _configurationManager.GetConfigFilePathByType(firstConfig.ConfigType);
                        
                        // 如果根据类型没找到，尝试根据 ConfigId 获取（同一类型配置应该使用同一个文件）
                        if (string.IsNullOrEmpty(configFilePath))
                        {
                            configFilePath = _configurationManager.GetConfigFilePath(firstConfig.ConfigId);
                        }
                    }
                    
                    // 如果没有找到已注册的路径，使用查找逻辑
                    if (string.IsNullOrEmpty(configFilePath))
                    {
                        configFilePath = GetConfigFilePath(deviceType, configs.First().GetType());
                        
                        // 如果找到了路径，将路径注册到 ConfigurationManager（方便下次使用）
                        if (!string.IsNullOrEmpty(configFilePath) && _configurationManager != null)
                        {
                            // 为该类型的所有配置注册路径
                            foreach (var config in configs)
                            {
                                _configurationManager.SetConfigFilePath(config.ConfigId, configFilePath);
                            }
                        }
                    }
                    
                    if (string.IsNullOrEmpty(configFilePath))
                    {
                        System.Diagnostics.Debug.WriteLine($"[ConfigViewModel] 无法确定设备类型 {deviceType} 的配置文件路径，跳过保存");
                        continue;
                    }

                    // 确保目录存在
                    var configDir = Path.GetDirectoryName(configFilePath);
                    if (!string.IsNullOrEmpty(configDir) && !Directory.Exists(configDir))
                    {
                        Directory.CreateDirectory(configDir);
                    }

                    // 获取具体的配置类型
                    var concreteConfigType = configs.First().GetType();
                    
                    // 使用反射创建泛型类型 DeviceConfigData<TConfig>
                    var configDataGenericType = typeof(DeviceConfigData<>).MakeGenericType(concreteConfigType);
                    var configData = Activator.CreateInstance(configDataGenericType);
                    var configsProperty = configDataGenericType.GetProperty("Configs");
                    
                    // 创建具体类型的列表，并将配置转换为具体类型
                    var concreteListType = typeof(List<>).MakeGenericType(concreteConfigType);
                    var concreteList = Activator.CreateInstance(concreteListType);
                    var addMethod = concreteListType.GetMethod("Add");
                    
                    foreach (var config in configs)
                    {
                        // 将 DeviceConfig 转换为具体类型并添加到列表
                        addMethod?.Invoke(concreteList, new[] { config });
                    }
                    
                    // 设置配置列表属性
                    configsProperty?.SetValue(configData, concreteList);

                    // 序列化为 JSON 并保存
                    var jsonOptions = new JsonSerializerOptions
                    {
                        WriteIndented = true,                    
                        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping // 不转义中文字符，直接显示中文
                    };
                    var json = JsonSerializer.Serialize(configData, configDataGenericType, jsonOptions);
                    File.WriteAllText(configFilePath, json, System.Text.Encoding.UTF8);

                    System.Diagnostics.Debug.WriteLine($"[ConfigViewModel] 配置文件已保存: {configFilePath}，包含 {configs.Count} 个配置");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[ConfigViewModel] 保存设备类型 {deviceType} 的配置文件失败: {ex.Message}");
                    throw;
                }
            }
        }

        /// <summary>
        /// 获取配置文件路径（根据设备类型）
        /// 配置文件路径：Bin/Debug/Configs/Devices/{插件名}.config.json
        /// </summary>
        private string GetConfigFilePath(Astra.Core.Devices.DeviceType deviceType, Type configType)
        {
            // 获取插件名称（从配置类型的程序集获取）
            var assembly = configType.Assembly;
            var assemblyName = assembly.GetName().Name;
            var configFileName = $"{assemblyName}.config.json";

            // 配置文件路径：Bin/Debug/Configs/Devices/{插件名}.config.json
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var configsDevicesDir = Path.Combine(baseDir, "Configs", "Devices");
            var configPath = Path.Combine(configsDevicesDir, configFileName);

            // 如果从插件宿主中找到了对应插件，也可以使用插件名称
            if (_pluginHost != null)
            {
                foreach (var plugin in _pluginHost.LoadedPlugins)
                {
                    var pluginType = plugin.GetType();
                    var pluginAssembly = pluginType.Assembly;

                    // 检查插件是否包含该配置类型
                    var configTypes = pluginAssembly.GetTypes()
                        .Where(t => !t.IsAbstract && !t.IsInterface && typeof(DeviceConfig).IsAssignableFrom(t))
                        .ToList();

                    if (configTypes.Contains(configType))
                    {
                        var pluginName = pluginAssembly.GetName().Name;
                        configFileName = $"{pluginName}.config.json";
                        configPath = Path.Combine(configsDevicesDir, configFileName);
                        break;
                    }
                }
            }

            return configPath;
        }

        /// <summary>
        /// 根据配置创建设备实例
        /// </summary>
        private IDevice CreateDeviceFromConfig(DeviceConfig config)
        {
            if (config == null)
                return null;

            try
            {
                // 根据配置类型找到对应的设备类
                var configType = config.GetType();
                var deviceType = FindDeviceTypeForConfig(configType);
                
                if (deviceType == null)
                {
                    System.Diagnostics.Debug.WriteLine($"[ConfigViewModel] 无法找到配置类型 {configType.Name} 对应的设备类");
                    return null;
                }

                // 获取必要的服务
                var messageBus = _serviceProvider?.GetService<IMessageBus>();
                var logger = _serviceProvider?.GetService<ILogger>();

                // 使用反射创建设备实例
                IDevice device = null;

                // 尝试不同的构造函数签名
                var constructors = deviceType.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
                
                foreach (var ctor in constructors)
                {
                    var parameters = ctor.GetParameters();
                    var paramValues = new List<object>();

                    if (parameters.Length == 0)
                    {
                        // 无参构造函数
                        device = (IDevice)Activator.CreateInstance(deviceType);
                        break;
                    }
                    else if (parameters.Length == 1 && parameters[0].ParameterType.IsAssignableFrom(configType))
                    {
                        // 单参数：config
                        device = (IDevice)Activator.CreateInstance(deviceType, config);
                        break;
                    }
                    else if (parameters.Length == 2 && 
                             parameters[0].ParameterType.IsAssignableFrom(configType) &&
                             parameters[1].ParameterType == typeof(IMessageBus))
                    {
                        // 双参数：config, messageBus
                        device = (IDevice)Activator.CreateInstance(deviceType, config, messageBus);
                        break;
                    }
                    else if (parameters.Length == 3 &&
                             parameters[0].ParameterType.IsAssignableFrom(configType) &&
                             parameters[1].ParameterType == typeof(IMessageBus) &&
                             parameters[2].ParameterType == typeof(ILogger))
                    {
                        // 三参数：config, messageBus, logger
                        device = (IDevice)Activator.CreateInstance(deviceType, config, messageBus, logger);
                        break;
                    }
                }

                if (device == null)
                {
                    System.Diagnostics.Debug.WriteLine($"[ConfigViewModel] 无法为设备类型 {deviceType.Name} 找到合适的构造函数");
                }

                return device;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ConfigViewModel] 创建设备实例时发生异常: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 根据配置类型找到对应的设备类
        /// </summary>
        private Type FindDeviceTypeForConfig(Type configType)
        {
            // 命名约定：DataAcquisitionConfig -> DataAcquisitionDevice
            var configName = configType.Name;
            if (configName.EndsWith("Config"))
            {
                var deviceName = configName.Substring(0, configName.Length - 6) + "Device";
                
                // 在配置类型的程序集中查找设备类
                var assembly = configType.Assembly;
                var deviceType = assembly.GetType($"{configType.Namespace}.{deviceName}");

                if (deviceType != null && typeof(IDevice).IsAssignableFrom(deviceType))
                {
                    return deviceType;
                }

                // 如果在同一命名空间找不到，尝试在整个程序集中查找
                deviceType = assembly.GetTypes()
                    .FirstOrDefault(t => !t.IsAbstract && 
                                        !t.IsInterface && 
                                        t.Name == deviceName && 
                                        typeof(IDevice).IsAssignableFrom(t));

                if (deviceType != null)
                {
                    return deviceType;
                }
            }

            // 如果命名约定不匹配，尝试扫描所有已加载的程序集
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    var deviceType = assembly.GetTypes()
                        .FirstOrDefault(t => !t.IsAbstract &&
                                            !t.IsInterface &&
                                            typeof(IDevice).IsAssignableFrom(t) &&
                                            IsDeviceForConfig(t, configType));

                    if (deviceType != null)
                    {
                        return deviceType;
                    }
                }
                catch
                {
                    // 忽略无法加载的程序集
                }
            }

            return null;
        }

        /// <summary>
        /// 检查设备类型是否对应指定的配置类型
        /// </summary>
        private bool IsDeviceForConfig(Type deviceType, Type configType)
        {
            // 检查设备类型是否实现了 DeviceBase<TConfig>，其中 TConfig 是指定的配置类型
            var baseType = deviceType.BaseType;
            while (baseType != null && baseType != typeof(object))
            {
                if (baseType.IsGenericType)
                {
                    var genericTypeDef = baseType.GetGenericTypeDefinition();
                    if (genericTypeDef.Name == "DeviceBase`1")
                    {
                        var genericArgs = baseType.GetGenericArguments();
                        if (genericArgs.Length == 1 && genericArgs[0] == configType)
                        {
                            return true;
                        }
                    }
                }
                baseType = baseType.BaseType;
            }

            return false;
        }

        /// <summary>
        /// 配置文件数据包装类（用于 JSON 序列化）
        /// </summary>
        private class DeviceConfigData<TConfig> where TConfig : DeviceConfig
        {
            public List<TConfig> Configs { get; set; } = new List<TConfig>();
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
