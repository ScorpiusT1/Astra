using Astra.Core.Configuration;
using Astra.Core.Foundation.Common;
using Astra.Models;
using Astra.UI.Abstractions.Attributes;
using Astra.UI.Helpers;
using Astra.Utilities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Windows.Controls;

namespace Astra.ViewModels
{
    public partial class ConfigViewModel : ObservableObject
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly string _defaultIcon = "📁";
        private readonly IConfigurationManager? _configManager;

        // ✅ 缓存已创建的 View 和 ViewModel，避免切换节点时丢失未保存的数据
        private readonly Dictionary<string, (UserControl View, object ViewModel)> _viewCache = new Dictionary<string, (UserControl, object)>();

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
                return;
            }

            _isRefreshingTree = true;

            TreeNodes.Clear();

            // ✅ 刷新树时清理所有缓存（因为节点可能已经改变）
            _viewCache.Clear();

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
        /// 获取所有已注册的配置类型及其 TreeNodeConfigAttribute
        /// ✅ 重构：基于已注册的 Provider 类型，而不是扫描程序集
        /// 这样可以确保只显示有 Provider 的配置类型，避免配置未注册的错误
        /// </summary>
        private async Task<List<(Type ConfigType, TreeNodeConfigAttribute Attribute)>> GetAllConfigTypesAsync()
        {
            var configTypes = new List<(Type, TreeNodeConfigAttribute)>();

            if (_configManager == null)
            {
                return configTypes;
            }

            try
            {
                // ✅ 使用 ConfigurationManager 获取所有已注册的配置类型
                // 这样可以确保只获取有 Provider 的配置类型
                var result = await _configManager.GetAllConfigTypesAsync();

                if (result?.Success == true && result.Data != null)
                {
                    foreach (var configType in result.Data)
                    {
                        try
                        {
                            // 获取配置类型的 TreeNodeConfigAttribute
                            var attr = configType.GetCustomAttribute<TreeNodeConfigAttribute>();
                            if (attr != null)
                            {
                                configTypes.Add((configType, attr));
                            }
                        }
                        catch (Exception ex)
                        {
                            // 忽略获取属性时的错误，继续处理下一个配置类型
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // 忽略获取配置类型时的异常
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
                }

                return defaultConfig;
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        /// <summary>
        /// 构建配置树（基于 ConfigurationManager 中的配置）
        /// ✅ 重构：使用 GetAllConfigTypesAsync() 获取已注册的配置类型
        /// ⚠️ 关键修复：保留已存在节点的 Config 引用，避免刷新树时丢失未保存的数据
        /// </summary>
        private async Task BuildConfigTree()
        {
            // ⚠️ 关键：在重新加载配置之前，先收集所有当前树节点中的 Config 对象
            // 这样可以保留用户在界面上修改但未保存的数据
            Dictionary<string, IConfig> existingNodeConfigs = new Dictionary<string, IConfig>();
            foreach (var rootNode in TreeNodes)
            {
                foreach (var childNode in rootNode.Children)
                {
                    if (childNode.Config != null && !string.IsNullOrEmpty(childNode.Config.ConfigId))
                    {
                        existingNodeConfigs[childNode.Config.ConfigId] = childNode.Config;
                    }
                }
            }

            var result = await _configManager?.GetAllConfigsAsync();

            var loadedConfigs = result?.Data?.ToList() ?? new List<IConfig>();

            // ✅ IPostLoadConfig 的调用已移至 ConfigurationManager，这里无需处理

            TreeNodes?.Clear();

            // 使用字典跟踪所有根节点，避免重复处理
            Dictionary<string, TreeNode> rootNodes = new Dictionary<string, TreeNode>();

            // ✅ 获取所有已注册的配置类型（基于 Provider，而不是扫描程序集）
            var allConfigTypes = await GetAllConfigTypesAsync();

            // 按配置类型分组（确保每个类型至少有一个配置）
            var configTypeGroups = allConfigTypes.GroupBy(x => x.ConfigType);

            foreach (var typeGroup in configTypeGroups)
            {
                var configType = typeGroup.Key;
                var attr = typeGroup.First().Attribute;

                // 查找该类型的现有配置实例
                // ⚠️ 关键：优先使用已存在树节点中的 Config（保留未保存的修改）
                // 如果某个 ConfigId 在 existingNodeConfigs 中存在，使用它；否则使用从配置管理器加载的 Config
                var configsOfType = new List<IConfig>();
                var loadedConfigsOfType = loadedConfigs.Where(c => c.GetType() == configType).ToList();
                
                foreach (var loadedConfig in loadedConfigsOfType)
                {
                    // 如果该 ConfigId 在 existingNodeConfigs 中存在，使用已存在的 Config（保留修改）
                    if (existingNodeConfigs.TryGetValue(loadedConfig.ConfigId, out var existingConfig))
                    {
                        configsOfType.Add(existingConfig);
                    }
                    else
                    {
                        // 否则使用从配置管理器加载的新 Config
                        configsOfType.Add(loadedConfig);
                    }
                }

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
        /// ✅ 重构：智能匹配 ViewModel 构造函数，支持 IConfig 和具体配置类型
        /// ✅ 添加缓存机制：复用已创建的 View 和 ViewModel，避免切换节点时丢失未保存的数据
        /// </summary>
        private void LoadConfigView(TreeNode node)
        {
            if (node == null)
                return;

            try
            {
                // ✅ 生成缓存键（基于配置ID，确保同一配置复用同一个View和ViewModel）
                string cacheKey = node.Config?.ConfigId ?? node.Id;

                // ✅ 如果缓存中已有该节点的 View 和 ViewModel，需要检查并更新 Config 引用
                if (_viewCache.TryGetValue(cacheKey, out var cached))
                {
                    // ⚠️ 关键：如果 node.Config 的引用已改变，需要更新 ViewModel 中的 Config 引用
                    // 这样可以确保 ViewModel 始终使用最新的 node.Config，避免数据丢失
                    if (node.Config != null && cached.ViewModel != null)
                    {
                        // 尝试更新 ViewModel 的 Config 属性
                        var viewModelType = cached.ViewModel.GetType();
                        var configProperty = viewModelType.GetProperty("Config");
                        
                        if (configProperty != null)
                        {
                            var currentConfig = configProperty.GetValue(cached.ViewModel);
                            
                            // 如果 Config 引用不同，更新它
                            if (!ReferenceEquals(currentConfig, node.Config))
                            {
                                configProperty.SetValue(cached.ViewModel, node.Config);
                            }
                        }
                    }
                    
                    ContentControlChanged?.Invoke(this, cached.View);
                    return;
                }

                UserControl? configView = null;

                if (node.ViewType != null)
                {
                    try
                    {                      
                        var viewTypeFromPluginContext = node.ViewType;
                        var viewAssemblyFromPluginContext = viewTypeFromPluginContext.Assembly;
                        var assemblyName = viewAssemblyFromPluginContext.GetName();
                        var assemblyLocation = viewAssemblyFromPluginContext.Location;
          
                        Type viewTypeToUse = viewTypeFromPluginContext;
                        var defaultContextAssembly = AssemblyLoadContext.Default.Assemblies
                            .FirstOrDefault(a => a.GetName().Name == assemblyName.Name && 
                                               a.GetName().Version?.ToString() == assemblyName.Version?.ToString());
                        
                        if (defaultContextAssembly != null && defaultContextAssembly != viewAssemblyFromPluginContext)
                        {
                            // 默认上下文中存在该程序集，尝试获取相同类型
                            try
                            {
                                var defaultContextViewType = defaultContextAssembly.GetType(viewTypeFromPluginContext.FullName);
                                if (defaultContextViewType != null)
                                {
                                    viewTypeToUse = defaultContextViewType;
                                }
                            }
                            catch (Exception typeEx)
                            {
                                // 忽略类型获取错误，继续使用原始类型
                            }
                        }
                        
                        // 如果 Location 为空（可能在内存中加载的），尝试从插件目录查找
                        if (string.IsNullOrEmpty(assemblyLocation))
                        {
                            // 尝试从 ServiceProvider 获取插件目录信息
                            try
                            {
                                var pluginHost = _serviceProvider?.GetService<Astra.Core.Plugins.Abstractions.IPluginHost>();
                                if (pluginHost != null)
                                {
                                    var plugin = pluginHost.LoadedPlugins.FirstOrDefault(p => p.GetType().Assembly == viewAssemblyFromPluginContext);
                                    if (plugin != null && !string.IsNullOrEmpty(plugin.GetType().Assembly.Location))
                                    {
                                        assemblyLocation = plugin.GetType().Assembly.Location;
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                // 忽略获取程序集路径的错误
                            }
                        }
                        
                        // 如果默认上下文中没有程序集，尝试加载到默认上下文（用于资源访问）
                        if (defaultContextAssembly == null && !string.IsNullOrEmpty(assemblyLocation) && File.Exists(assemblyLocation))
                        {
                            try
                            {
                                // 使用 LoadFrom 将程序集加载到默认上下文
                                defaultContextAssembly = Assembly.LoadFrom(assemblyLocation);
                                
                                // 再次尝试从默认上下文获取类型
                                try
                                {
                                    var defaultContextViewType = defaultContextAssembly.GetType(viewTypeFromPluginContext.FullName);
                                    if (defaultContextViewType != null)
                                    {
                                        viewTypeToUse = defaultContextViewType;
                                    }
                                }
                                catch (Exception typeEx2)
                                {
                                    // 忽略类型获取错误
                                }
                            }
                            catch (Exception loadEx)
                            {
                                // 如果加载失败，继续尝试创建 View（可能已经在默认上下文中了）
                            }
                        }
                        // ⚠️ UserControl 的构造函数是无参的，不应该传入 node.Config
                        // node.Config 应该传递给 ViewModel，而不是 View
                        // ⚠️ 关键：使用默认上下文中的类型（如果有），这样 WPF 才能访问资源
                        // ⚠️ 更关键：在创建 View 之前，确保使用默认上下文中的程序集进行反射
                        // 使用 AssemblyLoadContext.EnterContextualReflection 确保 WPF 使用正确的程序集
                        var targetAssemblyForReflection = defaultContextAssembly ?? viewAssemblyFromPluginContext;
                        
                        if (targetAssemblyForReflection != null)
                        {
                            using (AssemblyLoadContext.EnterContextualReflection(targetAssemblyForReflection))
                            {
                                configView = Activator.CreateInstance(viewTypeToUse) as UserControl;
                            }
                        }
                        else
                        {
                            configView = Activator.CreateInstance(viewTypeToUse) as UserControl;
                        }
                    }
                    catch (Exception createEx)
                    {
                        // 捕获创建 View 时的异常（通常是 InitializeComponent 失败）
                        var innerEx = createEx.InnerException ?? createEx;
                        var errorMsg = $"创建 View {node.ViewType.Name} 失败: {innerEx.Message}";

                        ToastHelper.ShowError(errorMsg);
                        return;
                    }
                }

                if (configView == null || node.ViewModelType == null)
                {
                    return;
                }

                // ✅ 智能创建 ViewModel 实例：查找匹配的构造函数
                // 优先查找接受具体配置类型的构造函数，如果不存在则查找接受 IConfig 的构造函数
                object viewModel = null;

                try
                {
                    if (node.Config != null)
                    {
                        var configType = node.Config.GetType();
                        var viewModelType = node.ViewModelType;

                        // 查找接受具体配置类型的构造函数（例如：DataAcquisitionConfig）
                        var concreteConstructor = viewModelType.GetConstructor(new[] { configType });
                        if (concreteConstructor != null)
                        {
                            viewModel = Activator.CreateInstance(viewModelType, node.Config);
                        }
                        else
                        {
                            // 如果不存在，查找接受 IConfig 的构造函数
                            var interfaceConstructor = viewModelType.GetConstructor(new[] { typeof(IConfig) });
                            if (interfaceConstructor != null)
                            {
                                viewModel = Activator.CreateInstance(viewModelType, node.Config);
                            }
                            else
                            {
                                // 如果都不存在，尝试无参构造函数
                                var parameterlessConstructor = viewModelType.GetConstructor(Type.EmptyTypes);
                                if (parameterlessConstructor != null)
                                {
                                    viewModel = Activator.CreateInstance(viewModelType);

                                    // 尝试通过属性设置 Config（如果 ViewModel 有 Config 属性）
                                    var configProperty = viewModelType.GetProperty("Config") ??
                                                        viewModelType.GetProperty("SelectedSensor") ??
                                                        viewModelType.GetProperty("SelectedConfig");
                                    if (configProperty != null)
                                    {
                                        configProperty.SetValue(viewModel, node.Config);
                                    }
                                }
                            }
                        }
                    }

                if (viewModel == null)
                {
                    var errorMsg = $"无法创建 ViewModel 实例: {node.ViewModelType.Name}，未找到合适的构造函数";
                    ToastHelper.ShowError(errorMsg);
                    return;
                }
                }
                catch (Exception vmEx)
                {
                    // 捕获创建 ViewModel 时的异常
                    var innerEx = vmEx.InnerException ?? vmEx;
                    var errorMsg = $"创建 ViewModel {node.ViewModelType.Name} 失败: {innerEx.Message}";

                    ToastHelper.ShowError(errorMsg);
                    return;
                }

                configView.DataContext = viewModel;
                
                // ✅ 将创建的 View 和 ViewModel 添加到缓存中
                if (configView != null && viewModel != null)
                {
                    _viewCache[cacheKey] = (configView, viewModel);
                }
                
                ContentControlChanged?.Invoke(this, configView);
            }
            catch (Exception ex)
            {
                // 记录详细错误信息，帮助调试
                // ⚠️ Activator.CreateInstance 会将构造函数中的异常包装为 TargetInvocationException
                // 需要检查 InnerException 来获取真正的错误信息
                var actualException = ex.InnerException ?? ex;

                // 如果内部异常有更详细的信息，显示内部异常的消息
                var errorMessage = actualException is TargetInvocationException ? actualException.InnerException?.Message ?? actualException.Message : actualException.Message;
                ToastHelper.ShowError($"加载配置界面时发生错误: {errorMessage}");
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
            string newNodeName = nodeAutoNaming.GenerateUniqueName(node);

            var newConfig = Activator.CreateInstance(node.ConfigType!, Guid.NewGuid().ToString()) as IConfig;
            if (newConfig != null)
            {
                // ✅ 确保配置名称与树节点名称一致
                newConfig.ConfigName = newNodeName;

                // ✅ 如果配置是设备配置（DeviceConfig），也要设置设备名称
                if (newConfig is Astra.Core.Devices.Configuration.DeviceConfig deviceConfig)
                {
                    deviceConfig.DeviceName = newNodeName;
                }
            }

            TreeNode newNode = new TreeNode
            {
                Header = newNodeName,
                Icon = attr.Icon ?? _defaultIcon,
                ViewModelType = attr.ViewModelType,
                ViewType = attr.ViewType,
                ShowAddButton = false,
                ShowDeleteButton = true,
                ConfigType = node.ConfigType,
                Config = newConfig,
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

                // ✅ 清理已删除节点的缓存
                string deletedCacheKey = node.Config?.ConfigId ?? node.Id;
                if (_viewCache.ContainsKey(deletedCacheKey))
                {
                    _viewCache.Remove(deletedCacheKey);
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
                MessageBoxHelper.ShowError($"无法删除节点: {ex.Message}", "错误");
            }
        }

        /// <summary>
        /// 保存单个配置命令（右键菜单使用）
        /// </summary>
        [RelayCommand]
        private async Task SaveSingleConfiguration(TreeNode? node)
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
                ToastHelper.ShowError($"保存配置失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 导入配置命令（根节点右键菜单使用）
        /// 重构后：使用 ConfigImportExportHelper 简化逻辑，提高可读性和可维护性
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

                // 获取导入导出助手
                var helper = _serviceProvider?.GetService<ConfigImportExportHelper>();
                if (helper == null)
                {
                    ToastHelper.ShowError("导入导出服务未初始化");
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

                // ✅ 使用 ConfigImportExportHelper 简化导入逻辑
                var successCount = 0;
                var failureCount = 0;
                var errors = new List<string>();
                var allImportedConfigs = new List<IConfig>();

                // 处理每个文件
                foreach (var filePath in filePaths)
                {
                    try
                    {
                        // 使用助手类导入配置（类型验证和反序列化逻辑已封装）
                        var result = await helper.ImportConfigsFromFileAsync(
                            filePath,
                            targetNode.ConfigType,
                            generateNewId: true);

                        if (result.Success && result.Data != null)
                        {
                            allImportedConfigs.AddRange(result.Data);
                            successCount += result.Data.Count;
                            }
                            else
                            {
                                                failureCount++;
                            errors.Add($"{Path.GetFileName(filePath)}: {result.Message}");
                        }
                            }
                            catch (Exception ex)
                            {
                                failureCount++;
                        errors.Add($"{Path.GetFileName(filePath)}: {ex.Message}");
                            }
                        }

                        // 获取配置类型的属性信息（用于创建树节点）
                        var attr = targetNode.ConfigType.GetCustomAttribute<TreeNodeConfigAttribute>();
                        if (attr == null)
                        {
                    ToastHelper.ShowError("配置类型缺少 TreeNodeConfigAttribute");
                    return;
                        }

                // 将所有导入的配置添加到树节点（不保存到配置管理器）
                foreach (var config in allImportedConfigs)
                        {
                            try
                            {
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
                            Config = config,
                                    Order = attr.Order,
                            ConfigType = config.ConfigType,
                                    Parent = targetNode,
                                };

                                // 添加到根节点的子节点集合
                                targetNode.Children.Add(childNode);
                                
                        // 确保根节点展开
                                targetNode.IsExpanded = true;
                            }
                            catch (Exception ex)
                            {
                                failureCount++;
                                errors.Add($"{config.ConfigName ?? "未知配置"}: {ex.Message}");
                    }
                }

                // 显示结果
                if (failureCount == 0 && successCount > 0)
                {
                    ToastHelper.ShowSuccess($"已成功导入 {successCount} 个配置");
                }
                else if (failureCount > 0)
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

                // ✅ 使用 ConfigImportExportHelper 简化导出逻辑
                var helper = _serviceProvider?.GetService<ConfigImportExportHelper>();
                if (helper == null)
                {
                    ToastHelper.ShowError("导入导出服务未初始化");
                    return;
                }

                var result = await helper.ExportConfigsToFileAsync(
                    configs,
                    exportFilePath,
                    ExportFormat.JsonArray);

                // 显示导出结果
                if (result.Success)
                {
                    ToastHelper.ShowSuccess($"已成功导出 {configs.Count} 个配置");
                }
                else
                {
                    ToastHelper.ShowError($"导出失败: {result.Message}");
                }
            }
            catch (Exception ex)
            {
                ToastHelper.ShowError($"导出配置失败: {ex.Message}");
            }
        }


    }
}
