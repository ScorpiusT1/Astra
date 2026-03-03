using System;
using System.Collections.ObjectModel;
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

            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                BuildDebugTree();
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

                var configType = configurableInterface?.GetGenericArguments()[0];
                var treeAttr = configType?.GetCustomAttribute<TreeNodeConfigAttribute>();

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

                // 设备显示名称：优先使用当前配置的 ConfigName（与配置树子节点名称保持一致），其次 DisplayName，再次 DeviceName/DeviceId
                string header = device.DeviceName;

                if (configurableInterface != null)
                {
                    try
                    {
                        // 通过 IConfigurable<TConfig>.CurrentConfig 反射获取当前配置实例
                        var currentConfigProp = configurableInterface.GetProperty("CurrentConfig");
                        var currentConfig = currentConfigProp?.GetValue(device);

                        if (currentConfig is ConfigBase configBase)
                        {
                            // 1. 优先使用 ConfigName（配置树子节点名称基于此字段生成并持久化）
                            if (!string.IsNullOrWhiteSpace(configBase.ConfigName))
                            {
                                header = configBase.ConfigName;
                            }
                            else
                            {
                                // 2. 没有 ConfigName 时退回到 GetDisplayName（包含厂商/型号等信息）
                                header = configBase.GetDisplayName();
                            }
                        }
                    }
                    catch
                    {
                        // 忽略获取配置时的异常，回退到 DeviceName / DeviceId
                        header = string.IsNullOrWhiteSpace(device.DeviceName)
                            ? device.DeviceId
                            : device.DeviceName;
                    }
                }

                if (string.IsNullOrWhiteSpace(header))
                {
                    header = device.DeviceId;
                }

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
            }

            // 根节点排序后加入集合
            foreach (var root in rootNodes.Values
                         .OrderBy(r => r.Order)
                         .ThenBy(r => r.Header))
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
    }
}
