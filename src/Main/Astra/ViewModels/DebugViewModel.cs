using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Windows.Controls;
using Astra.Core.Devices.Interfaces;
using Astra.Core.Devices.Management;
using Astra.Core.Configuration.Base;
using Astra.Core.Plugins.UI;
using Astra.Models;
using Astra.UI.Abstractions.Attributes;
using Astra.UI.Helpers;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Astra.ViewModels
{
    /// <summary>
    /// 调试视图模型：左侧设备调试树 + 右侧设备调试界面
    /// </summary>
    public partial class DebugViewModel : ObservableObject
    {
        private readonly IDeviceManager? _deviceManager;
        private readonly IPluginViewFactory? _pluginViewFactory;
        private readonly string _defaultIcon = "🧩";
        private readonly Dictionary<string, TreeNode> _deviceNodeMap = new Dictionary<string, TreeNode>();
        private static readonly CompareInfo ZhCompare = CultureInfo.GetCultureInfo("zh-CN").CompareInfo;

        [ObservableProperty]
        private string _title = "调试工具";

        [ObservableProperty]
        private ObservableCollection<TreeNode> _debugTreeNodes = new ObservableCollection<TreeNode>();

        [ObservableProperty]
        private TreeNode? _selectedNode;

        /// <summary>
        /// 右侧内容区变化事件（由 View 订阅并更新 ContentControl）
        /// </summary>
        public event EventHandler<Control?>? ContentControlChanged;

        private bool _isSelectingNode;

        /// <summary>
        /// 无参构造（设计时或未通过 DI 创建时使用，从 App.ServiceProvider 解析服务）
        /// </summary>
        public DebugViewModel()
        {
            var sp = App.ServiceProvider;
            _deviceManager = sp?.GetService<IDeviceManager>();
            _pluginViewFactory = sp?.GetService<IPluginViewFactory>();
            SubscribeDeviceStatusChanged();

            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                BuildDebugTree();
            });
        }

        /// <summary>
        /// 带依赖注入的构造函数（供 DI 使用）
        /// </summary>
        public DebugViewModel(IDeviceManager deviceManager, IPluginViewFactory pluginViewFactory)
        {
            _deviceManager = deviceManager;
            _pluginViewFactory = pluginViewFactory;
            SubscribeDeviceStatusChanged();

            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                BuildDebugTree();
            });
        }

        private void SubscribeDeviceStatusChanged()
        {
            if (_deviceManager == null)
            {
                return;
            }

            _deviceManager.DeviceStatusChanged -= OnDeviceStatusChanged;
            _deviceManager.DeviceStatusChanged += OnDeviceStatusChanged;
        }

        private void OnDeviceStatusChanged(object? sender, Astra.Core.Devices.DeviceStatusChangedEventArgs e)
        {
            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher == null)
            {
                return;
            }

            dispatcher.InvokeAsync(() =>
            {
                if (string.IsNullOrWhiteSpace(e?.Id))
                {
                    return;
                }

                if (_deviceNodeMap.TryGetValue(e.Id, out var node))
                {
                    node.IsOnline = e.NewStatus == Astra.Core.Devices.DeviceStatus.Online;
                }
            });
        }

        /// <summary>
        /// 供视图在加载时或需要时主动刷新调试树
        /// </summary>
        public void RefreshDebugTree()
        {
            BuildDebugTree();
        }

        /// <summary>
        /// 根据当前已注册设备构建调试树
        /// </summary>
        private void BuildDebugTree()
        {
            DebugTreeNodes.Clear();
            _deviceNodeMap.Clear();

            if (_deviceManager == null)
            {
                return;
            }

            var result = _deviceManager.GetAllDevices();
            if (result == null || !result.Success || result.Data == null)
            {
                return;
            }

            var devices = result.Data;
            if (devices.Count == 0)
            {
                return;
            }

            // 根节点按配置类型的 TreeNodeConfig 分类组织，使分组与配置界面一致
            var rootNodes = new System.Collections.Generic.Dictionary<string, TreeNode>();

            foreach (var device in devices)
            {
                var deviceType = device.GetType();

                // 查找设备实现的 IConfigurable<TConfig> 接口，以获取配置类型
                var configurableInterface = deviceType
                    .GetInterfaces()
                    .FirstOrDefault(i =>
                        i.IsGenericType &&
                        i.GetGenericTypeDefinition() == typeof(IConfigurable<>));

                // ⚠️ 关键：优先使用 CurrentConfig 的运行时类型分组，避免 S7 设备因基类泛型为 PlcDeviceConfig 被误归类到“通用PLC”
                var declaredConfigType = configurableInterface?.GetGenericArguments()[0];
                Type? runtimeConfigType = null;
                object? currentConfigObj = null;
                if (configurableInterface != null)
                {
                    try
                    {
                        var currentConfigProp = configurableInterface.GetProperty("CurrentConfig");
                        currentConfigObj = currentConfigProp?.GetValue(device);
                        runtimeConfigType = currentConfigObj?.GetType();
                    }
                    catch
                    {
                        // 回退到声明类型
                    }
                }

                var configType = runtimeConfigType ?? declaredConfigType;
                var treeAttr = configType?.GetCustomAttribute<TreeNodeConfigAttribute>(inherit: false);

                var category = treeAttr?.Category ?? "未分类设备";
                var icon = treeAttr?.Icon ?? _defaultIcon;
                var order = treeAttr?.Order ?? 0;

                if (!rootNodes.TryGetValue(category, out var root))
                {
                    root = new TreeNode
                    {
                        Header = category,
                        Icon = icon,
                        ShowAddButton = false,
                        ShowDeleteButton = false,
                        Config = null,
                        ConfigType = null,
                        Parent = null,
                        Order = order
                    };

                    rootNodes[category] = root;
                }

                // 设备显示名称：优先配置名；配置名为空时回退到中文友好名称，避免暴露英文类型名。
                string header = ResolveDeviceHeader(device, currentConfigObj, treeAttr, category);

                var child = new TreeNode
                {
                    Header = header,
                    Icon = icon,
                    ShowAddButton = false,
                    ShowDeleteButton = false,
                    Config = null,
                    // 对于调试树，ConfigType 存放对应的配置类型（与配置树保持一致）
                    ConfigType = configType,
                    Parent = root,
                    Order = order,
                    Tag = device,
                    // 在线状态用于调试树状态指示灯
                    IsOnline = device.IsOnline
                };

                root.Children.Add(child);
                if (!string.IsNullOrWhiteSpace(device.DeviceId))
                {
                    _deviceNodeMap[device.DeviceId] = child;
                }
            }

            // 根节点排序后加入集合
            foreach (var root in rootNodes.Values
                         .OrderBy(r => r.Order)
                         .ThenBy(r => r.Header, Comparer<string>.Create((a, b) => ZhCompare.Compare(a ?? string.Empty, b ?? string.Empty, CompareOptions.StringSort))))
            {
                DebugTreeNodes.Add(root);
                // 默认展开所有根节点
                root.IsExpanded = true;
            }

            // 自动选中第一个设备节点并加载其调试界面
            var firstLeaf = Utilities.TreeNodeHelper.GetFirstLeafNode(DebugTreeNodes);
            if (firstLeaf != null)
            {
                NodeSelected(firstLeaf);
            }
        }

        /// <summary>
        /// 调试树节点选择命令
        /// </summary>
        [RelayCommand]
        private void NodeSelected(TreeNode? node)
        {
            if (node == null)
                return;

            if (_isSelectingNode)
                return;

            _isSelectingNode = true;
            try
            {
                // 清除现有选中状态
                foreach (var root in DebugTreeNodes)
                {
                    ClearSelection(root);
                }

                SelectedNode = node;
                node.IsSelected = true;

                LoadDebugView(node);
            }
            finally
            {
                _isSelectingNode = false;
            }
        }

        private void ClearSelection(TreeNode node)
        {
            node.IsSelected = false;
            if (node.Children != null)
            {
                foreach (var child in node.Children)
                {
                    ClearSelection(child);
                }
            }
        }

        /// <summary>
        /// 根据选中节点加载对应设备的调试界面
        /// </summary>
        private void LoadDebugView(TreeNode node)
        {
            if (_pluginViewFactory == null)
            {
                ToastHelper.ShowError("插件视图工厂未注册，无法加载调试界面");
                return;
            }

            if (node.Tag is not IDevice device)
            {
                // 非设备节点（分类节点），清空右侧内容
                //ContentControlChanged?.Invoke(this, null);
                return;
            }

            try
            {
                var (view, viewModel) = _pluginViewFactory.CreateDebugViewForDevice(device);
                if (view is not UserControl control)
                {
                    ToastHelper.ShowError($"创建调试界面失败: {device.DeviceName}");
                    ContentControlChanged?.Invoke(this, null);
                    return;
                }

                ContentControlChanged?.Invoke(this, control);
            }
            catch (Exception ex)
            {
                ToastHelper.ShowError($"加载调试界面时发生错误: {ex.Message}");
                ContentControlChanged?.Invoke(this, null);
            }
        }

        /// <summary>
        /// 构建设备节点显示名：优先使用配置名，其次中文友好回退。
        /// </summary>
        private string ResolveDeviceHeader(IDevice device, object? currentConfigObj, TreeNodeConfigAttribute? treeAttr, string category)
        {
            // 1) 配置名（配置界面保存后的业务名称）
            if (currentConfigObj is ConfigBase cfg && !string.IsNullOrWhiteSpace(cfg.ConfigName))
            {
                return cfg.ConfigName;
            }

            // 2) 设备名（通常由业务层赋值，优先于类型显示名）
            if (!string.IsNullOrWhiteSpace(device.DeviceName))
            {
                return device.DeviceName;
            }

            // 3) 尝试 GetDisplayName（若明显是英文类型名则忽略）
            if (currentConfigObj is ConfigBase configBase)
            {
                var displayName = configBase.GetDisplayName();
                if (!string.IsNullOrWhiteSpace(displayName) && !LooksLikeTypeName(displayName))
                {
                    return displayName;
                }
            }

            // 4) 配置特性 Header（中文友好兜底）
            if (!string.IsNullOrWhiteSpace(treeAttr?.Header))
            {
                return treeAttr.Header!;
            }

            // 5) 分类名兜底，避免暴露英文类型
            if (!string.IsNullOrWhiteSpace(category))
            {
                return $"{category}设备";
            }

            // 6) 最后回退到设备ID
            return string.IsNullOrWhiteSpace(device.DeviceId) ? "未命名设备" : device.DeviceId;
        }

        private bool LooksLikeTypeName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            var text = name.Trim();
            if (text.EndsWith("Config", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // 全ASCII且无空格时，通常是类型名（如 SiemensPlcConfig）
            bool allAscii = text.All(c => c <= 127);
            return allAscii && !text.Contains(' ');
        }
    }
}
