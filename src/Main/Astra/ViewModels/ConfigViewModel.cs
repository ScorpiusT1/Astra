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
using Astra.Models;
using Astra.UI.Abstractions.Attributes;
using Astra.Utilities;
using System.Threading.Tasks;

namespace Astra.ViewModels
{
    public partial class ConfigViewModel : ObservableObject
    {
        private readonly IServiceProvider _serviceProvider;
        private string _defaultIcon = "📁";

        private readonly IDeviceManager _deviceManager;
        private readonly IPluginHost _pluginHost;

        private readonly IConfigurationManager? _configManager;

        [ObservableProperty]
        private string _title = "配置管理";

        [ObservableProperty]
        private ObservableCollection<TreeNode> _treeNodes = new ObservableCollection<TreeNode>();

        [ObservableProperty]
        private TreeNode _selectedNode;

        // 待删除的设备ID列表（点击保存时才从设备管理器注销）
        private readonly HashSet<string> _pendingDeviceUnregisters = new HashSet<string>();


        public event EventHandler<Control?>? ContentControlChanged;


        /// <summary>
        /// 递归清除节点的选中状态
        /// </summary>
        private void ClearNodeSelection(TreeNode node)
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

            _deviceManager = _serviceProvider?.GetService<IDeviceManager>();

            _configManager = _serviceProvider?.GetService<IConfigurationManager>();

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

            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                InitializeConfigTree();
            });
        }

        // 防止配置树刷新时的递归调用标志
        private bool _isRefreshingTree = false;



        /// <summary>
        /// 初始化配置树
        /// </summary>
        private async Task InitializeConfigTree()
        {
            // 如果正在刷新，跳过（避免递归）
            if (_isRefreshingTree)
            {
                System.Diagnostics.Debug.WriteLine("[ConfigViewModel] InitializeConfigTree: 正在刷新中，跳过");
                return;
            }

            _isRefreshingTree = true;

            TreeNodes.Clear();

            try
            {
                // 按设备类型分组构建树（配置来源：ConfigurationManager）
                await BuildConfigTree();

                ExpandAllNodes();

                TreeNode? selectedNode = TreeNodeHelper.GetFirstLeafNode(TreeNodes);

                NodeSelected(selectedNode);
            }
            finally
            {
                _isRefreshingTree = false;
            }
        }


        /// <summary>
        /// 构建配置树（基于 ConfigurationManager 中的配置）
        /// </summary>
        private async Task BuildConfigTree()
        {
            var result = await _configManager?.GetAllConfigsAsync();

            if (result == null || result.Data == null)
            {
                return;
            }

            TreeNodes?.Clear();

            // 使用字典跟踪所有根节点，避免重复处理
            Dictionary<string, TreeNode> rootNodes = new Dictionary<string, TreeNode>();

            foreach (var config in result.Data)
            {
                Type cfgType = config.GetType();

                TreeNodeConfigAttribute? attr = cfgType.GetCustomAttribute<TreeNodeConfigAttribute>();

                if (attr == null)
                {
                    continue;
                }

                // 获取或创建根节点
                if (!rootNodes.TryGetValue(attr.Category, out TreeNode? rootNode))
                {
                    rootNode = GetTreeNode(attr.Category);

                    // 首次创建时设置根节点属性
                    rootNode.ShowAddButton = true;
                    rootNode.ShowDeleteButton = false;
                    rootNode.Config = null;

                    rootNodes[attr.Category] = rootNode;
                }

                // 创建子节点
                TreeNode childNode = new TreeNode
                {
                    Header = config.ConfigName,
                    Icon = attr.Icon ?? _defaultIcon,
                    ViewModelType = attr.ViewModelType,
                    ViewType = attr.ViewType,
                    ShowAddButton = false,
                    ShowDeleteButton = true,
                    Config = config,
                    Order = attr.Order,
                    ConfigType = cfgType,
                    Parent = rootNode,
                };

                // 如果根节点还没有图标，使用第一个子节点的图标
                if (rootNode.Icon == null || rootNode.Icon == _defaultIcon)
                {
                    rootNode.Icon = childNode.Icon;
                }

                // 如果根节点还没有ConfigType，使用第一个子节点的类型
                if (rootNode.ConfigType == null)
                {
                    rootNode.ConfigType = cfgType;
                }

                rootNode.Children.Add(childNode);
            }

            // 添加所有非空的根节点
            foreach (var rootNode in rootNodes.Values)
            {
                if (rootNode.Children.Count > 0)
                {
                    TreeNodes.Add(rootNode);
                }
            }
        }

        private TreeNode GetTreeNode(string category)
        {
            if (string.IsNullOrEmpty(category))
            {
                return new TreeNode
                {
                    Header = "未分类",
                    Icon = _defaultIcon,
                };
            }

            TreeNode? node = TreeNodes.FirstOrDefault(n => n.Header == category);

            if (node == null)
            {
                node = new TreeNode
                {
                    Header = category,
                    Icon = _defaultIcon,
                };
            }

            return node;
        }

        private void ExpandNode(TreeNode node)
        {
            if (node == null)
                return;

            node.IsExpanded = true;

            if (node.Children != null)
            {
                foreach (var child in node.Children)
                {
                    ExpandNode(child);
                }
            }
        }

        private void ExpandAllNodes()
        {
            foreach (var rootNode in TreeNodes)
            {
                ExpandNode(rootNode);
            }
        }


        /// <summary>
        /// 节点选择命令
        /// </summary>
        [RelayCommand]
        private void NodeSelected(TreeNode? node)
        {
            if (node == null)
                return;

            SelectedNode = node;

            LoadConfigView(node);
        }

        /// <summary>
        /// 加载配置界面
        /// </summary>
        private void LoadConfigView(TreeNode node)
        {
            if (node == null)
                return;

            try
            {
                UserControl? configView = null;

                if (node.ViewType != null)
                {
                    configView = Activator.CreateInstance(node.ViewType) as UserControl;
                }

                if (configView == null || node.ViewModelType == null)
                {
                    return;
                }

                var viewModel = Activator.CreateInstance(node.ViewModelType, node.Config);

                if (viewModel == null)
                {
                    return;
                }

                configView.DataContext = viewModel;

                if (configView == null)
                {
                    return;
                }

                ContentControlChanged?.Invoke(this, configView);
            }
            catch (Exception ex)
            {
                ToastHelper.ShowError($"加载配置界面时发生错误: {ex.Message}");

                ContentControl control = new ContentControl()
                {
                    Content = new TextBlock
                    {
                        Text = $"无法加载配置界面: {ex.Message}",
                        Margin = new Thickness(20)
                    }
                };
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

                var sourceNode = sourceProperty.GetValue(parameter) as TreeNode;
                var targetNode = targetProperty.GetValue(parameter) as TreeNode;

                if (sourceNode == null || targetNode == null)
                    return;

                // 如果源节点和目标节点相同，不做任何操作
                if (sourceNode == targetNode)
                    return;

                // 查找源节点和目标节点的父节点
                TreeNode sourceParent = null;
                TreeNode targetParent = null;
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
        private bool FindNodeAndParent(TreeNode root, TreeNode target, ref TreeNode? parent, ref int index)
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


        private TreeNode? CreateNode(TreeNode node)
        {
            if (node == null || node.ConfigType == null)
                return null;

            var attr = node.ConfigType?.GetCustomAttribute<TreeNodeConfigAttribute>();

            if (attr == null)
            {
                return null;
            }

            NodeAutoNaming nodeAutoNaming = new NodeAutoNaming();

            TreeNode newNode = new TreeNode
            {
                Header = nodeAutoNaming.GenerateUniqueName(node),
                Icon = attr.Icon ?? _defaultIcon,
                ViewModelType = attr.ViewModelType,
                ViewType = attr.ViewType,
                ShowAddButton = false,
                ShowDeleteButton = true,
                ConfigType = node.ConfigType,
                Config = Activator.CreateInstance(node.ConfigType!, Guid.NewGuid().ToString()) as IConfig,
                Order = attr.Order,
                Parent = node,
            };

            return newNode;
        }

        [RelayCommand]
        private void AddNode(TreeNode node)
        {
            if (node == null)
                return;

            TreeNode? newNode = CreateNode(node);

            if (newNode == null)
            {
                return;
            }

            node.Children.Add(newNode);
        }

        /// <summary>
        /// 删除节点命令
        /// </summary>
        [RelayCommand]
        private async Task DeleteNode(TreeNode node)
        {
            if (node == null)
                return;

            try
            {
                // 确认删除
                if (!MessageBoxHelper.Confirm($"确定要删除 \"{node.Header}\" 吗？", "确认删除"))
                    return;

                // 先删除对应的配置（仅对叶子配置节点生效）
                if (_configManager != null && node.Config is IConfig cfg)
                {
                    try
                    {
                        var deleteResult = await _configManager.DeleteConfigAsync(cfg);
                        if (deleteResult == null || !deleteResult.Success)
                        {
                            MessageBoxHelper.ShowError($"删除配置失败: {deleteResult?.Message ?? "未知错误"}", "错误");
                            return;
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"删除配置时发生错误: {ex.Message}");
                        MessageBoxHelper.ShowError($"删除配置失败: {ex.Message}", "错误");
                        return;
                    }
                }

                // 从树中移除节点（仅内存中的树结构）
                TreeNode? parent = null;
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
                TreeNode? nextSelectedNode = null;

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

                        ContentControlChanged?.Invoke(this, null);
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
        private async Task SaveConfigurations()
        {
            try
            {
                if (_configManager == null)
                {
                    ToastHelper.ShowError("配置管理器未初始化", "错误");
                    return;
                }

                if (SelectedNode == null || SelectedNode.Config == null)
                {
                    ToastHelper.ShowError("未选择有效的配置节点", "错误");
                    return;
                }

                // 通过 IConfigurationManager 的非泛型入口更新当前配置
                OperationResult rlt = await _configManager.UpdateConfigAsync(SelectedNode.Config);

                if (rlt == null || !rlt.Success)
                {
                    ToastHelper.ShowError("保存配置失败", "错误");
                    return;
                }

                SelectedNode.Header = SelectedNode.Config.ConfigName;
                ToastHelper.ShowSuccess("保存配置成功");
                //var successCount = 0;
                //var errorCount = 0;
                //var errors = new List<string>();

                //// 1. 处理待删除的设备（从设备管理器注销）
                //foreach (var deviceId in _pendingDeviceUnregisters.ToList())
                //{
                //    if (_deviceManager != null)
                //    {
                //        var result = _deviceManager.UnregisterDevice(deviceId);
                //        if (result.Success)
                //        {
                //            successCount++;
                //            System.Diagnostics.Debug.WriteLine($"[ConfigViewModel] 设备 {deviceId} 已从设备管理器注销");
                //        }
                //        else
                //        {
                //            errorCount++;
                //            errors.Add($"注销设备 {deviceId} 失败: {result.ErrorMessage}");
                //            System.Diagnostics.Debug.WriteLine($"[ConfigViewModel] 注销设备 {deviceId} 失败: {result.ErrorMessage}");
                //        }
                //    }
                //}

                //// 清空待删除列表
                //_pendingDeviceUnregisters.Clear();

                //// 2. 将 ConfigurationManager 中的配置应用到已注册的设备（如果设备存在）
                //// 按照树节点的顺序获取配置（保持子节点顺序）
                //var allDeviceConfigs = GetDeviceConfigsInTreeOrder();

                //foreach (var config in allDeviceConfigs)
                //{
                //    if (_deviceManager == null)
                //        continue;

                //    // 检查设备是否已注册
                //    if (_deviceManager.DeviceExists(config.DeviceId))
                //    {
                //        // 设备已存在，检查是否需要更新配置
                //        var deviceResult = _deviceManager.GetDevice(config.DeviceId);
                //        if (deviceResult.Success && deviceResult.Data != null)
                //        {
                //            // 尝试应用配置到设备
                //            var device = deviceResult.Data;

                //            // 使用反射查找 IConfigurable<TConfig> 接口
                //            var configurableInterface = device.GetType().GetInterfaces()
                //                .FirstOrDefault(i => i.IsGenericType &&
                //                                     i.GetGenericTypeDefinition() == typeof(IConfigurable<>));

                //            if (configurableInterface != null)
                //            {
                //                try
                //                {
                //                    // 通过反射调用 ApplyConfig 方法
                //                    var applyConfigMethod = configurableInterface.GetMethod("ApplyConfig");

                //                    if (applyConfigMethod != null)
                //                    {
                //                        var applyResult = applyConfigMethod.Invoke(device, new object[] { config }) as OperationResult;
                //                        if (applyResult != null && !applyResult.Success)
                //                        {
                //                            errorCount++;
                //                            errors.Add($"应用配置到设备 {config.DeviceId} 失败: {applyResult.ErrorMessage}");
                //                        }
                //                        else
                //                        {
                //                            successCount++;
                //                            System.Diagnostics.Debug.WriteLine($"[ConfigViewModel] 设备 {config.DeviceId} 配置已应用");
                //                        }
                //                    }
                //                }
                //                catch (Exception ex)
                //                {
                //                    errorCount++;
                //                    errors.Add($"应用配置到设备 {config.DeviceId} 时发生异常: {ex.Message}");
                //                    System.Diagnostics.Debug.WriteLine($"[ConfigViewModel] 应用配置到设备 {config.DeviceId} 时发生异常: {ex.Message}");
                //                }
                //            }
                //            else
                //            {
                //                System.Diagnostics.Debug.WriteLine($"[ConfigViewModel] 设备 {config.DeviceId} 不支持配置接口，跳过配置应用");
                //            }
                //        }
                //    }
                //    else
                //    {
                //        // 设备不存在，需要根据配置创建设备
                //        try
                //        {
                //            var device = CreateDeviceFromConfig(config);
                //            if (device != null)
                //            {
                //                // 注册设备到设备管理器
                //                var registerResult = _deviceManager.RegisterDevice(device);
                //                if (registerResult.Success)
                //                {
                //                    successCount++;
                //                    System.Diagnostics.Debug.WriteLine($"[ConfigViewModel] 设备 {config.DeviceId} 已创建并注册");
                //                }
                //                else
                //                {
                //                    errorCount++;
                //                    errors.Add($"创建设备 {config.DeviceId} 失败: {registerResult.ErrorMessage}");
                //                    System.Diagnostics.Debug.WriteLine($"[ConfigViewModel] 创建设备 {config.DeviceId} 失败: {registerResult.ErrorMessage}");
                //                }
                //            }
                //            else
                //            {
                //                errorCount++;
                //                errors.Add($"无法为配置 {config.DeviceId} 创建设备：找不到对应的设备类");
                //                System.Diagnostics.Debug.WriteLine($"[ConfigViewModel] 无法为配置 {config.DeviceId} 创建设备：找不到对应的设备类");
                //            }
                //        }
                //        catch (Exception ex)
                //        {
                //            errorCount++;
                //            errors.Add($"创建设备 {config.DeviceId} 时发生异常: {ex.Message}");
                //            System.Diagnostics.Debug.WriteLine($"[ConfigViewModel] 创建设备 {config.DeviceId} 时发生异常: {ex.Message}");
                //        }
                //    }
                //}

                //// 3. 保存配置文件（按设备类型分组保存）
                //try
                //{
                //    SaveConfigFiles(allDeviceConfigs);
                //}
                //catch (Exception ex)
                //{
                //    errorCount++;
                //    errors.Add($"保存配置文件失败: {ex.Message}");
                //    System.Diagnostics.Debug.WriteLine($"[ConfigViewModel] 保存配置文件失败: {ex.Message}");
                //}

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

            //// 按设备类型分组
            //var configsByType = allDeviceConfigs.GroupBy(c => c.Type).ToList();

            //foreach (var group in configsByType)
            //{
            //    var deviceType = group.Key;
            //    // 按创建时间排序，确保保存顺序稳定（与树节点顺序一致）
            //    var configs = group.OrderBy(c => c.CreatedAt).ThenBy(c => c.DeviceName).ToList();

            //    try
            //    {
            //        // 优先从 ConfigurationManager 获取配置文件路径
            //        string configFilePath = null;

            //        // 尝试从第一个配置获取已注册的路径
            //        var firstConfig = configs.First();
            //        if (_configurationManager != null)
            //        {
            //            // 先尝试根据配置类型获取路径
            //            configFilePath = _configurationManager.GetConfigFilePathByType(firstConfig.ConfigType.ToString());

            //            // 如果根据类型没找到，尝试根据 ConfigId 获取（同一类型配置应该使用同一个文件）
            //            if (string.IsNullOrEmpty(configFilePath))
            //            {
            //                configFilePath = _configurationManager.GetConfigFilePath(firstConfig.ConfigId);
            //            }
            //        }

            //        // 如果没有找到已注册的路径，使用查找逻辑
            //        if (string.IsNullOrEmpty(configFilePath))
            //        {
            //            configFilePath = GetConfigFilePath(deviceType, configs.First().GetType());

            //            // 如果找到了路径，将路径注册到 ConfigurationManager（方便下次使用）
            //            if (!string.IsNullOrEmpty(configFilePath) && _configurationManager != null)
            //            {
            //                // 为该类型的所有配置注册路径
            //                foreach (var config in configs)
            //                {
            //                    _configurationManager.SetConfigFilePath(config.ConfigId, configFilePath);
            //                }
            //            }
            //        }

            //        if (string.IsNullOrEmpty(configFilePath))
            //        {
            //            System.Diagnostics.Debug.WriteLine($"[ConfigViewModel] 无法确定设备类型 {deviceType} 的配置文件路径，跳过保存");
            //            continue;
            //        }

            //        // 确保目录存在
            //        var configDir = Path.GetDirectoryName(configFilePath);
            //        if (!string.IsNullOrEmpty(configDir) && !Directory.Exists(configDir))
            //        {
            //            Directory.CreateDirectory(configDir);
            //        }

            //        // 获取具体的配置类型
            //        var concreteConfigType = configs.First().GetType();

            //        // 使用反射创建泛型类型 DeviceConfigData<TConfig>
            //        var configDataGenericType = typeof(DeviceConfigData<>).MakeGenericType(concreteConfigType);
            //        var configData = Activator.CreateInstance(configDataGenericType);
            //        var configsProperty = configDataGenericType.GetProperty("Configs");

            //        // 创建具体类型的列表，并将配置转换为具体类型
            //        var concreteListType = typeof(List<>).MakeGenericType(concreteConfigType);
            //        var concreteList = Activator.CreateInstance(concreteListType);
            //        var addMethod = concreteListType.GetMethod("Add");

            //        foreach (var config in configs)
            //        {
            //            // 将 DeviceConfig 转换为具体类型并添加到列表
            //            addMethod?.Invoke(concreteList, new[] { config });
            //        }

            //        // 设置配置列表属性
            //        configsProperty?.SetValue(configData, concreteList);

            //        // 序列化为 JSON 并保存
            //        var jsonOptions = new JsonSerializerOptions
            //        {
            //            WriteIndented = true,
            //            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping // 不转义中文字符，直接显示中文
            //        };
            //        var json = JsonSerializer.Serialize(configData, configDataGenericType, jsonOptions);
            //        File.WriteAllText(configFilePath, json, System.Text.Encoding.UTF8);

            //        System.Diagnostics.Debug.WriteLine($"[ConfigViewModel] 配置文件已保存: {configFilePath}，包含 {configs.Count} 个配置");
            //    }
            //    catch (Exception ex)
            //    {
            //        System.Diagnostics.Debug.WriteLine($"[ConfigViewModel] 保存设备类型 {deviceType} 的配置文件失败: {ex.Message}");
            //        throw;
            //    }
            //}
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

        /// <summary>
        /// 传感器管理节点标识
        /// </summary>
        private class SensorManagementNodeInfo
        {
            public static SensorManagementNodeInfo Instance { get; } = new SensorManagementNodeInfo();
        }

        /// <summary>
        /// 传感器实例信息
        /// </summary>
        private class SensorInstanceInfo
        {
            public object SensorConfig { get; set; }
        }
    }
}
