using Astra.Core.Configuration;
using Astra.Core.Devices.Attributes;
using Astra.Core.Devices.Interfaces;
using Astra.Core.Foundation.Common;
using Astra.Core.Plugins.Abstractions;
using Astra.UI.Helpers;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Astra.Models;
using Astra.UI.Abstractions.Attributes;
using Astra.Utilities;
using System.Threading.Tasks;
using System.Reflection;

namespace Astra.ViewModels
{
    public partial class ConfigViewModel : ObservableObject
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly string _defaultIcon = "📁";
        private readonly IConfigurationManager? _configManager;

        [ObservableProperty]
        private string _title = "配置管理";

        [ObservableProperty]
        private ObservableCollection<TreeNode> _treeNodes = new ObservableCollection<TreeNode>();

        [ObservableProperty]
        private TreeNode _selectedNode;

        public event EventHandler<Control?>? ContentControlChanged;

        public ConfigViewModel()
        {
            _serviceProvider = App.ServiceProvider;
            _configManager = _serviceProvider?.GetService<IConfigurationManager>();

            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                InitializeConfigTree();
            });
        }

        // 防止配置树刷新时的递归调用标志
        private bool _isRefreshingTree = false;

        // 防止节点选择时的递归调用标志
        private bool _isSelectingNode = false;

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

        /// <summary>
        /// 清除所有节点的选中状态
        /// </summary>
        private void ClearAllSelection()
        {
            foreach (var rootNode in TreeNodes)
            {
                ClearNodeSelection(rootNode);
            }
        }

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
        /// 获取所有带有 TreeNodeConfigAttribute 的配置类型
        /// </summary>
        private List<(Type ConfigType, TreeNodeConfigAttribute Attribute)> GetAllConfigTypes()
        {
            var configTypes = new List<(Type, TreeNodeConfigAttribute)>();

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    var types = assembly.GetTypes()
                        .Where(t => !t.IsAbstract &&
                                   !t.IsInterface &&
                                   typeof(IConfig).IsAssignableFrom(t))
                        .Select(t => new { Type = t, Attr = t.GetCustomAttribute<TreeNodeConfigAttribute>() })
                        .Where(x => x.Attr != null)
                        .Select(x => (x.Type, x.Attr!))
                        .ToList();

                    configTypes.AddRange(types);
                }
                catch (Exception ex)
                {
                    // 忽略无法加载类型的程序集
                    System.Diagnostics.Debug.WriteLine($"[ConfigViewModel] 扫描程序集 {assembly.FullName} 时出错: {ex.Message}");
                }
            }

            return configTypes;
        }

        /// <summary>
        /// 为没有实例的配置类型创建默认配置节点（仅内存，不持久化）
        /// </summary>
        private IConfig? CreateDefaultConfig(Type configType, TreeNodeConfigAttribute attr, List<IConfig> existingConfigsOfSameType)
        {
            try
            {
                // 创建默认配置实例
                var defaultConfig = Activator.CreateInstance(configType, Guid.NewGuid().ToString()) as IConfig;

                if (defaultConfig != null)
                {
                    // 使用 NodeAutoNaming 生成唯一名称
                    var existingNames = existingConfigsOfSameType.Select(c => c.ConfigName).ToList();

                    NodeAutoNaming nodeAutoNaming = new NodeAutoNaming();
                    defaultConfig.ConfigName = nodeAutoNaming.GenerateUniqueNameFromList(existingNames);

                    System.Diagnostics.Debug.WriteLine($"[ConfigViewModel] 已创建默认配置（内存）: {defaultConfig.ConfigName} ({configType.Name})");
                }

                return defaultConfig;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ConfigViewModel] 为类型 {configType.Name} 创建默认配置时出错: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 构建配置树（基于 ConfigurationManager 中的配置）
        /// </summary>
        private async Task BuildConfigTree()
        {
            var result = await _configManager?.GetAllConfigsAsync();

            var existingConfigs = result?.Data?.ToList() ?? new List<IConfig>();

            TreeNodes?.Clear();

            // 使用字典跟踪所有根节点，避免重复处理
            Dictionary<string, TreeNode> rootNodes = new Dictionary<string, TreeNode>();

            // 获取所有配置类型
            var allConfigTypes = GetAllConfigTypes();

            // 按配置类型分组（确保每个类型至少有一个配置）
            var configTypeGroups = allConfigTypes.GroupBy(x => x.ConfigType);

            foreach (var typeGroup in configTypeGroups)
            {
                var configType = typeGroup.Key;
                var attr = typeGroup.First().Attribute;

                // 查找该类型的现有配置实例
                var configsOfType = existingConfigs.Where(c => c.GetType() == configType).ToList();

                // 如果没有现有配置，创建一个默认配置（仅内存）
                if (configsOfType.Count == 0)
                {
                    var defaultConfig = CreateDefaultConfig(configType, attr, configsOfType);
                    if (defaultConfig != null)
                    {
                        configsOfType.Add(defaultConfig);
                    }
                }

                // 按照 UpdatedAt 排序（保持保存时的顺序）
                // UpdatedAt 为 null 的配置排在最后
                configsOfType = configsOfType
                    .OrderBy(c => c.UpdatedAt ?? DateTime.MaxValue)
                    .ThenBy(c => c.CreatedAt) // 如果 UpdatedAt 相同，按创建时间排序
                    .ToList();

                // 为该类型的所有配置创建树节点
                foreach (var config in configsOfType)
                {
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
                        ConfigType = configType,
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
                        rootNode.ConfigType = configType;
                    }

                    rootNode.Children.Add(childNode);
                }
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

            // 防止递归调用（TreeView 的 Selected 事件会触发此命令）
            if (_isSelectingNode)
                return;

            _isSelectingNode = true;
            try
            {
                // 清除所有节点的选中状态
                ClearAllSelection();

                // 设置新节点为选中状态
                SelectedNode = node;
                node.IsSelected = true;

                LoadConfigView(node);
            }
            finally
            {
                _isSelectingNode = false;
            }
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
                ContentControlChanged?.Invoke(this, configView);
            }
            catch (Exception ex)
            {
                ToastHelper.ShowError($"加载配置界面时发生错误: {ex.Message}");
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
                // 无论从上往下拖还是从下往上拖，目标索引保持不变
                int newTargetIndex = targetIndex;

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
                    // 在删除前，尝试选择相邻兄弟节点
                    var children = parent.Children;

                    // 优先选择上一个兄弟节点（向上移动）
                    if (index > 0)
                    {
                        nextSelectedNode = children[index - 1];
                    }
                    // 如果没有上一个节点，选择下一个兄弟节点（向下移动）
                    else if (index < children.Count - 1)
                    {
                        // 注意：删除后，index+1 位置的节点会移动到 index 位置
                        nextSelectedNode = children[index + 1];
                    }
                    else
                    {
                        // 如果没有兄弟节点（是唯一的子节点），选择父节点
                        nextSelectedNode = parent;
                    }
                }
                else if (isSelectedNode && index >= 0)
                {
                    // 删除的是根节点
                    // 优先选择上一个根节点（向上移动）
                    if (index > 0)
                    {
                        nextSelectedNode = TreeNodes[index - 1];
                    }
                    // 如果没有上一个节点，选择下一个根节点（向下移动）
                    else if (index < TreeNodes.Count - 1)
                    {
                        // 注意：删除后，index+1 位置的节点会移动到 index 位置
                        nextSelectedNode = TreeNodes[index + 1];
                    }
                    // 如果是最后一个根节点，nextSelectedNode 保持为 null（清空选中）
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
        /// 保存单个配置命令（右键菜单使用）
        /// </summary>
        [RelayCommand]
        private async Task SaveSingleConfiguration(TreeNode? node)
        {
            System.Diagnostics.Debug.WriteLine($"[ConfigViewModel] SaveSingleConfiguration 被调用, node: {node?.Header}");
            
            try
            {
                if (_configManager == null)
                {
                    ToastHelper.ShowError("配置管理器未初始化");
                    return;
                }

                // 如果没有传入节点，使用当前选中的节点
                var targetNode = node ?? SelectedNode;

                if (targetNode == null || targetNode.Config == null)
                {
                    ToastHelper.ShowError("未选择有效的配置节点");
                    return;
                }

                // 通过 IConfigurationManager 的非泛型入口更新当前配置
                OperationResult rlt = await _configManager.UpdateConfigAsync(targetNode.Config);

                if (rlt == null || !rlt.Success)
                {
                    ToastHelper.ShowError($"保存配置失败: {rlt?.Message}");
                    return;
                }

                targetNode.Header = targetNode.Config.ConfigName;
                ToastHelper.ShowSuccess($"已保存: {targetNode.Config.ConfigName}");

            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"保存单个配置时发生错误: {ex.Message}");
                ToastHelper.ShowError($"保存配置失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 保存所有配置命令（保存按钮使用）
        /// </summary>
        [RelayCommand]
        private async Task SaveAllConfigurations()
        {
            try
            {
                if (_configManager == null)
                {
                    ToastHelper.ShowError("配置管理器未初始化");
                    return;
                }

                var successCount = 0;
                var errorCount = 0;
                var errors = new List<string>();

                // 获取基准时间（用于按顺序设置 UpdatedAt）
                var baseTime = DateTime.Now;
                var timeOffset = 0;

                // 遍历所有根节点（按树中的顺序）
                foreach (var rootNode in TreeNodes)
                {
                    // 遍历所有子节点（实际配置，按树中的顺序）
                    foreach (var childNode in rootNode.Children)
                    {
                        if (childNode.Config != null)
                        {
                            try
                            {
                                // 根据节点在树中的位置设置 UpdatedAt（保持顺序）
                                // 使用递增的时间偏移量，确保顺序正确
                                childNode.Config.UpdatedAt = baseTime.AddMilliseconds(timeOffset);
                                timeOffset++;

                                var result = await _configManager.UpdateConfigAsync(childNode.Config);

                                if (result != null && result.Success)
                                {
                                    childNode.Header = string.IsNullOrEmpty(childNode.Config.ConfigName) ? childNode.Header : childNode.Config.ConfigName;
                                    successCount++;
                                }
                                else
                                {
                                    errorCount++;
                                    errors.Add($"{childNode.Config.ConfigName}: {result?.Message ?? "未知错误"}");
                                }
                            }
                            catch (Exception ex)
                            {
                                errorCount++;
                                errors.Add($"{childNode.Config.ConfigName}: {ex.Message}");
                            }
                        }
                    }
                }

                // 显示结果
                if (errorCount == 0)
                {
                    ToastHelper.ShowSuccess($"已成功保存 {successCount} 个配置");
                }
                else
                {
                    var errorMessage = $"保存完成：成功 {successCount} 个，失败 {errorCount} 个";
                    if (errors.Count > 0)
                    {
                        errorMessage += $"\n失败原因：\n{string.Join("\n", errors.Take(5))}";
                        if (errors.Count > 5)
                        {
                            errorMessage += $"\n... 还有 {errors.Count - 5} 个错误";
                        }
                    }
                    MessageBoxHelper.ShowWarning(errorMessage, "保存结果");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"保存所有配置时发生错误: {ex.Message}");
                ToastHelper.ShowError($"保存配置失败: {ex.Message}");
            }
        }

    }
}
