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
using Microsoft.Win32;
using System.IO;
using System.Text.Json;

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
        /// 从类型名称字符串获取类型（支持所有已加载的程序集和插件程序集）
        /// </summary>
        private Type? GetTypeFromName(string typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName))
                return null;

            // 首先尝试 Type.GetType()（最快，适用于当前程序集和 mscorlib）
            var type = Type.GetType(typeName);
            if (type != null)
                return type;

            // 第二步：从插件程序集中查找类型
            try
            {
                var pluginHost = _serviceProvider?.GetService<Astra.Core.Plugins.Abstractions.IPluginHost>();
                if (pluginHost != null)
                {
                    // 遍历所有已加载的插件
                    foreach (var plugin in pluginHost.LoadedPlugins)
                    {
                        try
                        {
                            // 获取插件类型所在的程序集
                            var pluginAssembly = plugin.GetType().Assembly;
                            
                            // 尝试完整类型名称
                            type = pluginAssembly.GetType(typeName);
                            if (type != null)
                                return type;

                            // 尝试匹配完整名称（忽略程序集信息）
                            if (typeName.Contains(','))
                            {
                                var typeNameWithoutAssembly = typeName.Split(',')[0].Trim();
                                type = pluginAssembly.GetType(typeNameWithoutAssembly);
                                if (type != null)
                                    return type;
                            }

                            // 尝试简单类型名称匹配
                            if (!typeName.Contains('.'))
                            {
                                type = pluginAssembly.GetTypes()
                                    .FirstOrDefault(t => t.Name == typeName);
                                if (type != null)
                                    return type;
                            }
                            else
                            {
                                // 尝试匹配命名空间和类型名
                                var parts = typeName.Split('.');
                                if (parts.Length > 0)
                                {
                                    var simpleName = parts[parts.Length - 1];
                                    type = pluginAssembly.GetTypes()
                                        .FirstOrDefault(t => t.Name == simpleName && 
                                                           (t.FullName == typeName || t.FullName?.EndsWith("." + typeName) == true));
                                    if (type != null)
                                        return type;
                                }
                            }
                        }
                        catch
                        {
                            // 忽略插件程序集中的错误
                            continue;
                        }
                    }
                }
            }
            catch
            {
                // 忽略插件系统访问错误
            }

            // 第三步：遍历所有已加载的程序集查找类型（包括非插件程序集）
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    // 跳过已经检查过的插件程序集
                    var pluginHost = _serviceProvider?.GetService<Astra.Core.Plugins.Abstractions.IPluginHost>();
                    if (pluginHost != null && pluginHost.LoadedPlugins.Any(p => p.GetType().Assembly == assembly))
                    {
                        continue; // 已经在插件检查中处理过了
                    }

                    // 尝试完整类型名称
                    type = assembly.GetType(typeName);
                    if (type != null)
                        return type;

                    // 尝试简单类型名称匹配（如果类型名称不包含命名空间）
                    if (!typeName.Contains('.'))
                    {
                        type = assembly.GetTypes()
                            .FirstOrDefault(t => t.Name == typeName);
                        if (type != null)
                            return type;
                    }
                    else
                    {
                        // 尝试匹配完整名称（忽略程序集信息）
                        var typeNameWithoutAssembly = typeName.Split(',')[0].Trim();
                        type = assembly.GetType(typeNameWithoutAssembly);
                        if (type != null)
                            return type;
                    }
                }
                catch
                {
                    // 忽略无法加载类型的程序集
                    continue;
                }
            }

            return null;
        }

        /// <summary>
        /// 获取所有带有 TreeNodeConfigAttribute 的配置类型（包括插件中的配置类型）
        /// </summary>
        private List<(Type ConfigType, TreeNodeConfigAttribute Attribute)> GetAllConfigTypes()
        {
            var configTypes = new List<(Type, TreeNodeConfigAttribute)>();
            var processedAssemblies = new HashSet<Assembly>();

            // 第一步：从插件程序集中获取配置类型
            try
            {
                var pluginHost = _serviceProvider?.GetService<Astra.Core.Plugins.Abstractions.IPluginHost>();
                if (pluginHost != null)
                {
                    foreach (var plugin in pluginHost.LoadedPlugins)
                    {
                        try
                        {
                            var pluginAssembly = plugin.GetType().Assembly;
                            if (processedAssemblies.Contains(pluginAssembly))
                                continue;

                            processedAssemblies.Add(pluginAssembly);

                            var types = pluginAssembly.GetTypes()
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
                            // 忽略插件程序集中的错误
                            System.Diagnostics.Debug.WriteLine($"[ConfigViewModel] 扫描插件程序集 {plugin.GetType().Assembly.FullName} 时出错: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // 忽略插件系统访问错误
                System.Diagnostics.Debug.WriteLine($"[ConfigViewModel] 访问插件系统时出错: {ex.Message}");
            }

            // 第二步：从其他程序集中获取配置类型
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                // 跳过已经处理过的插件程序集
                if (processedAssemblies.Contains(assembly))
                    continue;

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

            // 对于实现了 IPostLoadConfig 接口的配置，执行加载后处理
            await PostLoadConfigurations(existingConfigs);

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

                // 如果配置实现了 IPreSaveConfig 接口，执行预保存逻辑
                if (targetNode.Config is Astra.Core.Configuration.IPreSaveConfig preSaveConfig)
                {
                    var preSaveResult = await preSaveConfig.PreSaveAsync(_configManager);
                    if (preSaveResult != null && !preSaveResult.Success)
                    {
                        ToastHelper.ShowError($"预保存配置失败: {preSaveResult.Message}");
                        return;
                    }
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
                                // 如果配置实现了 IPreSaveConfig 接口，执行预保存逻辑
                                if (childNode.Config is Astra.Core.Configuration.IPreSaveConfig preSaveConfig)
                                {
                                    var preSaveResult = await preSaveConfig.PreSaveAsync(_configManager);
                                    if (preSaveResult != null && !preSaveResult.Success)
                                    {
                                        errorCount++;
                                        errors.Add($"{childNode.Config.ConfigName}: 预保存失败 - {preSaveResult.Message}");
                                        continue; // 跳过保存主配置
                                    }
                                }

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

        /// <summary>
        /// 导入配置命令（根节点右键菜单使用）
        /// </summary>
        [RelayCommand]
        private async Task ImportConfigurations(TreeNode? node)
        {
            try
            {
                if (_configManager == null)
                {
                    ToastHelper.ShowError("配置管理器未初始化");
                    return;
                }

                // 如果没有传入节点，使用当前选中的节点
                var targetNode = node ?? SelectedNode;

                // 验证是否为根节点（Config == null）
                if (targetNode == null || targetNode.Config != null)
                {
                    ToastHelper.ShowError("请选择根节点进行导入");
                    return;
                }

                // 检查根节点是否有配置类型
                if (targetNode.ConfigType == null)
                {
                    ToastHelper.ShowError("根节点未指定配置类型");
                    return;
                }

                // 打开文件选择对话框
                var openFileDialog = new OpenFileDialog
                {
                    Title = "选择要导入的配置文件",
                    Filter = "JSON文件 (*.json)|*.json|所有文件 (*.*)|*.*",
                    Multiselect = true,
                    CheckFileExists = true
                };

                if (openFileDialog.ShowDialog() != true)
                {
                    return; // 用户取消
                }

                var filePaths = openFileDialog.FileNames;
                if (filePaths.Length == 0)
                {
                    return;
                }

                var successCount = 0;
                var failureCount = 0;
                var errors = new List<string>();

                var jsonOptions = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNameCaseInsensitive = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };

                // 处理每个文件
                foreach (var filePath in filePaths)
                {
                    try
                    {
                        var jsonContent = await File.ReadAllTextAsync(filePath);
                        
                        var configsToImport = new List<IConfig>();

                        // 尝试解析JSON文档并提取所有配置的JSON字符串
                        List<string> configJsonStrings = new List<string>();
                        
                        using (var doc = JsonDocument.Parse(jsonContent))
                        {
                            var rootElement = doc.RootElement;

                            if (rootElement.ValueKind == JsonValueKind.Array)
                            {
                                // 数组格式：包含多个配置
                                foreach (var element in rootElement.EnumerateArray())
                                {
                                    // 在 using 块内提取 JSON 字符串（字符串不依赖于 JsonDocument）
                                    configJsonStrings.Add(element.GetRawText());
                                }
                            }
                            else
                            {
                                // 单个配置对象
                                configJsonStrings.Add(rootElement.GetRawText());
                            }
                        } // using 块结束，JsonDocument 被释放，但字符串仍然有效

                        // 在 using 块外反序列化所有配置，并严格验证类型
                        // 充分利用 ConfigTypeName 和 ConfigType 属性来确保类型安全
                        var allConfigTypes = GetAllConfigTypes();
                        var targetTypeName = targetNode.ConfigType.AssemblyQualifiedName ?? targetNode.ConfigType.FullName ?? targetNode.ConfigType.Name;
                        
                        foreach (var configJson in configJsonStrings)
                        {
                            try
                            {
                                IConfig? config = null;
                                
                                // 第一步：先检查 JSON 中的 ConfigTypeName，提前过滤类型不匹配的配置
                                string? jsonTypeName = null;
                                try
                                {
                                    using var tempDoc = JsonDocument.Parse(configJson);
                                    var element = tempDoc.RootElement;
                                    
                                    // 优先检查 ConfigTypeName 字段
                                    if (element.TryGetProperty("ConfigTypeName", out var configTypeNameProp))
                                    {
                                        jsonTypeName = configTypeNameProp.GetString();
                                    }
                                    // 如果没有 ConfigTypeName，检查 $type 或 @type 字段
                                    else if (element.TryGetProperty("$type", out var typeProp) || 
                                             element.TryGetProperty("@type", out typeProp))
                                    {
                                        jsonTypeName = typeProp.GetString();
                                    }
                                    
                                    // 如果找到了类型名称，先验证是否匹配目标类型
                                    if (!string.IsNullOrWhiteSpace(jsonTypeName))
                                    {
                                        // 使用辅助方法从所有程序集中查找类型
                                        var jsonType = GetTypeFromName(jsonTypeName);
                                        
                                        if (jsonType != null)
                                        {
                                            // 类型存在，检查是否匹配目标类型
                                            if (jsonType != targetNode.ConfigType)
                                            {
                                                // 类型不匹配，直接跳过
                                                var configName = element.TryGetProperty("ConfigName", out var nameProp) 
                                                    ? nameProp.GetString() 
                                                    : "未命名配置";
                                                var rootNodeName = targetNode.Header ?? "未知节点";
                                                var actualTypeName = jsonType.Name;
                                                var expectedTypeName = targetNode.ConfigType?.Name ?? "未知类型";
                                                
                                                errors.Add($"{Path.GetFileName(filePath)}: 配置类型不匹配 - {configName}（实际类型：{actualTypeName}，期望类型：{expectedTypeName}），不能导入到 {rootNodeName} 节点");
                                                failureCount++;
                                                continue; // 跳过这个配置
                                            }
                                            // 如果类型匹配，继续反序列化
                                        }
                                        // 如果类型不存在，继续尝试反序列化（可能是旧格式的配置文件）
                                    }
                                }
                                catch
                                {
                                    // JSON 解析失败，继续尝试反序列化
                                }
                                
                                // 第二步：只尝试反序列化为目标类型（如果 JSON 中没有类型信息或类型匹配）
                                // 这样可以避免 System.Text.Json 强制转换导致的问题
                                try
                                {
                                    var testObj = JsonSerializer.Deserialize(configJson, targetNode.ConfigType, jsonOptions) as IConfig;
                                    if (testObj != null)
                                    {
                                        // 验证反序列化是否真正成功（检查关键属性）
                                        if (!string.IsNullOrEmpty(testObj.ConfigId) || !string.IsNullOrEmpty(testObj.ConfigName))
                                        {
                                            // 使用 ConfigType 属性严格检查类型是否匹配
                                            if (testObj.ConfigType == targetNode.ConfigType)
                                            {
                                                config = testObj;
                                            }
                                        }
                                    }
                                }
                                catch
                                {
                                    // 反序列化失败，继续尝试其他类型
                                }
                                
                                // 第三步：如果目标类型反序列化失败，尝试所有已知类型（但只接受匹配的类型）
                                if (config == null)
                                {
                                    IConfig? foundConfig = null;
                                    Type? foundType = null;
                                    
                                    foreach (var (configType, _) in allConfigTypes)
                                    {
                                        // 跳过目标类型，因为已经尝试过了
                                        if (configType == targetNode.ConfigType)
                                            continue;
                                            
                                        try
                                        {
                                            var testObj = JsonSerializer.Deserialize(configJson, configType, jsonOptions) as IConfig;
                                            if (testObj != null)
                                            {
                                                // 验证反序列化是否真正成功
                                                if (!string.IsNullOrEmpty(testObj.ConfigId) || !string.IsNullOrEmpty(testObj.ConfigName))
                                                {
                                                    // 记录找到的配置和类型（用于后续错误提示）
                                                    if (foundConfig == null)
                                                    {
                                                        foundConfig = testObj;
                                                        foundType = testObj.ConfigType;
                                                    }
                                                    
                                                    // 严格检查类型是否匹配目标类型
                                                    if (testObj.ConfigType == targetNode.ConfigType)
                                                    {
                                                        config = testObj;
                                                        break;
                                                    }
                                                }
                                            }
                                        }
                                        catch
                                        {
                                            // 继续尝试下一个类型
                                            continue;
                                        }
                                    }
                                    
                                    // 如果找到了配置但类型不匹配，记录错误
                                    if (config == null && foundConfig != null && foundType != null)
                                    {
                                        var configName = foundConfig.ConfigName ?? "未命名配置";
                                        var rootNodeName = targetNode.Header ?? "未知节点";
                                        var actualTypeName = foundType.Name;
                                        var expectedTypeName = targetNode.ConfigType?.Name ?? "未知类型";
                                        
                                        errors.Add($"{Path.GetFileName(filePath)}: 配置类型不匹配 - {configName}（实际类型：{actualTypeName}，期望类型：{expectedTypeName}），不能导入到 {rootNodeName} 节点");
                                        failureCount++;
                                        continue; // 跳过这个配置
                                    }
                                }

                                // 验证是否成功反序列化
                                if (config == null)
                                {
                                    errors.Add($"{Path.GetFileName(filePath)}: 无法反序列化配置或配置类型不匹配目标节点类型");
                                    failureCount++;
                                    continue;
                                }

                                // 验证 ConfigTypeName（如果存在）是否与实际类型匹配
                                if (!string.IsNullOrWhiteSpace(config.ConfigTypeName))
                                {
                                    var expectedTypeName = config.ConfigType.AssemblyQualifiedName ?? config.ConfigType.FullName ?? config.ConfigType.Name;
                                    if (config.ConfigTypeName != expectedTypeName)
                                    {
                                        System.Diagnostics.Debug.WriteLine($"警告：配置 {config.ConfigName} 的 ConfigTypeName ({config.ConfigTypeName}) 与实际类型 ({expectedTypeName}) 不匹配，将使用实际类型");
                                        // 更新 ConfigTypeName 以匹配实际类型
                                        config.ConfigTypeName = expectedTypeName;
                                    }
                                }
                                else
                                {
                                    // 如果 ConfigTypeName 为空，自动设置
                                    config.ConfigTypeName = config.ConfigType.AssemblyQualifiedName ?? config.ConfigType.FullName ?? config.ConfigType.Name;
                                }

                                // 最终验证：使用 ConfigType 属性确保类型完全匹配
                                // 这是双重检查，确保类型安全
                                if (config.ConfigType != targetNode.ConfigType)
                                {
                                    var configName = config.ConfigName ?? "未命名配置";
                                    var rootNodeName = targetNode.Header ?? "未知节点";
                                    var actualTypeName = config.ConfigType.Name;
                                    var expectedTypeName = targetNode.ConfigType?.Name ?? "未知类型";
                                    
                                    errors.Add($"{Path.GetFileName(filePath)}: 配置类型不匹配 - {configName}（实际类型：{actualTypeName}，期望类型：{expectedTypeName}），不能导入到 {rootNodeName} 节点");
                                    failureCount++;
                                    continue;
                                }

                                // 生成新的 ConfigId 避免冲突（使用反射直接设置字段）
                                if (config is ConfigBase configBase)
                                {
                                    var configIdField = typeof(ConfigBase).GetField("_configId", 
                                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                                    configIdField?.SetValue(configBase, Guid.NewGuid().ToString());
                                }
                                
                                configsToImport.Add(config);
                            }
                            catch (JsonException jsonEx)
                            {
                                errors.Add($"{Path.GetFileName(filePath)}: JSON 格式错误 - {jsonEx.Message}");
                                failureCount++;
                            }
                            catch (Exception ex)
                            {
                                errors.Add($"{Path.GetFileName(filePath)}: 反序列化配置失败 - {ex.Message}");
                                failureCount++;
                            }
                        }

                        // 获取配置类型的属性信息（用于创建树节点）
                        var attr = targetNode.ConfigType.GetCustomAttribute<TreeNodeConfigAttribute>();
                        if (attr == null)
                        {
                            errors.Add($"{Path.GetFileName(filePath)}: 配置类型缺少 TreeNodeConfigAttribute");
                            failureCount++;
                            continue;
                        }

                        // 直接将配置添加到树节点（不保存到数据库，不修改配置属性）
                        // 注意：类型检查已在反序列化阶段完成，这里直接添加
                        foreach (var config in configsToImport)
                        {
                            try
                            {
                                // 使用配置的原始名称，不修改任何属性
                                var displayName = string.IsNullOrWhiteSpace(config.ConfigName) 
                                    ? "未命名配置" 
                                    : config.ConfigName;

                                // 创建子节点
                                TreeNode childNode = new TreeNode
                                {
                                    Header = displayName,
                                    Icon = attr.Icon ?? _defaultIcon,
                                    ViewModelType = attr.ViewModelType,
                                    ViewType = attr.ViewType,
                                    ShowAddButton = false,
                                    ShowDeleteButton = true,
                                    Config = config, // 保持配置对象的所有原始属性不变
                                    Order = attr.Order,
                                    ConfigType = config.ConfigType, // 使用配置的实际类型，而不是目标节点类型
                                    Parent = targetNode,
                                };

                                // 添加到根节点的子节点集合
                                targetNode.Children.Add(childNode);
                                
                                // 确保根节点展开，以便用户能看到导入的配置
                                targetNode.IsExpanded = true;
                                
                                successCount++;
                            }
                            catch (Exception ex)
                            {
                                failureCount++;
                                errors.Add($"{config.ConfigName ?? "未知配置"}: {ex.Message}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        failureCount++;
                        errors.Add($"{Path.GetFileName(filePath)}: {ex.Message}");
                    }
                }

                // 不需要刷新整个配置树，因为已经直接添加到树节点了

                // 显示结果
                if (failureCount == 0)
                {
                    ToastHelper.ShowSuccess($"已成功导入 {successCount} 个配置");
                }
                else
                {
                    var errorMessage = $"导入完成：成功 {successCount} 个，失败 {failureCount} 个";
                    if (errors.Count > 0)
                    {
                        var errorDetails = errors.Take(5);
                        errorMessage += $"\n失败详情：\n{string.Join("\n", errorDetails)}";
                        if (errors.Count > 5)
                        {
                            errorMessage += $"\n... 还有 {errors.Count - 5} 个错误";
                        }
                    }
                    MessageBoxHelper.ShowWarning(errorMessage, "导入结果");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"导入配置时发生错误: {ex.Message}");
                ToastHelper.ShowError($"导入配置失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 导出配置命令（根节点右键菜单使用）
        /// </summary>
        [RelayCommand]
        private async Task ExportConfigurations(TreeNode? node)
        {
            try
            {
                if (_configManager == null)
                {
                    ToastHelper.ShowError("配置管理器未初始化");
                    return;
                }

                // 如果没有传入节点，使用当前选中的节点
                var targetNode = node ?? SelectedNode;

                // 验证是否为根节点（Config == null）
                if (targetNode == null || targetNode.Config != null)
                {
                    ToastHelper.ShowError("请选择根节点进行导出");
                    return;
                }

                // 检查根节点是否有子配置
                if (targetNode.Children == null || targetNode.Children.Count == 0)
                {
                    ToastHelper.ShowWarning("该根节点下没有可导出的配置");
                    return;
                }

                // 检查根节点是否有配置类型
                if (targetNode.ConfigType == null)
                {
                    ToastHelper.ShowError("根节点未指定配置类型");
                    return;
                }

                // 打开文件夹选择对话框（使用 SaveFileDialog 作为替代方案，选择目录）
                var saveFileDialog = new SaveFileDialog
                {
                    Title = "选择导出目录和文件名",
                    Filter = "JSON文件 (*.json)|*.json|所有文件 (*.*)|*.*",
                    FileName = $"{targetNode.Header}_导出_{DateTime.Now:yyyyMMdd_HHmmss}.json"
                };

                if (saveFileDialog.ShowDialog() != true)
                {
                    return; // 用户取消
                }

                var exportFilePath = saveFileDialog.FileName;
                var exportDirectory = Path.GetDirectoryName(exportFilePath);

                // 获取所有子节点的配置
                var configs = targetNode.Children
                    .Where(child => child.Config != null)
                    .Select(child => child.Config)
                    .ToList();

                if (configs.Count == 0)
                {
                    ToastHelper.ShowWarning("没有可导出的配置");
                    return;
                }

                // 导出方式：将所有配置序列化为一个JSON数组
                var jsonOptions = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNameCaseInsensitive = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };

                // 使用 ConfigType 属性获取配置的JSON表示
                var configsJson = new List<string>();
                var successCount = 0;
                var failCount = 0;
                
                foreach (var config in configs)
                {
                    try
                    {
                        // 使用 ConfigType 属性而不是 GetType()，保持一致性
                        var json = JsonSerializer.Serialize(config, config.ConfigType, jsonOptions);
                        if (!string.IsNullOrWhiteSpace(json))
                        {
                            configsJson.Add(json);
                            successCount++;
                        }
                        else
                        {
                            failCount++;
                            System.Diagnostics.Debug.WriteLine($"序列化配置 {config.ConfigName} 返回空字符串");
                        }
                    }
                    catch (Exception ex)
                    {
                        failCount++;
                        System.Diagnostics.Debug.WriteLine($"序列化配置 {config.ConfigName} 时出错: {ex.Message}");
                        System.Diagnostics.Debug.WriteLine($"异常详情: {ex}");
                    }
                }

                // 检查是否有成功序列化的配置
                if (configsJson.Count == 0)
                {
                    ToastHelper.ShowError($"导出失败：所有配置序列化都失败了（共 {configs.Count} 个配置）");
                    return;
                }

                // 将所有配置合并为一个JSON数组
                var combinedJson = $"[{string.Join(",\n", configsJson)}]";
                
                // 确保目录存在
                var directory = Path.GetDirectoryName(exportFilePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                
                // 写入文件
                await File.WriteAllTextAsync(exportFilePath, combinedJson, System.Text.Encoding.UTF8);

                // 显示导出结果
                if (failCount == 0)
                {
                    ToastHelper.ShowSuccess($"已成功导出 {successCount} 个配置到: {Path.GetFileName(exportFilePath)}");
                }
                else
                {
                    ToastHelper.ShowWarning($"导出完成：成功 {successCount} 个，失败 {failCount} 个");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"导出配置时发生错误: {ex.Message}");
                ToastHelper.ShowError($"导出配置失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 对实现了 IPostLoadConfig 接口的配置执行加载后处理
        /// 使用接口而非具体类型，避免主应用依赖插件实现
        /// </summary>
        private async Task PostLoadConfigurations(List<IConfig> configs)
        {
            if (_configManager == null)
                return;

            // 查找所有实现了 IPostLoadConfig 接口的配置
            var postLoadConfigs = configs.OfType<Astra.Core.Configuration.IPostLoadConfig>().ToList();
            if (postLoadConfigs.Count == 0)
                return;

            try
            {
                // 对每个配置执行加载后处理
                foreach (var config in postLoadConfigs)
                {
                    try
                    {
                        var result = await config.PostLoadAsync(_configManager);
                        if (result != null && !result.Success)
                        {
                            System.Diagnostics.Debug.WriteLine($"[ConfigViewModel] 配置 {config.ConfigName} 的加载后处理失败: {result.Message}");
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[ConfigViewModel] 配置 {config.ConfigName} 的加载后处理异常: {ex.Message}");
                    }
                }

                System.Diagnostics.Debug.WriteLine($"[ConfigViewModel] 已完成 {postLoadConfigs.Count} 个配置的加载后处理");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ConfigViewModel] 执行配置加载后处理失败: {ex.Message}");
            }
        }

    }
}
