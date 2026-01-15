using Astra.Core.Nodes.Models;
using Astra.Core.Plugins.Abstractions;
using Astra.Core.Plugins.Manifest.Serializers;
using Astra.UI.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using System.Collections.Generic;
using System.Linq;
using Astra.Core.Nodes.Geometry;
using System.Windows.Controls;
using Microsoft.Win32;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Astra.UI.Helpers;
using Astra.UI.Models;
using Astra.UI.Services;
using Astra.UI.Commands;
using Astra.Core.Nodes.Serialization;
using Astra.Core.Nodes.Models;
using System.IO;

namespace Astra.UI.ViewModels
{
    public partial class MultiFlowEditorViewModel : ObservableObject
    {       
        #region 常量定义

        /// <summary>
        /// 流程文件扩展名
        /// </summary>
        private const string FILE_EXTENSION = ".sol";

        /// <summary>
        /// 流程文件过滤器
        /// </summary>
        private const string FILE_FILTER = "流程项目文件 (*.sol)|*.sol";

        /// <summary>
        /// Solutions 文件夹名称（在 bin 目录下）
        /// </summary>
        private const string SOLUTIONS_FOLDER_NAME = "Solutions";

        /// <summary>
        /// 获取 Solutions 文件夹的完整路径（确保路径格式正确）
        /// </summary>
        private static string SolutionsFolder
        {
            get
            {
                // 使用 AppDomain.CurrentDomain.BaseDirectory 已经是 bin 目录
                var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                var folder = Path.Combine(baseDir, SOLUTIONS_FOLDER_NAME);
                
                // 确保文件夹存在
                if (!Directory.Exists(folder))
                {
                    try
                    {
                        Directory.CreateDirectory(folder);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[SolutionsFolder] 创建文件夹失败: {ex.Message}");
                    }
                }
                
                // 返回完整路径（解决 SaveFileDialog 的路径格式问题）
                return Path.GetFullPath(folder);
            }
        }

        #endregion

        #region 字段
       
        private PluginNodeService _pluginNodeService;

        private IPluginHost _pluginHost;

        private IManifestSerializer _manifestSerializer;

        /// <summary>
        /// 多流程序列化服务
        /// </summary>
        private readonly IMultiWorkflowSerializer _workflowSerializer;

        /// <summary>
        /// 当前文件路径（如果已保存）
        /// </summary>
        [ObservableProperty]
        private string _currentFilePath;

        /// <summary>
        /// 全局剪贴板：存储复制的节点（支持跨流程复制粘贴）
        /// </summary>
        [ObservableProperty]
        private List<Node> _globalClipboardNodes = new List<Node>();

        /// <summary>
        /// 全局剪贴板：存储复制的连线（支持跨流程复制粘贴）
        /// </summary>
        [ObservableProperty]
        private List<Edge> _globalClipboardEdges = new List<Edge>();

        /// <summary>
        /// 全局剪贴板：存储复制节点的边界框（用于保持粘贴时的相对位置）
        /// </summary>
        [ObservableProperty]
        private Rect _globalClipboardBounds = Rect.Empty;

        // 用于防止重复关闭标签页的集合
        private readonly HashSet<string> _closingTabs = new HashSet<string>();

        /// <summary>
        /// 命令管理器 - 管理所有可撤销命令的执行、撤销和重做
        /// </summary>
        private readonly CommandManager _commandManager;

        /// <summary>
        /// 获取全局 CommandManager（供外部访问）
        /// </summary>
        public CommandManager GetGlobalCommandManager() => _commandManager;

        [ObservableProperty]
        private string _title = "序列配置";

        [ObservableProperty]
        private bool _isNavigating = false;

        /// <summary>
        /// 工具箱数据源 - 工具类别集合
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<ToolCategory> _toolBoxItemsSource;

        /// <summary>
        /// 画布数据源 - 节点集合（使用 Node 基类）
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<Node> _canvasItemsSource;

        /// <summary>
        /// 连线数据源 - 连线集合（使用 Edge 类）
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<Edge> _edgeItemsSource;

        #region 多流程管理属性

        /// <summary>
        /// 流程标签页集合（包含主流程和所有子流程）
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<WorkflowTab> _workflowTabs;

        /// <summary>
        /// 子流程标签页集合（用于标签栏显示，不包含主流程）
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<WorkflowTab> _subWorkflowTabs;

        /// <summary>
        /// 当前活动标签页
        /// </summary>
        [ObservableProperty]
        private WorkflowTab _currentTab;

        partial void OnCurrentTabChanged(WorkflowTab value)
        {
            Debug.WriteLine($"[OnCurrentTabChanged] CurrentTab 变更: {value?.Name ?? "null"}");
            
            // 如果新标签页不为空，执行切换逻辑
            if (value != null)
            {
                Debug.WriteLine($"[OnCurrentTabChanged] 调用 SwitchWorkflow");
                
                // 保存旧标签页的状态
                var oldTab = SubWorkflowTabs.FirstOrDefault(t => t.IsActive && t != value);
                if (oldTab != null)
                {
                    Debug.WriteLine($"[OnCurrentTabChanged] 保存旧标签页: {oldTab.Name}");
                    oldTab.IsActive = false;
                    SaveCurrentTabState();
                }
                
                // 确保所有其他标签页都是非活动状态
                foreach (var tab in SubWorkflowTabs)
                {
                    if (tab != value)
                    {
                        tab.IsActive = false;
                    }
                }
                
                // 激活新标签页
                value.IsActive = true;
                IsMasterWorkflow = value.Type == WorkflowType.Master;
                
                // 同步到画布
                SyncTabToCanvas(value);
                
                Debug.WriteLine($"[OnCurrentTabChanged] 切换完成，新标签页: {value.Name}, IsActive: {value.IsActive}");
                
                // 打印所有标签页的状态
                Debug.WriteLine($"[OnCurrentTabChanged] 所有标签页状态:");
                foreach (var t in SubWorkflowTabs)
                {
                    Debug.WriteLine($"[OnCurrentTabChanged]   {t.Name}: Nodes 哈希={t.Nodes.GetHashCode()}, 节点数={t.Nodes.Count}, IsActive={t.IsActive}");
                }
            }
            
            // 当当前标签页改变时，更新启动命令的可用性
            StartCurrentSubWorkflowCommand.NotifyCanExecuteChanged();
            
            // 更新撤销/重做命令的可用性（因为切换流程后，CommandManager 可能不同）
            UndoCommand.NotifyCanExecuteChanged();
            RedoCommand.NotifyCanExecuteChanged();
            
            // 订阅全局 CommandManager 的属性变更事件（如果还没有订阅）
            if (_commandManager != null)
            {
                _commandManager.PropertyChanged -= OnCommandManagerPropertyChanged;
                _commandManager.PropertyChanged += OnCommandManagerPropertyChanged;
            }
            
            // 如果新标签页有 CommandManager，订阅其属性变更事件
            if (value?.CommandManager != null)
            {
                value.CommandManager.PropertyChanged -= OnFlowCommandManagerPropertyChanged;
                value.CommandManager.PropertyChanged += OnFlowCommandManagerPropertyChanged;
            }
        }

        /// <summary>
        /// 全局 CommandManager 属性变更事件处理
        /// </summary>
        private void OnCommandManagerPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(CommandManager.CanUndo) || e.PropertyName == nameof(CommandManager.CanRedo))
            {
                UndoCommand.NotifyCanExecuteChanged();
                RedoCommand.NotifyCanExecuteChanged();
            }
        }

        /// <summary>
        /// 流程 CommandManager 属性变更事件处理
        /// </summary>
        private void OnFlowCommandManagerPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(CommandManager.CanUndo) || e.PropertyName == nameof(CommandManager.CanRedo))
            {
                UndoCommand.NotifyCanExecuteChanged();
                RedoCommand.NotifyCanExecuteChanged();
            }
        }

        /// <summary>
        /// 主流程数据
        /// </summary>
        [ObservableProperty]
        private MasterWorkflow _masterWorkflow;

        /// <summary>
        /// 子流程字典（Key: 流程ID，Value: WorkFlowNode）
        /// </summary>
        [ObservableProperty]
        private Dictionary<string, WorkFlowNode> _subWorkflows;

        /// <summary>
        /// 全局变量池
        /// </summary>
        [ObservableProperty]
        private GlobalVariablePool _globalVariables;

        /// <summary>
        /// 当前是否为主流程视图
        /// </summary>
        [ObservableProperty]
        private bool _isMasterWorkflow;

        /// <summary>
        /// 是否显示主流程编辑界面（独立于 TabControl）
        /// </summary>
        [ObservableProperty]
        private bool _isMasterWorkflowViewVisible = false;

        partial void OnIsMasterWorkflowViewVisibleChanged(bool value)
        {
            // 当主流程编辑界面显示状态改变时，更新启动命令的可用性
            StartCurrentSubWorkflowCommand.NotifyCanExecuteChanged();
        }

        /// <summary>
        /// 主流程标签页（独立管理，不添加到 WorkflowTabs）
        /// </summary>
        [ObservableProperty]
        private WorkflowTab _masterWorkflowTab;

        /// <summary>
        /// 状态消息
        /// </summary>
        [ObservableProperty]
        private string _statusMessage = "";

        /// <summary>
        /// 流程执行时间（毫秒）
        /// </summary>
        [ObservableProperty]
        private double _processTime = 0.0;

        /// <summary>
        /// 缩放百分比
        /// </summary>
        [ObservableProperty]
        private double _zoomPercentage = 100.0;

        /// <summary>
        /// 是否锁定画布
        /// </summary>
        [ObservableProperty]
        private bool _isLocked = false;

        /// <summary>
        /// 是否全屏
        /// </summary>
        [ObservableProperty]
        private bool _isFullscreen = false;

        /// <summary>
        /// 是否显示启动按钮（子流程编辑界面）
        /// </summary>
        [ObservableProperty]
        private bool _isStartButtonVisible = true;

        #endregion

        public MultiFlowEditorViewModel(IPluginHost pluginHost, IManifestSerializer manifestSerializer)
        {
            _pluginHost = pluginHost;
            _manifestSerializer = manifestSerializer;
            
            // 初始化多流程序列化服务
            _workflowSerializer = new MultiWorkflowSerializer();
            
            // 初始化命令管理器
            _commandManager = new CommandManager(maxHistorySize: 100);
            _commandManager.PropertyChanged += OnCommandManagerPropertyChanged;

            // 初始化插件节点服务
            InitializePluginNodeService();

            // 初始化数据源
            InitializeDataSources();

            // 初始化流程管理
            InitializeWorkflowManagement();
        }

        /// <summary>
        /// 初始化流程管理
        /// </summary>
        private void InitializeWorkflowManagement()
        {
            // 初始化流程标签页集合
            WorkflowTabs = new ObservableCollection<WorkflowTab>();

            // 初始化子流程标签页集合（用于标签栏显示）
            SubWorkflowTabs = new ObservableCollection<WorkflowTab>();

            // 初始化子流程字典
            SubWorkflows = new Dictionary<string, WorkFlowNode>();

            // 初始化全局变量池
            GlobalVariables = new GlobalVariablePool();

            // 创建主流程数据（但不创建标签页，需要点击按钮才打开）
            MasterWorkflow = new MasterWorkflow
            {
                Name = "主流程"
            };

            // 不设置 CurrentTab，初始状态为空
            CurrentTab = null;
            IsMasterWorkflow = false;

            // 创建默认子流程
            AddNewWorkflow(WorkflowType.Sub);
        }

        /// <summary>
        /// 初始化插件节点服务
        /// </summary>
        private void InitializePluginNodeService()
        {
            try
            {
                // 从服务提供者获取 IPluginHost
                var pluginHost = _pluginHost;

                if (pluginHost == null)
                {
                    Debug.WriteLine("[SequenceViewModel] 警告：无法从服务提供者获取 IPluginHost，插件节点功能将不可用");
                    return;
                }

                //// 获取清单序列化器（如果已注册）
                //// 注意：如果服务提供者中没有注册序列化器，PluginNodeService 会使用默认的 XML 序列化器
                var serializers = new List<IManifestSerializer>();
                try
                {
                    // 尝试从服务提供者获取序列化器
                    var xmlSerializer = _manifestSerializer;
                    if (xmlSerializer != null)
                        serializers.Add(xmlSerializer);
                }
                catch { }

                // 创建插件节点服务
                _pluginNodeService = new PluginNodeService(pluginHost, serializers.Count > 0 ? serializers : null);

                Debug.WriteLine($"[SequenceViewModel] 插件节点服务已初始化，已加载插件数量: {pluginHost.LoadedPlugins.Count}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SequenceViewModel] 初始化插件节点服务失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 初始化数据源
        /// </summary>
        private void InitializeDataSources()
        {
            // ⭐ 如果数据源已存在且不为空，则不重新初始化（单例模式下保持数据）
            if (ToolBoxItemsSource != null && ToolBoxItemsSource.Count > 0)
            {
                Debug.WriteLine("[SequenceViewModel] 数据源已存在，跳过初始化");
                return;
            }

            // 初始化工具箱数据源
            ToolBoxItemsSource = new ObservableCollection<ToolCategory>();

            // 初始化画布数据源（如果为空则创建新集合，否则保持现有数据）
            if (CanvasItemsSource == null)
            {
                CanvasItemsSource = new ObservableCollection<Node>();
            }

            // 初始化连线数据源（如果为空则创建新集合，否则保持现有数据）
            if (EdgeItemsSource == null)
            {
                EdgeItemsSource = new ObservableCollection<Edge>();
            }

            // 创建示例工具类别
            CreateSampleToolCategories();

            // 从插件加载节点工具类别
            LoadToolCategoriesFromPlugins();
        }

        /// <summary>
        /// 创建示例工具类别
        /// </summary>
        private void CreateSampleToolCategories()
        {
            Debug.WriteLine("[SequenceViewModel] 开始创建示例工具类别");

            // 基础节点类别
            var basicCategory = new ToolCategory
            {
                Name = "基础节点",
                IconCode = FlowEditorIcons.BasicCategory,
                Description = "基础流程节点",
                CategoryColor = Application.Current?.FindResource("PrimaryBrush") as Brush,
                CategoryLightColor = Application.Current?.FindResource("LightPrimaryBrush") as Brush
            };
            basicCategory.Tools.Add(new ToolItem
            {
                Name = "开始",
                IconCode = FlowEditorIcons.Start,
                Description = "流程开始节点",
                NodeType = typeof(WorkFlowNode).FullName // 使用 WorkFlowNode 作为默认类型
            });
            basicCategory.Tools.Add(new ToolItem
            {
                Name = "结束",
                IconCode = FlowEditorIcons.End,
                Description = "流程结束节点",
                NodeType = typeof(WorkFlowNode).FullName // 使用 WorkFlowNode 作为默认类型
            });
            basicCategory.Tools.Add(new ToolItem
            {
                Name = "等待",
                IconCode = FlowEditorIcons.Wait,
                Description = "等待节点",
                NodeType = typeof(WorkFlowNode).FullName // 使用 WorkFlowNode 作为默认类型
            });
            ToolBoxItemsSource.Add(basicCategory);
            Debug.WriteLine($"[SequenceViewModel] 添加基础节点类别: {basicCategory.Name}, 工具数量: {basicCategory.Tools.Count}");

            // 逻辑节点类别
            var logicCategory = new ToolCategory
            {
                Name = "逻辑节点",
                IconCode = FlowEditorIcons.LogicCategory,
                Description = "逻辑控制节点",
                CategoryColor = Application.Current?.FindResource("PrimaryBrush") as Brush,
                CategoryLightColor = Application.Current?.FindResource("LightPrimaryBrush") as Brush
            };

            logicCategory.Tools.Add(new ToolItem
            {
                Name = "条件判断",
                IconCode = FlowEditorIcons.Condition,
                Description = "条件判断节点",
                NodeType = typeof(WorkFlowNode).FullName // 暂时使用 WorkFlowNode 作为默认类型
            });
            logicCategory.Tools.Add(new ToolItem
            {
                Name = "循环",
                IconCode = FlowEditorIcons.Loop,
                Description = "循环节点",
                NodeType = typeof(WorkFlowNode).FullName // 暂时使用 WorkFlowNode 作为默认类型
            });
            logicCategory.Tools.Add(new ToolItem
            {
                Name = "并行",
                IconCode = FlowEditorIcons.Parallel,
                Description = "并行执行节点",
                NodeType = typeof(WorkFlowNode).FullName // 暂时使用 WorkFlowNode 作为默认类型
            });
            ToolBoxItemsSource.Add(logicCategory);
            Debug.WriteLine($"[SequenceViewModel] 添加逻辑节点类别: {logicCategory.Name}, 工具数量: {logicCategory.Tools.Count}");

            // 设备节点类别
            var deviceCategory = new ToolCategory
            {
                Name = "设备节点",
                IconCode = FlowEditorIcons.DeviceCategory,
                Description = "设备操作节点",
                CategoryColor = Application.Current?.FindResource("PrimaryBrush") as Brush,
                CategoryLightColor = Application.Current?.FindResource("LightPrimaryBrush") as Brush
            };
            deviceCategory.Tools.Add(new ToolItem
            {
                Name = "PLC控制",
                IconCode = FlowEditorIcons.PLC,
                Description = "PLC控制节点",
                NodeType = typeof(WorkFlowNode).FullName // 暂时使用 WorkFlowNode 作为默认类型
            });
            deviceCategory.Tools.Add(new ToolItem
            {
                Name = "扫码枪",
                IconCode = FlowEditorIcons.Scanner,
                Description = "扫码枪节点",
                NodeType = typeof(WorkFlowNode).FullName // 暂时使用 WorkFlowNode 作为默认类型
            });
            deviceCategory.Tools.Add(new ToolItem
            {
                Name = "传感器",
                IconCode = FlowEditorIcons.Sensor,
                Description = "传感器节点",
                NodeType = typeof(WorkFlowNode).FullName // 暂时使用 WorkFlowNode 作为默认类型
            });
            ToolBoxItemsSource.Add(deviceCategory);
            Debug.WriteLine($"[SequenceViewModel] 添加设备节点类别: {deviceCategory.Name}, 工具数量: {deviceCategory.Tools.Count}");

            Debug.WriteLine($"[SequenceViewModel] 工具类别总数: {ToolBoxItemsSource.Count}");
        }

        /// <summary>
        /// 从插件系统加载工具类别
        /// </summary>
        private void LoadToolCategoriesFromPlugins()
        {
            if (_pluginNodeService == null)
            {
                Debug.WriteLine("[SequenceViewModel] 插件节点服务未初始化，跳过插件节点加载");
                return;
            }

            try
            {
                var pluginCategories = _pluginNodeService.GetToolCategoriesFromPlugins();

                foreach (var category in pluginCategories)
                {
                    ToolBoxItemsSource.Add(category);
                    Debug.WriteLine($"[SequenceViewModel] 从插件添加工具类别: {category.Name}, 工具数量: {category.Tools.Count}");
                }

                Debug.WriteLine($"[SequenceViewModel] 从插件加载的工具类别总数: {pluginCategories.Count}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SequenceViewModel] 从插件加载工具类别失败: {ex.Message}");
            }
        }

        #region 私有方法

        /// <summary>
        /// 初始化序列页面
        /// </summary>
        private void InitializeSequencePage()
        {
            Debug.WriteLine("[SequenceViewModel] 开始初始化序列页面");

            // ⭐ 确保数据源已初始化（单例模式下，数据应该已存在）
            if (ToolBoxItemsSource == null || ToolBoxItemsSource.Count == 0)
            {
                Debug.WriteLine("[SequenceViewModel] 检测到数据源为空，重新初始化");
                InitializeDataSources();
            }

            if (CanvasItemsSource == null)
            {
                Debug.WriteLine("[SequenceViewModel] 检测到画布数据源为空，重新初始化");
                CanvasItemsSource = new ObservableCollection<Node>();
            }

            if (EdgeItemsSource == null)
            {
                Debug.WriteLine("[SequenceViewModel] 检测到连线数据源为空，重新初始化");
                EdgeItemsSource = new ObservableCollection<Edge>();
            }

            Debug.WriteLine($"[SequenceViewModel] 序列页面初始化完成 - 工具箱: {ToolBoxItemsSource?.Count ?? 0} 个类别, 画布: {CanvasItemsSource?.Count ?? 0} 个节点, 连线: {EdgeItemsSource?.Count ?? 0} 条");
        }

        /// <summary>
        /// 处理导航失败
        /// </summary>
        private void HandleNavigationFailure(Exception exception, string targetPage)
        {
            Debug.WriteLine($"[SequenceViewModel] 处理导航失败: {targetPage}");

            // 这里可以添加失败处理逻辑
            // 例如：显示错误消息、尝试重新导航等

            if (exception != null)
            {
                Debug.WriteLine($"[SequenceViewModel] 异常详情: {exception}");
            }
        }

        #endregion

        #region 流程管理命令

        /// <summary>
        /// 获取下一个可用的流程名（从1开始，不重名）
        /// </summary>
        private string GetNextAvailableWorkflowName()
        {
            // 获取所有现有的流程名
            var existingNames = new HashSet<string>();

            // 从 SubWorkflows 字典中获取所有流程名
            foreach (var subWorkflow in SubWorkflows.Values)
            {
                if (!string.IsNullOrEmpty(subWorkflow.Name))
                {
                    existingNames.Add(subWorkflow.Name);
                }
            }

            // 从 SubWorkflowTabs 中获取所有标签页名（作为备用检查）
            foreach (var tab in SubWorkflowTabs)
            {
                if (!string.IsNullOrEmpty(tab.Name))
                {
                    existingNames.Add(tab.Name);
                }
            }

            // 从1开始查找第一个未使用的编号
            int number = 1;
            string workflowName;
            while (number <= 10000) // 防止无限循环
            {
                workflowName = $"流程{number}";
                if (!existingNames.Contains(workflowName))
                {
                    // 找到未使用的名称，返回
                    return workflowName;
                }
                number++;
            }

            // 如果所有编号都被使用（理论上不会发生），返回带时间戳的名称
            return $"流程{DateTime.Now:yyyyMMddHHmmss}";
        }

        /// <summary>
        /// 创建新流程命令（仅用于创建子流程，主流程通过 OpenMasterWorkflow 管理）
        /// 使用命令模式实现，支持撤销/重做
        /// </summary>
        [RelayCommand]
        private void AddNewWorkflow(WorkflowType? workflowType)
        {
            var type = workflowType ?? WorkflowType.Sub;

            // 不允许通过此方法创建主流程
            if (type == WorkflowType.Master)
            {
                Debug.WriteLine("[SequenceViewModel] 警告：不能通过 AddNewWorkflow 创建主流程，请使用 OpenMasterWorkflow");
                return;
            }

            // 获取下一个可用的流程名（从1开始，不重名）
            var workflowName = GetNextAvailableWorkflowName();

            // 创建新的子流程（使用 WorkFlowNode）
            var subWorkflow = new WorkFlowNode
            {
                Name = workflowName,
                Description = $"子流程: {workflowName}"
            };

            // 将 WorkFlowNode 的 Nodes 和 Connections 转换为画布需要的格式
            var nodes = new ObservableCollection<Node>(subWorkflow.Nodes ?? new List<Node>());
            var edges = new ObservableCollection<Edge>();

            // 将 Connections 转换为 Edges（如果需要）
            // 注意：WorkFlowNode 使用 Connection，画布使用 Edge
            // 这里暂时只同步 Nodes，Edges 需要根据实际需求转换

            var newTab = new WorkflowTab
            {
                Name = workflowName,
                Type = WorkflowType.Sub,
                WorkflowData = subWorkflow,
                Nodes = nodes,
                Edges = edges,
                IsActive = false // 初始化为非活动状态
            };

            Debug.WriteLine($"[AddNewWorkflow] 创建新标签页: {workflowName}");
            Debug.WriteLine($"[AddNewWorkflow]   Nodes 集合哈希: {nodes.GetHashCode()}, 节点数: {nodes.Count}");
            Debug.WriteLine($"[AddNewWorkflow]   Edges 集合哈希: {edges.GetHashCode()}, 连线数: {edges.Count}");

            // 取消所有标签页的活动状态（确保只有新标签页是活动的）
            foreach (var existingTab in SubWorkflowTabs)
            {
                Debug.WriteLine($"[AddNewWorkflow] 设置 {existingTab.Name} 为非活动，节点数: {existingTab.Nodes.Count}");
                existingTab.IsActive = false;
            }

            // 添加新标签页并设置为活动
            newTab.IsActive = true;
            newTab.IsModified = true;
            Debug.WriteLine($"[AddNewWorkflow] 新标签页设置为活动: {newTab.Name}, IsActive: {newTab.IsActive}");


            // 创建添加流程命令
            var addCommand = new AddWorkflowCommand(
                newTab,
                WorkflowTabs,
                SubWorkflowTabs,
                SubWorkflows,
                onWorkflowAdded: (tab) =>
                {
                    // 流程添加后的回调：设置当前标签页并同步到画布
                    CurrentTab = tab;
                    IsMasterWorkflow = false;
                    SyncTabToCanvas(tab);

                    // 如果主流程编辑界面已打开，刷新主流程节点
                    if (IsMasterWorkflowViewVisible)
                    {
                        RefreshMasterWorkflowNodes();
                    }
                },
                onWorkflowRemoved: (removedTab) =>
                {
                    // 流程移除后的回调：如果被删除的是当前标签页，切换到其他标签页
                    if (removedTab == CurrentTab)
                    {
                        // 切换到第一个可用的子流程标签页
                        var otherTab = SubWorkflowTabs.FirstOrDefault(t => t != removedTab);
                        if (otherTab != null)
                        {
                            SwitchWorkflow(otherTab);
                        }
                        else
                        {
                            // 没有其他标签页，设置为 null
                            CurrentTab = null;
                        }
                    }
                    
                    // 刷新主流程节点
                    if (IsMasterWorkflowViewVisible)
                    {
                        RefreshMasterWorkflowNodes();
                    }
                }
            );

            // 通过命令管理器执行命令
            _commandManager.Execute(addCommand);

            Debug.WriteLine($"[SequenceViewModel] 创建新流程: {workflowName}, 类型: {type}");
        }

        /// <summary>
        /// 切换流程标签页命令
        /// </summary>
        [RelayCommand]
        private void SwitchWorkflow(WorkflowTab tab)
        {
            if (tab == null || tab == CurrentTab)
                return;

            Debug.WriteLine($"[SwitchWorkflow] === 开始切换到流程: {tab.Name} ===");
            Debug.WriteLine($"[SwitchWorkflow]   目标 Tab.Nodes 哈希: {tab.Nodes.GetHashCode()}, 节点数: {tab.Nodes.Count}");
            Debug.WriteLine($"[SwitchWorkflow]   目标 Tab.Edges 哈希: {tab.Edges.GetHashCode()}, 连线数: {tab.Edges.Count}");

            // 保存当前标签页的修改状态
            if (CurrentTab != null)
            {
                Debug.WriteLine($"[SwitchWorkflow]   当前 Tab: {CurrentTab.Name}");
                Debug.WriteLine($"[SwitchWorkflow]   当前 Tab.Nodes 哈希: {CurrentTab.Nodes.GetHashCode()}, 节点数: {CurrentTab.Nodes.Count}");
                Debug.WriteLine($"[SwitchWorkflow]   当前 Tab.Edges 哈希: {CurrentTab.Edges.GetHashCode()}, 连线数: {CurrentTab.Edges.Count}");
                CurrentTab.IsActive = false;
                SaveCurrentTabState();
            }

            // 确保所有其他标签页都是非活动状态
            foreach (var existingTab in SubWorkflowTabs)
            {
                if (existingTab != tab)
                {
                    existingTab.IsActive = false;
                }
            }

            // 切换到新标签页
            tab.IsActive = true;
            CurrentTab = tab;
            IsMasterWorkflow = tab.Type == WorkflowType.Master;

            // 同步到画布
            SyncTabToCanvas(tab);

            Debug.WriteLine($"[SwitchWorkflow] === 切换完成 ===");
            Debug.WriteLine($"[SwitchWorkflow]   CurrentTab: {CurrentTab.Name}, Nodes 哈希: {CurrentTab.Nodes.GetHashCode()}, 节点数: {CurrentTab.Nodes.Count}");
            
            // 打印所有标签页的集合状态（用于诊断）
            Debug.WriteLine($"[SwitchWorkflow] 所有标签页状态:");
            foreach (var t in SubWorkflowTabs)
            {
                Debug.WriteLine($"[SwitchWorkflow]   {t.Name}: Nodes 哈希={t.Nodes.GetHashCode()}, 节点数={t.Nodes.Count}, IsActive={t.IsActive}");
            }
        }

        /// <summary>
        /// 关闭其他流程标签页命令
        /// </summary>
        [RelayCommand]
        private void CloseOtherWorkflows(WorkflowTab tab)
        {
            if (tab == null)
            {
                Debug.WriteLine("[SequenceViewModel] 关闭其他流程失败: tab 为 null");
                return;
            }

            Debug.WriteLine($"[SequenceViewModel] 关闭除 {tab.Name} 外的所有流程");

            // 获取所有其他子流程标签页
            var otherTabs = SubWorkflowTabs.Where(t => t != tab).ToList();

            foreach (var otherTab in otherTabs)
            {
                CloseWorkflow(otherTab);
            }
        }

        /// <summary>
        /// 关闭所有流程标签页命令
        /// </summary>
        [RelayCommand]
        private void CloseAllWorkflows()
        {
            Debug.WriteLine("[SequenceViewModel] 关闭所有流程");

            // 获取所有子流程标签页的副本
            var allTabs = SubWorkflowTabs.ToList();

            foreach (var tab in allTabs)
            {
                CloseWorkflow(tab);
            }
        }

        /// <summary>
        /// 关闭流程标签页命令
        /// </summary>
        [RelayCommand]
        private void CloseWorkflow(object parameter)
        {
            Debug.WriteLine($"[SequenceViewModel] CloseWorkflow 命令被调用，参数类型: {parameter?.GetType().Name ?? "null"}");

            // 从参数中提取 WorkflowTab
            WorkflowTab tab = null;

            // 如果参数是 WorkflowTab，直接使用
            if (parameter is WorkflowTab workflowTab)
            {
                tab = workflowTab;
            }
            // 如果参数是 RoutedEventArgs，尝试从 Source 或 OriginalSource 获取
            else if (parameter is RoutedEventArgs routedEventArgs)
            {
                Debug.WriteLine($"[SequenceViewModel] 收到 RoutedEventArgs: Source={routedEventArgs.Source?.GetType().Name}, OriginalSource={routedEventArgs.OriginalSource?.GetType().Name}");

                // 辅助方法：向上遍历视觉树查找 DataContext 是 WorkflowTab 的元素
                Func<DependencyObject, WorkflowTab> findWorkflowTab = (element) =>
                {
                    if (element == null) return null;

                    var current = element;
                    while (current != null)
                    {
                        if (current is FrameworkElement fe)
                        {
                            // 检查 DataContext
                            if (fe.DataContext is WorkflowTab wt)
                            {
                                Debug.WriteLine($"[SequenceViewModel] 在 {fe.GetType().Name} 中找到 WorkflowTab: {wt.Name}");
                                return wt;
                            }

                            // 检查 Content（某些控件可能将 WorkflowTab 作为 Content）
                            if (fe is ContentControl cc && cc.Content is WorkflowTab wt2)
                            {
                                Debug.WriteLine($"[SequenceViewModel] 在 {fe.GetType().Name} 的 Content 中找到 WorkflowTab: {wt2.Name}");
                                return wt2;
                            }
                        }

                        // 向上遍历
                        current = System.Windows.Media.VisualTreeHelper.GetParent(current);
                    }
                    return null;
                };

                // 尝试从 Source 获取
                if (routedEventArgs.Source is DependencyObject sourceObj)
                {
                    tab = findWorkflowTab(sourceObj);
                }

                // 如果从 Source 没找到，尝试从 OriginalSource 获取
                if (tab == null && routedEventArgs.OriginalSource is DependencyObject originalObj)
                {
                    tab = findWorkflowTab(originalObj);
                }

                // 如果还是没找到，尝试从事件的 Sender 获取（如果事件是从 TabItem 触发的）
                if (tab == null && routedEventArgs.Source is HandyControl.Controls.TabItem tabItem)
                {
                    tab = tabItem.DataContext as WorkflowTab;
                    if (tab != null)
                    {
                        Debug.WriteLine($"[SequenceViewModel] 从 TabItem.DataContext 中找到 WorkflowTab: {tab.Name}");
                    }
                }
            }

            if (tab == null)
            {
                Debug.WriteLine("[SequenceViewModel] 关闭标签页失败: 无法从参数中提取 WorkflowTab");
                return;
            }

            // 如果正在重命名或编辑名尚未提交，禁止关闭，避免回车/失焦导致误删
            // 检查 IsInEditMode 或 EditingName 是否与当前名称不同（表示正在编辑）
            if (tab.IsInEditMode || (!string.IsNullOrEmpty(tab.EditingName) && tab.EditingName != tab.Name))
            {
                Debug.WriteLine($"[SequenceViewModel] 标签页正在重命名，忽略关闭: {tab.Name}, IsInEditMode={tab.IsInEditMode}, EditingName={tab.EditingName}");
                return;
            }

            Debug.WriteLine($"[SequenceViewModel] 准备关闭标签页: {tab.Name} (Type: {tab.Type}, Id: {tab.Id})");

            // 检查标签页是否已经在关闭过程中（防止重复关闭）
            if (_closingTabs.Contains(tab.Id))
            {
                Debug.WriteLine($"[SequenceViewModel] 标签页 {tab.Name} (Id: {tab.Id}) 正在关闭中，跳过重复关闭");
                return;
            }

            // 检查标签页是否已经被移除（防止重复关闭）
            if (!WorkflowTabs.Contains(tab))
            {
                Debug.WriteLine($"[SequenceViewModel] 标签页 {tab.Name} 已经被移除，跳过重复关闭");
                return;
            }

            // 显示确认对话框
            var confirmMessage = $"确定要删除子流程 '{tab.Name}' 吗？";
            if (!MessageBoxHelper.Confirm(confirmMessage, "删除子流程"))
            {
                Debug.WriteLine($"[SequenceViewModel] 用户取消删除子流程: {tab.Name}");
                return;
            }

            // 标记为正在关闭
            _closingTabs.Add(tab.Id);
            Debug.WriteLine($"[SequenceViewModel] 标记标签页 {tab.Name} (Id: {tab.Id}) 为正在关闭");

            try
            {
                // 如果标签页已修改，提示保存
                if (tab.IsModified)
                {
                    // TODO: 显示保存确认对话框
                    Debug.WriteLine($"[SequenceViewModel] 流程 {tab.Name} 已修改，需要保存");
                }

                // 如果是当前活动标签页，优先切换到"前一个"标签页（更符合用户直觉）
                WorkflowTab targetTab = null;
                if (tab == CurrentTab)
                {
                    // 优先使用子流程标签集合的顺序（这是界面 TabItem 的顺序）
                    var subIndex = SubWorkflowTabs?.IndexOf(tab) ?? -1;
                    if (subIndex >= 0)
                    {
                        // 先选前一个
                        if (subIndex - 1 >= 0)
                        {
                            targetTab = SubWorkflowTabs[subIndex - 1];
                        }
                        // 如果没有前一个（说明自己是第一个），再选后一个
                        else if (subIndex + 1 < SubWorkflowTabs.Count)
                        {
                            targetTab = SubWorkflowTabs[subIndex + 1];
                        }
                    }

                    // 兜底：如果不在 SubWorkflowTabs（例如异常情况），再从 WorkflowTabs 里按索引找相邻
                    if (targetTab == null && WorkflowTabs != null)
                    {
                        var allIndex = WorkflowTabs.IndexOf(tab);
                        if (allIndex >= 0)
                        {
                            if (allIndex - 1 >= 0) targetTab = WorkflowTabs[allIndex - 1];
                            else if (allIndex + 1 < WorkflowTabs.Count) targetTab = WorkflowTabs[allIndex + 1];
                        }
                    }

                    if (targetTab != null && !ReferenceEquals(targetTab, tab))
                    {
                        SwitchWorkflow(targetTab);
                    }
                    else
                    {
                        // 没有可切换的目标（最后一个被关掉）
                        CurrentTab = null;
                    }
                }

                // 使用命令模式执行删除操作（支持撤销/重做）
                var removeCommand = new RemoveWorkflowCommand(
                    tab,
                    WorkflowTabs,
                    SubWorkflowTabs,
                    SubWorkflows,
                    MasterWorkflow,
                    MasterWorkflowTab,
                    IsMasterWorkflowViewVisible,
                    onWorkflowRemoved: (removedTab) =>
                    {
                        // 流程删除后的回调：如果被删除的是当前标签页，切换到其他标签页
                        if (removedTab == CurrentTab)
                        {
                            // 切换到第一个可用的子流程标签页
                            var otherTab = SubWorkflowTabs.FirstOrDefault(t => t != removedTab);
                            if (otherTab != null)
                            {
                                SwitchWorkflow(otherTab);
                            }
                            else
                            {
                                // 没有其他标签页，设置为 null
                                CurrentTab = null;
                            }
                        }
                        
                        // 触发 UI 更新
                        OnPropertyChanged(nameof(MasterWorkflowTab));
                        if (IsMasterWorkflowViewVisible)
                        {
                            System.Windows.Application.Current?.Dispatcher.BeginInvoke(new System.Action(() =>
                            {
                                OnPropertyChanged(nameof(MasterWorkflowTab));
                                Debug.WriteLine($"[SequenceViewModel] 已触发 MasterWorkflowTab 属性变更通知（UI 刷新）");
                            }), System.Windows.Threading.DispatcherPriority.Loaded);
                        }
                        SaveMasterWorkflowState();
                        Debug.WriteLine($"[SequenceViewModel] 关闭流程: {removedTab.Name} (Id: {removedTab.Id})");
                    },
                    onWorkflowAdded: (restoredTab) =>
                    {
                        // 流程恢复后的回调：切换到恢复的标签页并刷新主流程节点
                        if (restoredTab != null)
                        {
                            SwitchWorkflow(restoredTab);
                            Debug.WriteLine($"[SequenceViewModel] 恢复流程并切换到: {restoredTab.Name} (Id: {restoredTab.Id})");
                        }
                        if (IsMasterWorkflowViewVisible)
                        {
                            RefreshMasterWorkflowNodes();
                        }
                    },
                    refreshMasterWorkflowNodes: () =>
                    {
                        // 刷新主流程节点的回调
                        if (IsMasterWorkflowViewVisible)
                        {
                            RefreshMasterWorkflowNodes();
                        }
                    }
                );

                // 通过命令管理器执行命令
                _commandManager.Execute(removeCommand);
            }
            finally
            {
                // 从正在关闭的集合中移除
                _closingTabs.Remove(tab.Id);
                Debug.WriteLine($"[SequenceViewModel] 从正在关闭集合中移除标签页 {tab.Name} (Id: {tab.Id})");
            }
        }

        /// <summary>
        /// 在主流程中添加子流程节点命令
        /// 使用命令模式实现，支持撤销/重做
        /// 如果子流程不存在，会自动创建子流程（统一处理添加流程和添加节点）
        /// </summary>
        [RelayCommand]
        private void AddSubWorkflowNode(string subWorkflowId)
        {
            if (CurrentTab == null || CurrentTab.Type != WorkflowType.Master)
            {
                Debug.WriteLine("[SequenceViewModel] 只能在主流程中添加子流程节点");
                return;
            }

            // 检查是否已存在引用
            var existingRef = MasterWorkflow.GetSubWorkflowReference(subWorkflowId);
            if (existingRef != null)
            {
                var existingName = SubWorkflows.TryGetValue(subWorkflowId, out var existing) 
                    ? existing.Name ?? "未命名流程" 
                    : "未命名流程";
                Debug.WriteLine($"[SequenceViewModel] 子流程 {existingName} 已在主流程中");
                return;
            }

            // 获取子流程名称（如果不存在，使用ID作为名称）
            string subWorkflowName;
            if (SubWorkflows.TryGetValue(subWorkflowId, out var subWorkflow))
            {
                subWorkflowName = subWorkflow.Name ?? "未命名流程";
            }
            else
            {
                // 子流程不存在，使用下一个可用的流程名
                subWorkflowName = GetNextAvailableWorkflowName();
            }

            // 计算节点位置
            var position = new Point2D(100 + MasterWorkflow.SubWorkflowReferences.Count * 250, 100);

            // 创建添加子流程节点命令（统一处理创建子流程和添加节点）
            var addCommand = new AddSubWorkflowNodeCommand(
                subWorkflowId,
                subWorkflowName,
                MasterWorkflow,
                CurrentTab.Nodes,
                WorkflowTabs,
                SubWorkflowTabs,
                SubWorkflows,
                position,
                onWorkflowCreated: (tab) =>
                {
                    // 子流程创建后的回调：设置当前标签页并同步到画布
                    if (CurrentTab != null) CurrentTab.IsActive = false;
                    tab.IsActive = true;
                    tab.IsModified = true;
                    CurrentTab = tab;
                    IsMasterWorkflow = false;
                    SyncTabToCanvas(tab);

                    // 如果主流程编辑界面已打开，刷新主流程节点
                    if (IsMasterWorkflowViewVisible)
                    {
                        RefreshMasterWorkflowNodes();
                    }

                    Debug.WriteLine($"[SequenceViewModel] 创建新子流程: {subWorkflowName}");
                },
                onNodeAdded: () =>
                {
                    // 节点添加后的回调：标记为已修改
                    CurrentTab.MarkAsModified();
                    Debug.WriteLine($"[SequenceViewModel] 在主流程中添加子流程节点: {subWorkflowName}");
                },
                onNodeRemoved: () =>
                {
                    // 节点移除后的回调：标记为已修改并刷新主流程节点
                    CurrentTab.MarkAsModified();
                    if (IsMasterWorkflowViewVisible)
                    {
                        RefreshMasterWorkflowNodes();
                    }
                    Debug.WriteLine($"[SequenceViewModel] 从主流程中移除子流程节点: {subWorkflowName}");
                },
                onWorkflowRemoved: (removedTab) =>
                {
                    // 子流程移除后的回调：如果被删除的是当前标签页，切换到其他标签页
                    if (removedTab == CurrentTab)
                    {
                        // 切换到第一个可用的子流程标签页
                        var otherTab = SubWorkflowTabs.FirstOrDefault(t => t != removedTab);
                        if (otherTab != null)
                        {
                            SwitchWorkflow(otherTab);
                        }
                        else
                        {
                            // 没有其他标签页，设置为 null
                            CurrentTab = null;
                        }
                    }
                    
                    // 刷新主流程节点
                    if (IsMasterWorkflowViewVisible)
                    {
                        RefreshMasterWorkflowNodes();
                    }
                }
            );

            // 通过命令管理器执行命令
            _commandManager.Execute(addCommand);
        }

        /// <summary>
        /// 连接两个流程节点命令（使用参数对象）
        /// </summary>
        [RelayCommand]
        private void ConnectWorkflows(WorkflowConnectionParams parameters)
        {
            if (parameters == null)
                return;

            ConnectWorkflowsInternal(parameters.SourceWorkflowId, parameters.TargetWorkflowId);
        }

        /// <summary>
        /// 连接两个流程节点的内部实现
        /// </summary>
        private void ConnectWorkflowsInternal(string sourceWorkflowId, string targetWorkflowId)
        {
            if (CurrentTab == null || CurrentTab.Type != WorkflowType.Master)
            {
                Debug.WriteLine("[SequenceViewModel] 只能在主流程中连接流程");
                return;
            }

            // 查找对应的节点ID
            var sourceNode = CurrentTab.Nodes.OfType<WorkflowReferenceNode>()
                .FirstOrDefault(n => n.SubWorkflowId == sourceWorkflowId);
            var targetNode = CurrentTab.Nodes.OfType<WorkflowReferenceNode>()
                .FirstOrDefault(n => n.SubWorkflowId == targetWorkflowId);

            if (sourceNode == null || targetNode == null)
            {
                Debug.WriteLine($"[SequenceViewModel] 无法找到对应的节点: 源={sourceWorkflowId}, 目标={targetWorkflowId}");
                return;
            }

            // 创建 Edge 连线
            var edge = new Edge
            {
                SourceNodeId = sourceNode.Id,
                TargetNodeId = targetNode.Id,
                SourcePortId = $"{sourceNode.Id}:Bottom", // 默认从底部端口连接
                TargetPortId = $"{targetNode.Id}:Top" // 默认连接到顶部端口
            };

            // 添加到当前标签页和主流程数据
            try
            {
                CurrentTab.Edges.Add(edge);

                // 同时保存到主流程数据（使用引用ID）
                var sourceRef = MasterWorkflow.GetSubWorkflowReference(sourceWorkflowId);
                var targetRef = MasterWorkflow.GetSubWorkflowReference(targetWorkflowId);

                if (sourceRef != null && targetRef != null)
                {
                    var savedEdge = edge.Clone();
                    savedEdge.SourceNodeId = sourceRef.Id; // 使用引用ID
                    savedEdge.TargetNodeId = targetRef.Id; // 使用引用ID
                    MasterWorkflow.AddEdge(savedEdge);
                }

                CurrentTab.MarkAsModified();
                Debug.WriteLine($"[SequenceViewModel] 连接流程: {sourceWorkflowId} -> {targetWorkflowId}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SequenceViewModel] 连接流程失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 打开子流程编辑器命令
        /// </summary>
        [RelayCommand]
        private void OpenWorkflowEditor(string subWorkflowId)
        {
            if (!SubWorkflows.TryGetValue(subWorkflowId, out var subWorkflow))
            {
                Debug.WriteLine($"[SequenceViewModel] 子流程 {subWorkflowId} 不存在");
                return;
            }

            // 查找或创建子流程标签页
            var tab = WorkflowTabs.FirstOrDefault(t =>
                t.Type == WorkflowType.Sub &&
                t.GetSubWorkflow()?.Id == subWorkflowId);

            if (tab == null)
            {
                // 创建新的子流程标签页
                // 将 WorkFlowNode 的 Nodes 转换为 ObservableCollection
                var nodes = new ObservableCollection<Node>(subWorkflow.Nodes ?? new List<Node>());
                var edges = new ObservableCollection<Edge>();

                // 注意：WorkFlowNode 使用 Connections，画布使用 Edges
                // 这里暂时只同步 Nodes，Edges 需要根据实际需求转换

                tab = new WorkflowTab
                {
                    Name = subWorkflow.Name ?? "未命名流程",
                    Type = WorkflowType.Sub,
                    WorkflowData = subWorkflow,
                    Nodes = nodes,
                    Edges = edges
                };
                WorkflowTabs.Add(tab);
                // 添加到子流程标签页集合（用于标签栏显示）
                SubWorkflowTabs.Add(tab);
            }

            // 切换到子流程标签页
            SwitchWorkflow(tab);
        }

        #endregion

        #region 流程管理辅助方法

        /// <summary>
        /// 同步标签页数据到画布（仅用于子流程标签页）
        /// </summary>
        private void SyncTabToCanvas(WorkflowTab tab)
        {
            if (tab == null || tab.Type == WorkflowType.Master)
                return;

            // 注意：FlowEditor 通过 XAML 绑定直接使用 WorkflowTab 的 Nodes 和 Edges 属性
            // 因此这里的同步仅用于保持 ViewModel 属性的一致性（用于某些不直接绑定到 Tab 的场景）
            // 由于 FlowEditor 直接绑定到 WorkflowTab，切换标签页不会触发连线重绘
            // 只有在引用发生变化时才更新，避免不必要的通知
            if (CanvasItemsSource != tab.Nodes)
            {
            CanvasItemsSource = tab.Nodes;
            }

            if (EdgeItemsSource != tab.Edges)
            {
            EdgeItemsSource = tab.Edges;
            }

            Debug.WriteLine($"[SequenceViewModel] 同步标签页到画布: {tab.Name}, 节点数: {tab.Nodes.Count}, 连线数: {tab.Edges.Count}");
        }

        /// <summary>
        /// 初始化主流程编辑界面（订阅节点集合变化事件）
        /// </summary>
        private void InitializeMasterWorkflowView()
        {
            if (MasterWorkflowTab == null)
                return;

            // 订阅主流程节点集合变化事件（用于检测节点删除）
            if (MasterWorkflowTab.Nodes is INotifyCollectionChanged notifyCollection)
            {
                // 先取消之前的订阅（如果存在）
                notifyCollection.CollectionChanged -= OnMasterWorkflowNodesCollectionChanged;
                // 订阅新的集合变化事件
                notifyCollection.CollectionChanged += OnMasterWorkflowNodesCollectionChanged;
            }
        }

        /// <summary>
        /// 主流程节点集合变化事件处理（检测节点添加和删除）
        /// </summary>
        private void OnMasterWorkflowNodesCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            Debug.WriteLine($"[OnMasterWorkflowNodesCollectionChanged] 集合变化事件触发，操作类型: {e.Action}");

            // 处理删除操作
            if (e.Action == NotifyCollectionChangedAction.Remove)
            {
                Debug.WriteLine($"[OnMasterWorkflowNodesCollectionChanged] 检测到删除操作，删除项数: {e.OldItems?.Count ?? 0}");
                // 检查是否有 WorkflowReferenceNode 被删除
                foreach (var removedItem in e.OldItems)
                {
                    if (removedItem is WorkflowReferenceNode workflowNode)
                    {
                        // 删除对应的子流程标签页
                        DeleteSubWorkflowTab(workflowNode.SubWorkflowId);
                        Debug.WriteLine($"[SequenceViewModel] 检测到流程引用节点被删除，删除对应的子流程标签页: {workflowNode.SubWorkflowName}");
                    }
                }
            }
            // 处理添加操作（复制粘贴子流程节点时）
            else if (e.Action == NotifyCollectionChangedAction.Add)
            {
                Debug.WriteLine($"[OnMasterWorkflowNodesCollectionChanged] 检测到添加操作，新增项数: {e.NewItems?.Count ?? 0}");
                // 检查是否有 WorkflowReferenceNode 被添加
                foreach (var newItem in e.NewItems)
                {
                    Debug.WriteLine($"[OnMasterWorkflowNodesCollectionChanged] 检查新增项类型: {newItem?.GetType().Name ?? "null"}");
                    if (newItem is WorkflowReferenceNode workflowNode)
                    {
                        Debug.WriteLine($"[OnMasterWorkflowNodesCollectionChanged] 发现 WorkflowReferenceNode: {workflowNode.Name}, NodeId: {workflowNode.Id}, SubWorkflowId: {workflowNode.SubWorkflowId}");
                        
                        // 检查对应的子流程数据是否已存在
                        bool subWorkflowExists = SubWorkflows.ContainsKey(workflowNode.SubWorkflowId);
                        Debug.WriteLine($"[OnMasterWorkflowNodesCollectionChanged] 子流程数据是否存在: {subWorkflowExists}");
                        
                        // 检查主流程中是否已经有其他节点使用相同的 SubWorkflowId（表示这是复制粘贴操作）
                        bool isDuplicate = false;
                        if (subWorkflowExists && MasterWorkflowTab?.Nodes != null)
                        {
                            int sameSubWorkflowCount = MasterWorkflowTab.Nodes
                                .OfType<WorkflowReferenceNode>()
                                .Count(n => n.SubWorkflowId == workflowNode.SubWorkflowId);
                            isDuplicate = sameSubWorkflowCount > 1; // 如果有多个节点使用相同的 SubWorkflowId，说明是复制粘贴
                            Debug.WriteLine($"[OnMasterWorkflowNodesCollectionChanged] 主流程中使用相同 SubWorkflowId 的节点数: {sameSubWorkflowCount}, 是否为复制粘贴: {isDuplicate}");
                        }
                        
                        if (!subWorkflowExists || isDuplicate)
                        {
                            // 子流程数据不存在，或者是复制粘贴操作，需要创建新的子流程
                            Debug.WriteLine($"[OnMasterWorkflowNodesCollectionChanged] 调用 HandlePastedWorkflowReferenceNode");
                            HandlePastedWorkflowReferenceNode(workflowNode);
                        }
                        else
                        {
                            Debug.WriteLine($"[OnMasterWorkflowNodesCollectionChanged] 子流程数据已存在且不是复制粘贴，跳过处理");
                        }
                    }
                }
            }
            else
            {
                Debug.WriteLine($"[OnMasterWorkflowNodesCollectionChanged] 其他操作类型: {e.Action}");
            }
        }

        /// <summary>
        /// 处理粘贴的 WorkflowReferenceNode（复制对应的子流程数据并创建新标签页）
        /// </summary>
        private void HandlePastedWorkflowReferenceNode(WorkflowReferenceNode workflowNode)
        {
            if (workflowNode == null)
                return;

            Debug.WriteLine($"[SequenceViewModel] 检测到粘贴的流程引用节点: {workflowNode.Name}, SubWorkflowId: {workflowNode.SubWorkflowId}");

            // 查找对应的源子流程数据
            // 注意：粘贴的节点的 SubWorkflowId 可能指向已存在的子流程，也可能指向不存在的子流程
            WorkFlowNode sourceSubWorkflow = null;

            // 先尝试从当前的 SubWorkflows 字典中查找
            if (!string.IsNullOrEmpty(workflowNode.SubWorkflowId) && SubWorkflows.ContainsKey(workflowNode.SubWorkflowId))
            {
                sourceSubWorkflow = SubWorkflows[workflowNode.SubWorkflowId];
            }

            // 如果找不到，说明这是一个孤立的节点（可能来自其他项目的复制粘贴），创建一个空的子流程
            if (sourceSubWorkflow == null)
            {
                Debug.WriteLine($"[SequenceViewModel] 未找到源子流程数据，创建新的空子流程");
                
                // 创建一个新的空子流程
                var newSubWorkflow = new WorkFlowNode
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = workflowNode.SubWorkflowName ?? "新子流程",
                    Nodes = new List<Node>(),
                    Connections = new List<Connection>()
                };

                // 更新节点的 SubWorkflowId
                workflowNode.SubWorkflowId = newSubWorkflow.Id;
                workflowNode.SubWorkflowName = newSubWorkflow.Name;

                // 添加到字典
                SubWorkflows[newSubWorkflow.Id] = newSubWorkflow;

                // 创建标签页
                CreateSubWorkflowTab(newSubWorkflow);
                return;
            }

            // 如果找到了源子流程，复制它
            Debug.WriteLine($"[SequenceViewModel] 找到源子流程数据，开始复制: {sourceSubWorkflow.Name}");

            try
            {
                // 克隆子流程数据
                var clonedNode = sourceSubWorkflow.Clone();
                var duplicatedSubWorkflow = clonedNode as WorkFlowNode;
                
                if (duplicatedSubWorkflow == null)
                {
                    Debug.WriteLine($"[SequenceViewModel] 克隆子流程失败：类型转换错误");
                    return;
                }

                // 重建关系
                duplicatedSubWorkflow.RebuildRelationships();

                // 生成新的子流程名称
                var newName = GenerateUniqueSubWorkflowName(sourceSubWorkflow.Name);
                duplicatedSubWorkflow.Name = newName;

                // 更新节点的引用
                workflowNode.SubWorkflowId = duplicatedSubWorkflow.Id;
                workflowNode.SubWorkflowName = newName;
                workflowNode.Name = newName; // 同步节点名称

                // 添加到字典
                SubWorkflows[duplicatedSubWorkflow.Id] = duplicatedSubWorkflow;

                // 创建标签页
                CreateSubWorkflowTab(duplicatedSubWorkflow);

                Debug.WriteLine($"[SequenceViewModel] 成功复制子流程: {newName}, 新ID: {duplicatedSubWorkflow.Id}");
                StatusMessage = $"已复制子流程: {newName}";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SequenceViewModel] 复制子流程失败: {ex.Message}");
                MessageBoxHelper.ShowError($"复制子流程失败: {ex.Message}", "错误");
            }
        }

        /// <summary>
        /// 创建子流程标签页
        /// </summary>
        private void CreateSubWorkflowTab(WorkFlowNode subWorkflow)
        {
            // 检查是否已存在
            var existingTab = WorkflowTabs.FirstOrDefault(t =>
                t.Type == WorkflowType.Sub &&
                t.WorkflowData is WorkFlowNode workflow &&
                workflow.Id == subWorkflow.Id);

            if (existingTab != null)
            {
                Debug.WriteLine($"[SequenceViewModel] 子流程标签页已存在: {subWorkflow.Name}");
                return;
            }

            // 创建新标签页
            var newTab = new WorkflowTab
            {
                Name = subWorkflow.Name,
                Type = WorkflowType.Sub,
                IsActive = false,
                WorkflowData = subWorkflow
            };

            // 同步节点到标签页
            foreach (var node in subWorkflow.Nodes)
            {
                newTab.Nodes.Add(node);
            }

            // 同步连线到标签页
            foreach (var connection in subWorkflow.Connections)
            {
                var edge = new Edge
                {
                    SourceNodeId = connection.SourceNodeId,
                    SourcePortId = connection.SourcePortId,
                    TargetNodeId = connection.TargetNodeId,
                    TargetPortId = connection.TargetPortId
                };
                newTab.Edges.Add(edge);
            }

            // 添加到集合
            WorkflowTabs.Add(newTab);
            SubWorkflowTabs.Add(newTab);

            Debug.WriteLine($"[SequenceViewModel] 创建子流程标签页: {subWorkflow.Name}");
        }

        /// <summary>
        /// 生成唯一的子流程名称
        /// </summary>
        private string GenerateUniqueSubWorkflowName(string baseName)
        {
            // 移除基础名称中已有的 " - 副本" 或 " - 副本(N)" 后缀
            var cleanBaseName = System.Text.RegularExpressions.Regex.Replace(baseName, @" - 副本(\(\d+\))?$", "");

            // 查找所有以 cleanBaseName 开头的子流程
            var existingNames = SubWorkflows.Values
                .Select(w => w.Name)
                .Where(n => n.StartsWith(cleanBaseName))
                .ToHashSet();

            // 如果基础名称本身就不存在，直接使用 " - 副本"
            if (!existingNames.Contains($"{cleanBaseName} - 副本"))
            {
                return $"{cleanBaseName} - 副本";
            }

            // 否则，查找下一个可用的序号
            int counter = 2;
            string candidateName;
            do
            {
                candidateName = $"{cleanBaseName} - 副本({counter})";
                counter++;
            }
            while (existingNames.Contains(candidateName));

            return candidateName;
        }

        /// <summary>
        /// 删除子流程标签页（当主流程中的流程引用节点被删除时调用）
        /// </summary>
        private void DeleteSubWorkflowTab(string subWorkflowId)
        {
            if (string.IsNullOrEmpty(subWorkflowId))
                return;

            // 查找对应的子流程标签页
            var subWorkflowTab = WorkflowTabs.FirstOrDefault(t =>
                t.Type == WorkflowType.Sub &&
                t.WorkflowData is WorkFlowNode workflow &&
                workflow.Id == subWorkflowId);

            if (subWorkflowTab != null)
            {
                // 如果当前正在编辑这个子流程，需要先切换到其他标签页
                if (CurrentTab == subWorkflowTab)
                {
                    // 切换到第一个子流程标签页（如果存在）
                    var otherTab = SubWorkflowTabs.FirstOrDefault(t => t != subWorkflowTab);
                    if (otherTab != null)
                    {
                        SwitchWorkflow(otherTab);
                    }
                    else
                    {
                        CurrentTab = null;
                    }
                }

                // 从子流程字典中删除
                if (SubWorkflows.ContainsKey(subWorkflowId))
                {
                    SubWorkflows.Remove(subWorkflowId);
                }

                // 从主流程的引用中删除
                var masterWorkflow = MasterWorkflow;
                if (masterWorkflow != null)
                {
                    masterWorkflow.RemoveSubWorkflowReference(subWorkflowId);
                }

                // 从标签页集合中删除
                WorkflowTabs.Remove(subWorkflowTab);
                SubWorkflowTabs.Remove(subWorkflowTab);

                Debug.WriteLine($"[SequenceViewModel] 已删除子流程标签页: {subWorkflowTab.Name}");
            }
        }

        /// <summary>
        /// 保存主流程状态（包括节点位置和连线）
        /// </summary>
        private void SaveMasterWorkflowState()
        {
            if (MasterWorkflowTab == null)
            {
                Debug.WriteLine("[SequenceViewModel] 保存主流程状态失败: MasterWorkflowTab 为 null");
                return;
            }

            var masterWorkflow = MasterWorkflowTab.GetMasterWorkflow();
            if (masterWorkflow == null)
            {
                Debug.WriteLine("[SequenceViewModel] 保存主流程状态失败: MasterWorkflow 为 null");
                return;
            }

            // 更新流程引用节点的位置
            foreach (var node in MasterWorkflowTab.Nodes.OfType<WorkflowReferenceNode>())
            {
                var reference = masterWorkflow.GetSubWorkflowReference(node.SubWorkflowId);
                if (reference != null)
                {
                    reference.Position = node.Position;
                    reference.Size = node.Size;
                }
            }

            // 保存连线：直接使用 Edge，因为节点ID已经是引用ID（稳定），无需转换
            // 先清空现有连线，然后从主流程标签页的 Edges 重建
            masterWorkflow.Edges.Clear();

            // 直接保存 Edge，因为节点ID已经是引用ID（稳定）
            foreach (var edge in MasterWorkflowTab.Edges)
            {
                // 直接保存 Edge，因为节点ID已经是引用ID（稳定），连线可以一直存在
                masterWorkflow.Edges.Add(edge);
            }

            Debug.WriteLine($"[SequenceViewModel] 保存主流程状态完成: 节点数={MasterWorkflowTab.Nodes.Count}, 连线数={masterWorkflow.Edges.Count}");
        }

        /// <summary>
        /// 保存当前标签页状态
        /// </summary>
        private void SaveCurrentTabState()
        {
            // 如果主流程编辑界面可见，保存主流程状态
            if (IsMasterWorkflowViewVisible && MasterWorkflowTab != null)
            {
                SaveMasterWorkflowState();
            }

            if (CurrentTab == null)
                return;

            if (CurrentTab.Type == WorkflowType.Master)
            {
                // 这种情况不应该发生，因为主流程不再在 TabControl 中
                Debug.WriteLine("[SequenceViewModel] 警告：CurrentTab 是主流程，但主流程应该在独立界面中");
                return;
            }
            else
            {
                // 子流程：同步节点和连线到子流程数据（WorkFlowNode）
                var subWorkflow = CurrentTab.GetSubWorkflow();
                if (subWorkflow != null)
                {
                    // 同步节点
                    subWorkflow.Nodes = CurrentTab.Nodes.ToList();

                    // 将 Edges 转换为 Connections 并同步到 WorkFlowNode
                    // 注意：WorkFlowNode 使用 Connections，画布使用 Edges
                    if (subWorkflow.Connections == null)
                    {
                        subWorkflow.Connections = new List<Connection>();
                    }
                    else
                    {
                        subWorkflow.Connections.Clear();
                    }

                    // 从 Edges 转换为 Connections
                    if (CurrentTab.Edges != null && CurrentTab.Edges.Count > 0)
                    {
                        Debug.WriteLine($"[SequenceViewModel] 保存当前标签页状态: 将 {CurrentTab.Edges.Count} 个 Edges 转换为 Connections");
                        foreach (var edge in CurrentTab.Edges)
                        {
                            if (edge == null)
                                continue;

                            var connection = new Connection
                            {
                                Id = edge.Id,
                                SourceNodeId = edge.SourceNodeId,
                                TargetNodeId = edge.TargetNodeId,
                                SourcePortId = edge.SourcePortId,
                                TargetPortId = edge.TargetPortId,
                                Type = ConnectionType.Flow, // 默认类型，可以根据需要调整
                                Label = null,
                                Metadata = new Dictionary<string, object>()
                            };
                            subWorkflow.Connections.Add(connection);
                        }
                        Debug.WriteLine($"[SequenceViewModel] 已保存 {subWorkflow.Connections.Count} 个 Connections 到子流程 '{subWorkflow.Name}'");
                    }
                    else
                    {
                        Debug.WriteLine($"[SequenceViewModel] 当前标签页没有 Edges，清空 Connections");
                    }
                }
            }
        }

        #endregion

        #region 工具栏命令

        /// <summary>
        /// 打开文件命令
        /// </summary>
        [RelayCommand]
        private void OpenFile()
        {
            try
        {
            Debug.WriteLine("[SequenceViewModel] 打开文件命令");

                // 检查是否有未保存的更改
                if (HasUnsavedChanges())
                {
                    var saveResult = MessageBoxHelper.ConfirmSave("当前项目有未保存的更改，是否保存？");
                    if (saveResult == MessageBoxResult.Yes)
                    {
                        if (!SaveFileInternal())
                        {
                            return; // 用户取消保存或保存失败
                        }
                    }
                    else if (saveResult == MessageBoxResult.Cancel)
                    {
                        return; // 用户取消操作
                    }
                }

                // 打开文件对话框
                var openFileDialog = new Microsoft.Win32.OpenFileDialog
                {
                    Title = "打开流程项目",
                    Filter = FILE_FILTER,
                    FilterIndex = 1,
                    InitialDirectory = SolutionsFolder,
                    DefaultExt = FILE_EXTENSION,
                    AddExtension = true
                };

                if (openFileDialog.ShowDialog() != true)
                {
                    Debug.WriteLine("[SequenceViewModel] 用户取消打开文件");
                    return;
                }

                var filePath = openFileDialog.FileName;

                // 加载文件
                var loadResult = _workflowSerializer.LoadFromFile(filePath);
                if (!loadResult.Success)
                {
                    MessageBoxHelper.ShowError($"加载文件失败: {loadResult.ErrorMessage}", "打开失败");
                    Debug.WriteLine($"[SequenceViewModel] 加载文件失败: {loadResult.ErrorMessage}");
                    return;
                }

                // 导入数据到 ViewModel
                LoadMultiWorkflowData(loadResult.Data);

                // 更新当前文件路径
                CurrentFilePath = filePath;

                // 清除所有修改标记
                ClearAllModifiedFlags();

                MessageBoxHelper.ShowSuccess($"项目已成功加载: {Path.GetFileName(filePath)}", "打开成功");
                Debug.WriteLine($"[SequenceViewModel] 项目已成功加载: {filePath}");
            }
            catch (Exception ex)
            {
                MessageBoxHelper.ShowError($"打开文件时发生错误: {ex.Message}", "错误");
                Debug.WriteLine($"[SequenceViewModel] 打开文件异常: {ex.Message}");
            }
        }

        /// <summary>
        /// 保存文件命令
        /// </summary>
        [RelayCommand]
        private void SaveFile()
        {
            try
        {
            Debug.WriteLine("[SequenceViewModel] 保存文件命令");

                if (string.IsNullOrWhiteSpace(CurrentFilePath))
                {
                    // 如果没有当前文件路径，执行另存为
                    SaveFileAs();
                    return;
                }

                // 保存到当前文件
                if (SaveFileInternal(CurrentFilePath))
                {
                    MessageBoxHelper.ShowSuccess($"项目已保存: {Path.GetFileName(CurrentFilePath)}", "保存成功");
                    Debug.WriteLine($"[SequenceViewModel] 项目已保存: {CurrentFilePath}");
                }
            }
            catch (Exception ex)
            {
                MessageBoxHelper.ShowError($"保存文件时发生错误: {ex.Message}", "错误");
                Debug.WriteLine($"[SequenceViewModel] 保存文件异常: {ex.Message}");
            }
        }

        /// <summary>
        /// 另存为命令
        /// </summary>
        [RelayCommand]
        private void SaveFileAs()
        {
            try
        {
            Debug.WriteLine("[SequenceViewModel] 另存为命令");

                // 提前获取文件夹路径，确保文件夹存在
                var initialDir = SolutionsFolder;
                Debug.WriteLine($"[SequenceViewModel] Solutions 文件夹路径: {initialDir}");

                // 打开文件保存对话框
                var saveFileDialog = new Microsoft.Win32.SaveFileDialog
                {
                    Title = "保存流程项目",
                    Filter = FILE_FILTER,
                    FilterIndex = 1,
                    DefaultExt = FILE_EXTENSION,
                    AddExtension = true,
                    InitialDirectory = initialDir,
                    FileName = string.IsNullOrWhiteSpace(CurrentFilePath) 
                        ? $"项目_{DateTime.Now:yyyyMMdd_HHmmss}" 
                        : Path.GetFileNameWithoutExtension(CurrentFilePath)
                };

                if (saveFileDialog.ShowDialog() != true)
                {
                    Debug.WriteLine("[SequenceViewModel] 用户取消另存为");
                    return;
                }

                var filePath = saveFileDialog.FileName;

                // 保存文件
                if (SaveFileInternal(filePath))
                {
                    // 更新当前文件路径
                    CurrentFilePath = filePath;
                    MessageBoxHelper.ShowSuccess($"项目已保存: {Path.GetFileName(filePath)}", "保存成功");
                    Debug.WriteLine($"[SequenceViewModel] 项目已另存为: {filePath}");
                }
            }
            catch (Exception ex)
            {
                MessageBoxHelper.ShowError($"另存为文件时发生错误: {ex.Message}", "错误");
                Debug.WriteLine($"[SequenceViewModel] 另存为文件异常: {ex.Message}");
            }
        }

        /// <summary>
        /// 内部保存方法
        /// </summary>
        private bool SaveFileInternal(string filePath = null)
        {
            try
            {
                filePath = filePath ?? CurrentFilePath;
                if (string.IsNullOrWhiteSpace(filePath))
                {
                    Debug.WriteLine("[SequenceViewModel] 保存失败: 文件路径为空");
                    return false;
                }

                // 导出当前数据
                var data = ExportMultiWorkflowData();
                if (data == null)
                {
                    MessageBoxHelper.ShowError("导出数据失败，无法保存", "保存失败");
                    return false;
                }

                // 保存到文件
                var result = _workflowSerializer.SaveToFile(data, filePath);
                if (!result.Success)
                {
                    MessageBoxHelper.ShowError($"保存文件失败: {result.ErrorMessage}", "保存失败");
                    return false;
                }

                // 清除所有修改标记
                ClearAllModifiedFlags();

                return true;
            }
            catch (Exception ex)
            {
                MessageBoxHelper.ShowError($"保存文件时发生错误: {ex.Message}", "错误");
                Debug.WriteLine($"[SequenceViewModel] 保存文件异常: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 导出当前多流程数据
        /// </summary>
        private MultiWorkflowData ExportMultiWorkflowData()
        {
            try
            {
                // 保存当前标签页状态
                SaveCurrentTabState();

                // 保存所有子流程标签页的状态（确保所有标签页的 Edges 都转换为 Connections）
                foreach (var tab in SubWorkflowTabs)
                {
                    if (tab == null || tab.Type != WorkflowType.Sub)
                        continue;

                    var subWorkflow = tab.GetSubWorkflow();
                    if (subWorkflow != null)
                    {
                        // 同步节点
                        subWorkflow.Nodes = tab.Nodes?.ToList() ?? new List<Node>();

                        // 将 Edges 转换为 Connections
                        if (subWorkflow.Connections == null)
                        {
                            subWorkflow.Connections = new List<Connection>();
                        }
                        else
                        {
                            subWorkflow.Connections.Clear();
                        }

                        if (tab.Edges != null && tab.Edges.Count > 0)
                        {
                            foreach (var edge in tab.Edges)
                            {
                                if (edge == null)
                                    continue;

                                var connection = new Connection
                                {
                                    Id = edge.Id,
                                    SourceNodeId = edge.SourceNodeId,
                                    TargetNodeId = edge.TargetNodeId,
                                    SourcePortId = edge.SourcePortId,
                                    TargetPortId = edge.TargetPortId,
                                    Type = ConnectionType.Flow, // 默认类型
                                    Label = null,
                                    Metadata = new Dictionary<string, object>()
                                };
                                subWorkflow.Connections.Add(connection);
                            }
                            Debug.WriteLine($"[SequenceViewModel] 导出时保存子流程 '{subWorkflow.Name}': {subWorkflow.Connections.Count} 个 Connections");
                        }
                    }
                }

                // 如果主流程编辑界面可见，保存主流程状态
                if (IsMasterWorkflowViewVisible)
                {
                    SaveMasterWorkflowState();
                }

                // 创建多流程数据对象
                var data = new MultiWorkflowData
                {
                    ProjectName = MasterWorkflow?.Name ?? "未命名项目",
                    ProjectDescription = MasterWorkflow?.Description ?? "",
                    MasterWorkflow = MasterWorkflow,
                    SubWorkflows = SubWorkflows,
                    GlobalVariables = GlobalVariables
                };

                return data;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SequenceViewModel] 导出多流程数据失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 从多流程数据加载到 ViewModel
        /// </summary>
        private void LoadMultiWorkflowData(MultiWorkflowData data)
        {
            try
            {
                if (data == null)
                {
                    Debug.WriteLine("[SequenceViewModel] 加载失败: 数据为空");
                    return;
                }

                // 清空当前数据
                ClearAllWorkflows();

                // 加载主流程
                if (data.MasterWorkflow != null)
                {
                    MasterWorkflow = data.MasterWorkflow;
                }

                // 加载子流程
                if (data.SubWorkflows != null)
                {
                    SubWorkflows = new Dictionary<string, WorkFlowNode>(data.SubWorkflows);
                    
                    // 为每个子流程创建标签页
                    foreach (var subWorkflow in data.SubWorkflows.Values)
                    {
                        // 确保关系已重建（虽然序列化器已经调用过，但为了保险再次调用）
                        subWorkflow.RebuildRelationships();

                        var nodes = new ObservableCollection<Node>(subWorkflow.Nodes ?? new List<Node>());
                        var edges = new ObservableCollection<Edge>();

                        // 将 Connections 转换为 Edges（如果需要）
                        if (subWorkflow.Connections != null && subWorkflow.Connections.Count > 0)
                        {
                            Debug.WriteLine($"[SequenceViewModel] 加载子流程 '{subWorkflow.Name}' 包含 {subWorkflow.Connections.Count} 个连接");
                            foreach (var connection in subWorkflow.Connections)
                            {
                                if (connection == null)
                                {
                                    Debug.WriteLine($"[SequenceViewModel] 警告：发现空的连接对象");
                                    continue;
                                }

                                var edge = new Edge
                                {
                                    Id = connection.Id,
                                    SourceNodeId = connection.SourceNodeId,
                                    TargetNodeId = connection.TargetNodeId,
                                    SourcePortId = connection.SourcePortId,
                                    TargetPortId = connection.TargetPortId
                                };
                                edges.Add(edge);
                                Debug.WriteLine($"[SequenceViewModel] 转换连接: {connection.SourceNodeId} -> {connection.TargetNodeId}");
                            }
                        }
                        else
                        {
                            Debug.WriteLine($"[SequenceViewModel] 警告：子流程 '{subWorkflow.Name}' 没有连接（Connections 为 null 或空）");
                        }

                        var tab = new WorkflowTab
                        {
                            Name = subWorkflow.Name ?? "未命名流程",
                            Type = WorkflowType.Sub,
                            WorkflowData = subWorkflow,
                            Nodes = nodes,
                            Edges = edges,
                            IsActive = false // 初始化为非活动状态
                        };

                        WorkflowTabs.Add(tab);
                        SubWorkflowTabs.Add(tab);
                    }
                }

                // 加载全局变量
                if (data.GlobalVariables != null)
                {
                    GlobalVariables = data.GlobalVariables;
                }

                // 重建主流程标签页（如果主流程存在）
                if (MasterWorkflow != null)
                {
                    MasterWorkflowTab = new WorkflowTab
                    {
                        Name = MasterWorkflow.Name ?? "主流程",
                        Type = WorkflowType.Master,
                        IsActive = false,
                        WorkflowData = MasterWorkflow
                    };

                    // 初始化主流程的节点和连线
                    MasterWorkflowTab.Nodes = new ObservableCollection<Node>();
                    MasterWorkflowTab.Edges = new ObservableCollection<Edge>();

                    // 恢复主流程的连线
                    if (MasterWorkflow.Edges != null)
                    {
                        foreach (var edge in MasterWorkflow.Edges)
                        {
                            MasterWorkflowTab.Edges.Add(edge);
                        }
                    }

                    // 如果主流程编辑界面已打开，刷新节点
                    if (IsMasterWorkflowViewVisible)
                    {
                        RefreshMasterWorkflowNodes();
                    }
                }

                // 切换到第一个子流程标签页（如果存在）
                var firstTab = SubWorkflowTabs.FirstOrDefault();
                if (firstTab != null)
                {
                    SwitchWorkflow(firstTab);
                }
                else
                {
                    CurrentTab = null;
                }

                Debug.WriteLine($"[SequenceViewModel] 多流程数据加载完成: 主流程1个, 子流程{data.SubWorkflows?.Count ?? 0}个");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SequenceViewModel] 加载多流程数据失败: {ex.Message}");
                MessageBoxHelper.ShowError($"加载数据失败: {ex.Message}", "加载失败");
            }
        }

        /// <summary>
        /// 检查是否有未保存的更改
        /// </summary>
        private bool HasUnsavedChanges()
        {
            // 检查主流程是否已修改
            if (MasterWorkflow != null && MasterWorkflow.IsModified)
                return true;

            // 检查是否有标签页已修改
            if (WorkflowTabs != null && WorkflowTabs.Any(t => t.IsModified))
                return true;

            return false;
        }

        /// <summary>
        /// 清除所有修改标记
        /// </summary>
        private void ClearAllModifiedFlags()
        {
            if (MasterWorkflow != null)
            {
                MasterWorkflow.IsModified = false;
            }

            if (WorkflowTabs != null)
            {
                foreach (var tab in WorkflowTabs)
                {
                    tab.IsModified = false;
                }
            }
        }

        /// <summary>
        /// 清空所有流程
        /// </summary>
        private void ClearAllWorkflows()
        {
            // 清空标签页
            if (WorkflowTabs != null)
            {
                WorkflowTabs.Clear();
            }

            if (SubWorkflowTabs != null)
            {
                SubWorkflowTabs.Clear();
            }

            // 清空子流程字典
            if (SubWorkflows != null)
            {
                SubWorkflows.Clear();
            }

            // 重置当前标签页
            CurrentTab = null;
            MasterWorkflowTab = null;

            // 重置主流程
            MasterWorkflow = new MasterWorkflow
            {
                Name = "主流程"
            };
        }

        /// <summary>
        /// 撤销命令
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanUndo))]
        private void Undo()
        {
            // 直接使用全局 CommandManager 撤销（所有命令都在同一个栈中，按栈的顺序撤销即可）
            if (_commandManager != null && _commandManager.CanUndo)
            {
                var command = _commandManager.PeekLastCommand();
                Debug.WriteLine($"[SequenceViewModel] 撤销命令 - 描述: {command?.Description}");
                
                // 如果命令有 WorkflowTab，先切换到对应的标签页
                if (command is UndoableCommandBase undoableCommand && undoableCommand.WorkflowTab != null)
                {
                    var targetTab = undoableCommand.WorkflowTab;
                    if (CurrentTab != targetTab && WorkflowTabs.Contains(targetTab))
                    {
                        Debug.WriteLine($"[SequenceViewModel] 撤销前切换到流程: {targetTab.Name}");
                        SwitchWorkflow(targetTab);
                        
                        // 等待界面切换完成后再执行撤销操作
                        ExecuteAfterTabSwitch(targetTab, () =>
                        {
                            Debug.WriteLine($"[SequenceViewModel] 界面切换完成，执行撤销操作");
                            _commandManager.Undo();
                        });
                        return; // 提前返回，等待异步执行
                    }
                }
                
                // 如果已经是当前标签页，立即执行撤销
                _commandManager.Undo();
            }
            else
            {
                Debug.WriteLine($"[SequenceViewModel] 撤销命令 - 没有可撤销的操作");
            }
        }


        /// <summary>
        /// 是否可以撤销
        /// </summary>
        private bool CanUndo()
        {
            // 直接检查全局 CommandManager
            return _commandManager?.CanUndo ?? false;
        }

        /// <summary>
        /// 重做命令
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanRedo))]
        private void Redo()
        {
            // 直接使用全局 CommandManager 重做（所有命令都在同一个栈中，按栈的顺序重做即可）
            if (_commandManager != null && _commandManager.CanRedo)
            {
                var command = _commandManager.PeekRedoCommand();
                Debug.WriteLine($"[SequenceViewModel] 重做命令 - 描述: {command?.Description}");
                
                // 如果命令有 WorkflowTab，先切换到对应的标签页
                if (command is UndoableCommandBase undoableCommand && undoableCommand.WorkflowTab != null)
                {
                    var targetTab = undoableCommand.WorkflowTab;
                    if (CurrentTab != targetTab && WorkflowTabs.Contains(targetTab))
                    {
                        Debug.WriteLine($"[SequenceViewModel] 重做前切换到流程: {targetTab.Name}");
                        SwitchWorkflow(targetTab);
                        
                        // 等待界面切换完成后再执行重做操作
                        ExecuteAfterTabSwitch(targetTab, () =>
                        {
                            Debug.WriteLine($"[SequenceViewModel] 界面切换完成，执行重做操作");
                            _commandManager.Redo();
                        });
                        return; // 提前返回，等待异步执行
                    }
                }
                
                // 如果已经是当前标签页，立即执行重做
                _commandManager.Redo();
            }
            else
            {
                Debug.WriteLine($"[SequenceViewModel] 重做命令 - 没有可重做的操作");
            }
        }


        /// <summary>
        /// 是否可以重做
        /// </summary>
        private bool CanRedo()
        {
            // 直接检查全局 CommandManager
            return _commandManager?.CanRedo ?? false;
        }

        /// <summary>
        /// 在标签页切换完成后执行操作
        /// 使用 Dispatcher 等待界面更新完成，并添加延时确保界面完全渲染
        /// </summary>
        private void ExecuteAfterTabSwitch(WorkflowTab targetTab, System.Action action)
        {
            if (action == null)
                return;

            // 使用 Dispatcher.BeginInvoke 确保在下一个 UI 更新周期执行
            Application.Current.Dispatcher.BeginInvoke(new System.Action(() =>
            {
                // 检查是否已经切换到目标标签页
                if (CurrentTab == targetTab)
                {
                    // 界面切换完成，添加延时确保界面完全渲染后再执行操作
                    var timer = new System.Windows.Threading.DispatcherTimer
                    {
                        Interval = TimeSpan.FromMilliseconds(250) // 延时250毫秒，确保界面完全渲染
                    };
                    timer.Tick += (s, e) =>
                    {
                        timer.Stop();
                        action();
                    };
                    timer.Start();
                }
                else
                {
                    // 如果还没有切换完成，再等待一个周期（最多等待3次）
                    int retryCount = 0;
                    System.Action checkAndExecute = null;
                    checkAndExecute = () =>
                    {
                        retryCount++;
                        if (CurrentTab == targetTab || retryCount >= 3)
                        {
                            // 界面切换完成或达到最大重试次数，添加延时后执行操作
                            var timer = new System.Windows.Threading.DispatcherTimer
                            {
                                Interval = TimeSpan.FromMilliseconds(250) // 延时250毫秒，确保界面完全渲染
                            };
                            timer.Tick += (s, e) =>
                            {
                                timer.Stop();
                                action();
                            };
                            timer.Start();
                        }
                        else
                        {
                            // 继续等待
                            Application.Current.Dispatcher.BeginInvoke(checkAndExecute, System.Windows.Threading.DispatcherPriority.Loaded);
                        }
                    };
                    Application.Current.Dispatcher.BeginInvoke(checkAndExecute, System.Windows.Threading.DispatcherPriority.Loaded);
                }
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        /// <summary>
        /// 切换锁定画布命令
        /// </summary>
        [RelayCommand]
        private void ToggleLock()
        {
            IsLocked = !IsLocked;
            Debug.WriteLine($"[SequenceViewModel] 切换锁定画布: {IsLocked}");
            // TODO: 通过 FlowEditor 设置画布锁定状态
        }

        /// <summary>
        /// 截图命令
        /// </summary>
        [RelayCommand]
        private void CaptureScreenshot()
        {
            Debug.WriteLine("[SequenceViewModel] 截图命令");
            // TODO: 实现截图功能
        }

        /// <summary>
        /// 放大命令
        /// </summary>
        [RelayCommand]
        private void ZoomIn()
        {
            Debug.WriteLine("[SequenceViewModel] 放大命令");
            // TODO: 通过 FlowEditor 放大画布
            ZoomPercentage = Math.Min(200, ZoomPercentage + 10);
        }

        /// <summary>
        /// 缩小命令
        /// </summary>
        [RelayCommand]
        private void ZoomOut()
        {
            Debug.WriteLine("[SequenceViewModel] 缩小命令");
            // TODO: 通过 FlowEditor 缩小画布
            ZoomPercentage = Math.Max(40, ZoomPercentage - 10);
        }

        /// <summary>
        /// 适应画布命令
        /// </summary>
        [RelayCommand]
        private void FitToScreen()
        {
            Debug.WriteLine("[SequenceViewModel] 适应画布命令");
            // TODO: 通过 FlowEditor 适应画布
            ZoomPercentage = 100;
        }

        /// <summary>
        /// 播放命令
        /// </summary>
        [RelayCommand]
        private void Play()
        {
            Debug.WriteLine("[SequenceViewModel] 播放命令");
            StatusMessage = "正在执行流程...";
            // TODO: 实现流程执行逻辑
        }

        /// <summary>
        /// 拖动重排 TabItem 命令
        /// </summary>
        [RelayCommand]
        private void ReorderTabs(object parameter)
        {
            if (parameter == null) return;

            try
            {
                // 使用反射获取动态参数
                var sourceProperty = parameter.GetType().GetProperty("Source");
                var targetProperty = parameter.GetType().GetProperty("Target");

                if (sourceProperty == null || targetProperty == null)
                {
                    Debug.WriteLine("[SequenceViewModel] ReorderTabs: 参数格式不正确");
                    return;
                }

                var sourceTab = sourceProperty.GetValue(parameter) as WorkflowTab;
                var targetTab = targetProperty.GetValue(parameter) as WorkflowTab;

                if (sourceTab == null || targetTab == null)
                {
                    Debug.WriteLine("[SequenceViewModel] ReorderTabs: Source 或 Target 为 null");
                    return;
                }

                var sourceIndex = SubWorkflowTabs.IndexOf(sourceTab);
                var targetIndex = SubWorkflowTabs.IndexOf(targetTab);

                if (sourceIndex >= 0 && targetIndex >= 0 && sourceIndex != targetIndex)
                {
                    SubWorkflowTabs.Move(sourceIndex, targetIndex);
                    Debug.WriteLine($"[SequenceViewModel] 拖动重排: {sourceTab.Name} 从位置 {sourceIndex} 移动到位置 {targetIndex}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SequenceViewModel] ReorderTabs 出错: {ex.Message}");
            }
        }

        /// <summary>
        /// 启动指定子流程命令（从Tab标签按钮调用）
        /// </summary>
        [RelayCommand]
        private void StartSubWorkflow(WorkflowTab tab)
        {
            if (tab == null)
            {
                Debug.WriteLine("[SequenceViewModel] 启动子流程失败: 标签页为 null");
                return;
            }

            if (tab.Type != WorkflowType.Sub)
            {
                Debug.WriteLine("[SequenceViewModel] 启动子流程失败: 标签页不是子流程类型");
                return;
            }

            var subWorkflow = tab.GetSubWorkflow();
            if (subWorkflow == null)
            {
                Debug.WriteLine("[SequenceViewModel] 启动子流程失败: 无法获取子流程数据");
                return;
            }

            Debug.WriteLine($"[SequenceViewModel] 启动子流程: {subWorkflow.Name} (ID: {subWorkflow.Id})");
            StatusMessage = $"正在执行子流程: {subWorkflow.Name}...";
            // TODO: 实现子流程执行逻辑
        }

        /// <summary>
        /// 启动当前子流程命令（保留作为备用）
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanStartCurrentSubWorkflow))]
        private void StartCurrentSubWorkflow()
        {
            if (CurrentTab == null || CurrentTab.Type != WorkflowType.Sub)
            {
                Debug.WriteLine("[SequenceViewModel] 启动子流程失败: 当前标签页不是子流程");
                return;
            }

            // 调用启动指定子流程的方法
            StartSubWorkflow(CurrentTab);
        }

        /// <summary>
        /// 是否可以启动当前子流程
        /// </summary>
        private bool CanStartCurrentSubWorkflow()
        {
            return CurrentTab != null && CurrentTab.Type == WorkflowType.Sub && !IsMasterWorkflowViewVisible;
        }

        /// <summary>
        /// 开始编辑流程名称命令（进入编辑模式）
        /// </summary>
        [RelayCommand]
        private void BeginEditWorkflowName(WorkflowTab tab)
        {
            if (tab == null)
            {
                Debug.WriteLine("[SequenceViewModel] BeginEditWorkflowName: tab 为 null");
                return;
            }

            Debug.WriteLine($"[SequenceViewModel] BeginEditWorkflowName: TabId={tab.Id}, Name={tab.Name}, IsInEditMode={tab.IsInEditMode}");

            // 先设置编辑状态
            tab.IsInEditMode = true;
            tab.EditingName = tab.Name;

            Debug.WriteLine($"[SequenceViewModel] BeginEditWorkflowName 完成: IsInEditMode={tab.IsInEditMode}, EditingName={tab.EditingName}");

            // 强制刷新UI - 触发集合变更通知
            OnPropertyChanged(nameof(SubWorkflowTabs));

            // 延迟强制刷新绑定（拖动后可能需要）
            System.Windows.Application.Current?.Dispatcher.BeginInvoke(new System.Action(() =>
            {
                // 再次触发属性变更，确保UI更新
                OnPropertyChanged(nameof(SubWorkflowTabs));

                // 如果tab在集合中，尝试通过索引刷新
                var index = SubWorkflowTabs.IndexOf(tab);
                if (index >= 0)
                {
                    // 通过临时修改再恢复来触发UI刷新
                    var temp = tab.IsInEditMode;
                    tab.IsInEditMode = false;
                    System.Windows.Application.Current?.Dispatcher.BeginInvoke(new System.Action(() =>
                    {
                        tab.IsInEditMode = temp;
                        Debug.WriteLine($"[SequenceViewModel] 强制刷新绑定完成: IsInEditMode={tab.IsInEditMode}, Index={index}");
                    }), System.Windows.Threading.DispatcherPriority.Loaded);
                }
                else
                {
                    Debug.WriteLine($"[SequenceViewModel] 延迟刷新完成: IsInEditMode={tab.IsInEditMode}, Tab不在集合中");
                }
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        /// <summary>
        /// 确认编辑流程名称命令（保存新名称）
        /// </summary>
        [RelayCommand]
        private void CommitEditWorkflowName(WorkflowTab tab)
        {
            if (tab == null)
            {
                Debug.WriteLine("[SequenceViewModel] CommitEditWorkflowName: tab 为 null");
                return;
            }

            if (!tab.IsInEditMode)
            {
                Debug.WriteLine("[SequenceViewModel] CommitEditWorkflowName: tab 不在编辑模式，忽略（可能已被处理）");
                return;
            }

            // 在 CommitEdit 之前先保存 EditingName，因为 CommitEdit 会设置 IsInEditMode = false
            // 这样可以防止重复调用时丢失新名称
            var newName = tab.EditingName?.Trim();
            var oldName = tab.Name;

            Debug.WriteLine($"[SequenceViewModel] CommitEditWorkflowName: OldName={oldName}, NewName={newName}, IsInEditMode={tab.IsInEditMode}");

            // 检查名称是否为空
            if (string.IsNullOrWhiteSpace(newName))
            {
                Debug.WriteLine("[SequenceViewModel] CommitEditWorkflowName: 新名称为空，取消编辑");
                tab.CancelEdit();
                return;
            }

            // 检查名称是否改变（与当前名称比较，而不是原始名称）
            if (newName == oldName)
            {
                Debug.WriteLine("[SequenceViewModel] CommitEditWorkflowName: 名称未改变，取消编辑");
                tab.CancelEdit();
                return;
            }

            // 执行重命名（在重命名完成后再退出编辑模式，防止重命名期间被误删）
            Debug.WriteLine($"[SequenceViewModel] CommitEditWorkflowName: 准备执行重命名，OldName={oldName}, NewName={newName}");
            
            // 先执行重命名，如果成功再退出编辑模式
            try
            {
                ExecuteRenameWorkflow(tab, newName);
                
                // 重命名成功后，延迟退出编辑模式，确保UI已更新
                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    // 再次检查是否仍在编辑模式（防止重复调用）
                    if (tab.IsInEditMode)
                    {
                        tab.IsInEditMode = false;
                        tab.EditingName = null;
                        Debug.WriteLine($"[SequenceViewModel] CommitEditWorkflowName: 退出编辑模式完成");
                    }
                }), System.Windows.Threading.DispatcherPriority.Loaded);
            }
            catch (Exception ex)
            {
                // 如果重命名失败，保持编辑模式，让用户可以继续编辑
                Debug.WriteLine($"[SequenceViewModel] CommitEditWorkflowName: 重命名失败，保持编辑模式: {ex.Message}");
                // 不退出编辑模式，让用户可以继续编辑或取消
            }
        }

        /// <summary>
        /// 取消编辑流程名称命令（恢复原始名称）
        /// </summary>
        [RelayCommand]
        private void CancelEditWorkflowName(WorkflowTab tab)
        {
            if (tab == null)
            {
                Debug.WriteLine("[SequenceViewModel] CancelEditWorkflowName: tab 为 null");
                return;
            }

            Debug.WriteLine($"[SequenceViewModel] CancelEditWorkflowName: {tab.Name}");
            tab.CancelEdit();
        }

        /// <summary>
        /// 重命名流程命令（支持直接传入新名称，或从参数对象中获取）
        /// </summary>
        [RelayCommand]
        private void RenameWorkflow(object parameter)
        {
            Debug.WriteLine($"[SequenceViewModel] RenameWorkflow: 收到参数, 类型={parameter?.GetType().Name}");

            WorkflowTab tab = null;
            string newName = null;

            // 处理参数：可能是 WorkflowTab，也可能是包含 tab 和 newName 的字典
            if (parameter is WorkflowTab workflowTab)
            {
                tab = workflowTab;
                Debug.WriteLine($"[SequenceViewModel] RenameWorkflow: 参数是 WorkflowTab, TabId={tab.Id}, TabName={tab.Name}");
            }
            else if (parameter is Dictionary<string, object> dict)
            {
                Debug.WriteLine($"[SequenceViewModel] RenameWorkflow: 参数是 Dictionary, 键={string.Join(", ", dict.Keys)}");
                if (dict.TryGetValue("Tab", out var tabObj) && tabObj is WorkflowTab t)
                {
                    tab = t;
                    Debug.WriteLine($"[SequenceViewModel] RenameWorkflow: 从字典获取 Tab, TabId={tab.Id}, TabName={tab.Name}");
                }
                if (dict.TryGetValue("NewName", out var nameObj))
                {
                    newName = nameObj?.ToString();
                    Debug.WriteLine($"[SequenceViewModel] RenameWorkflow: 从字典获取 NewName={newName}");
                }
            }
            else
            {
                Debug.WriteLine($"[SequenceViewModel] RenameWorkflow: 参数类型未知: {parameter?.GetType().FullName}");
            }

            if (tab == null)
            {
                Debug.WriteLine("[SequenceViewModel] RenameWorkflow: 重命名流程失败: 标签页为 null");
                return;
            }

            if (string.IsNullOrWhiteSpace(newName) || newName == tab.Name)
            {
                Debug.WriteLine($"[SequenceViewModel] RenameWorkflow: 用户取消或名称未改变, NewName={newName}, TabName={tab.Name}");
                // 用户取消或名称未改变
                return;
            }

            Debug.WriteLine($"[SequenceViewModel] RenameWorkflow: 准备执行重命名, TabId={tab.Id}, OldName={tab.Name}, NewName={newName}");
            // 执行重命名
            ExecuteRenameWorkflow(tab, newName);
        }

        /// <summary>
        /// 执行重命名流程（内部方法，使用命令模式）
        /// </summary>
        private void ExecuteRenameWorkflow(WorkflowTab tab, string newName)
        {
            if (tab == null || string.IsNullOrWhiteSpace(newName))
            {
                Debug.WriteLine($"[SequenceViewModel] ExecuteRenameWorkflow: tab 或 newName 为空");
                throw new ArgumentException("tab 或 newName 为空");
            }

            Debug.WriteLine($"[SequenceViewModel] ExecuteRenameWorkflow: 开始重命名流程, TabId={tab.Id}, TabName={tab.Name}, NewName={newName}, Type={tab.Type}");

            // 检查名称是否已存在
            var existingTab = WorkflowTabs.FirstOrDefault(t => t != tab && t.Name == newName);
            if (existingTab != null)
            {
                Debug.WriteLine($"[SequenceViewModel] ExecuteRenameWorkflow: 名称已存在，重命名失败");
                MessageBoxHelper.ShowWarning($"流程名称 '{newName}' 已存在，请使用其他名称。", "重命名失败");
                throw new InvalidOperationException($"流程名称 '{newName}' 已存在");
            }

            // 更新流程名称
            var oldName = tab.Name;
            Debug.WriteLine($"[SequenceViewModel] ExecuteRenameWorkflow: 旧名称={oldName}, 新名称={newName}");

            // 使用命令模式执行重命名（支持撤销/重做）
            var command = new RenameWorkflowCommand(
                tab,
                oldName,
                newName,
                WorkflowTabs,
                UpdateMasterWorkflowNodeName,
                (t) => t.MarkAsModified()
            );

            // 使用全局 CommandManager 执行命令（流程级别的操作）
            _commandManager.Execute(command);

            Debug.WriteLine($"[SequenceViewModel] 重命名流程完成: {oldName} -> {newName}");
        }

        /// <summary>
        /// 复制子流程命令（克隆整个子流程，包括所有节点和连线）
        /// </summary>
        [RelayCommand]
        private void DuplicateWorkflow(WorkflowTab tab)
        {
            if (tab == null)
            {
                Debug.WriteLine("[SequenceViewModel] 复制子流程失败: 标签页为 null");
                return;
            }

            if (tab.Type != WorkflowType.Sub)
            {
                MessageBoxHelper.ShowWarning("只能复制子流程，主流程不支持复制", "复制失败");
                return;
            }

            try
            {
                // 先保存当前标签页状态（如果是当前活动标签页）
                if (tab == CurrentTab)
                {
                    SaveCurrentTabState();
                }

                // 创建并执行复制子流程命令（支持撤销/重做）
                var command = new DuplicateWorkflowCommand(
                    sourceWorkflowTab: tab,
                    workflowTabs: WorkflowTabs,
                    subWorkflowTabs: SubWorkflowTabs,
                    subWorkflows: SubWorkflows,
                    onWorkflowAdded: (newTab) =>
                    {
                        // 自动切换到新创建的子流程
                        CurrentTab = newTab;
                        SwitchWorkflow(newTab);
                        StatusMessage = $"已复制子流程: {newTab.Name}";
                        Debug.WriteLine($"[SequenceViewModel] 复制子流程成功: {newTab.Name}");
                    },
                    onWorkflowRemoved: (removedTab) =>
                    {
                        // 撤销时的回调（选择第一个标签页或主流程）
                        if (SubWorkflowTabs.Count > 0)
                        {
                            CurrentTab = SubWorkflowTabs[0];
                            SwitchWorkflow(CurrentTab);
                        }
                        else
                        {
                            CurrentTab = null;
                        }
                        StatusMessage = $"已撤销复制子流程: {removedTab.Name}";
                    });

                _commandManager.Execute(command);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SequenceViewModel] 复制子流程异常: {ex.Message}");
                MessageBoxHelper.ShowError($"复制子流程失败: {ex.Message}", "复制失败");
            }
        }

        /// <summary>
        /// 导出流程命令（导出单个子流程）
        /// </summary>
        [RelayCommand]
        private void ExportWorkflow(WorkflowTab tab)
        {
            if (tab == null)
            {
                Debug.WriteLine("[SequenceViewModel] 导出流程失败: 标签页为 null");
                return;
            }

            if (tab.Type != WorkflowType.Sub)
            {
                MessageBoxHelper.ShowWarning("只能导出子流程，主流程不支持单独导出", "导出失败");
                return;
            }

            try
            {
                var subWorkflow = tab.GetSubWorkflow();
                if (subWorkflow == null)
                {
                    MessageBoxHelper.ShowError("无法获取子流程数据", "导出失败");
                    return;
                }

                // 先保存当前标签页状态，确保 Edges 转换为 Connections
                // 同步节点
                subWorkflow.Nodes = tab.Nodes?.ToList() ?? new List<Node>();

                // 将 Edges 转换为 Connections
                if (subWorkflow.Connections == null)
                {
                    subWorkflow.Connections = new List<Connection>();
                }
                else
                {
                    subWorkflow.Connections.Clear();
                }

                if (tab.Edges != null && tab.Edges.Count > 0)
                {
                    Debug.WriteLine($"[SequenceViewModel] 导出流程 '{tab.Name}': 将 {tab.Edges.Count} 个 Edges 转换为 Connections");
                    foreach (var edge in tab.Edges)
                    {
                        if (edge == null)
                            continue;

                        var connection = new Connection
                        {
                            Id = edge.Id,
                            SourceNodeId = edge.SourceNodeId,
                            TargetNodeId = edge.TargetNodeId,
                            SourcePortId = edge.SourcePortId,
                            TargetPortId = edge.TargetPortId,
                            Type = ConnectionType.Flow, // 默认类型
                            Label = null,
                            Metadata = new Dictionary<string, object>()
                        };
                        subWorkflow.Connections.Add(connection);
                    }
                    Debug.WriteLine($"[SequenceViewModel] 已保存 {subWorkflow.Connections.Count} 个 Connections 到子流程 '{subWorkflow.Name}'");
                }
                else
                {
                    Debug.WriteLine($"[SequenceViewModel] 导出流程 '{tab.Name}': 没有 Edges，清空 Connections");
                }

                // 打开文件保存对话框
                // 确保 Solutions 文件夹存在
                var saveFileDialog = new SaveFileDialog
                {
                    Title = "导出子流程",
                    Filter = FILE_FILTER,
                    FilterIndex = 1,
                    InitialDirectory = SolutionsFolder,
                    FileName = $"{tab.Name}_{DateTime.Now:yyyyMMdd_HHmmss}",
                    DefaultExt = FILE_EXTENSION,
                    AddExtension = true
                };

                if (saveFileDialog.ShowDialog() != true)
                {
                    return; // 用户取消
                }

                var exportFilePath = saveFileDialog.FileName;

                // 使用序列化服务导出
                var result = _workflowSerializer.ExportSingleWorkflowToFile(subWorkflow, exportFilePath);
                if (!result.Success)
                {
                    MessageBoxHelper.ShowError($"导出流程失败: {result.ErrorMessage}", "导出失败");
                    Debug.WriteLine($"[SequenceViewModel] 导出流程失败: {result.ErrorMessage}");
                    return;
                }

                MessageBoxHelper.ShowSuccess($"流程 '{tab.Name}' 已成功导出到:\n{exportFilePath}", "导出成功");
                Debug.WriteLine($"[SequenceViewModel] 导出流程成功: {tab.Name} -> {exportFilePath}");
            }
            catch (Exception ex)
            {
                MessageBoxHelper.ShowError($"导出流程失败: {ex.Message}", "导出失败");
                Debug.WriteLine($"[SequenceViewModel] 导出流程失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 导出所有流程命令
        /// </summary>
        [RelayCommand]
        private void ExportAllWorkflows()
        {
            try
            {
                if (SubWorkflows == null || SubWorkflows.Count == 0)
                {
                    MessageBoxHelper.ShowWarning("没有可导出的子流程", "导出失败");
                    return;
                }

                // 打开文件保存对话框
                var saveFileDialog = new SaveFileDialog
                {
                    Title = "导出所有流程",
                    Filter = FILE_FILTER,
                    FilterIndex = 1,
                    InitialDirectory = SolutionsFolder,
                    FileName = $"所有流程_{DateTime.Now:yyyyMMdd_HHmmss}",
                    DefaultExt = FILE_EXTENSION,
                    AddExtension = true
                };

                if (saveFileDialog.ShowDialog() != true)
                {
                    return; // 用户取消
                }

                var exportFilePath = saveFileDialog.FileName;

                // 导出当前所有流程数据
                var data = ExportMultiWorkflowData();
                if (data == null)
                {
                    MessageBoxHelper.ShowError("导出数据失败，无法保存", "导出失败");
                    return;
                }

                // 使用序列化服务保存
                var result = _workflowSerializer.SaveToFile(data, exportFilePath);
                if (!result.Success)
                {
                    MessageBoxHelper.ShowError($"导出所有流程失败: {result.ErrorMessage}", "导出失败");
                    Debug.WriteLine($"[SequenceViewModel] 导出所有流程失败: {result.ErrorMessage}");
                    return;
                }

                MessageBoxHelper.ShowSuccess($"所有流程已成功导出到:\n{exportFilePath}\n共 {SubWorkflows.Count} 个子流程", "导出成功");
                Debug.WriteLine($"[SequenceViewModel] 导出所有流程成功: {exportFilePath}, 共 {SubWorkflows.Count} 个子流程");
            }
            catch (Exception ex)
            {
                MessageBoxHelper.ShowError($"导出所有流程失败: {ex.Message}", "导出失败");
                Debug.WriteLine($"[SequenceViewModel] 导出所有流程失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 导入子流程命令（自动检测单个或多个）
        /// </summary>
        [RelayCommand]
        private void ImportWorkflows()
        {
            try
            {
                Debug.WriteLine("[SequenceViewModel] 导入子流程命令");

                // 打开文件对话框
                var openFileDialog = new Microsoft.Win32.OpenFileDialog
                {
                    Title = "导入流程",
                    Filter = FILE_FILTER,
                    FilterIndex = 1,
                    InitialDirectory = SolutionsFolder,
                    DefaultExt = FILE_EXTENSION,
                    AddExtension = true
                };

                if (openFileDialog.ShowDialog() != true)
                {
                    Debug.WriteLine("[SequenceViewModel] 用户取消导入子流程");
                    return;
                }

                var filePath = openFileDialog.FileName;

                // 尝试导入多个子流程（会自动处理单个子流程文件）
                var result = _workflowSerializer.ImportMultipleWorkflowsFromFile(filePath);
                if (!result.Success)
                {
                    MessageBoxHelper.ShowError($"导入子流程失败: {result.ErrorMessage}", "导入失败");
                    Debug.WriteLine($"[SequenceViewModel] 导入子流程失败: {result.ErrorMessage}");
                    return;
                }

                var importedWorkflows = result.Data;
                if (importedWorkflows == null || importedWorkflows.Count == 0)
                {
                    MessageBoxHelper.ShowWarning("导入的文件中没有找到子流程", "导入失败");
                    return;
                }

                // 添加到当前流程后面
                int successCount = 0;
                foreach (var workflow in importedWorkflows)
                {
                    try
                    {
                        AddImportedWorkflow(workflow);
                        successCount++;
            }
            catch (Exception ex)
            {
                        Debug.WriteLine($"[SequenceViewModel] 添加导入的子流程失败: {workflow.Name}, 错误: {ex.Message}");
                    }
                }

                // 根据导入数量显示不同的提示信息
                if (importedWorkflows.Count == 1)
                {
                    MessageBoxHelper.ShowSuccess($"子流程 '{importedWorkflows[0].Name}' 已成功导入", "导入成功");
                    Debug.WriteLine($"[SequenceViewModel] 子流程已成功导入: {importedWorkflows[0].Name}");
                }
                else
                {
                    MessageBoxHelper.ShowSuccess($"成功导入 {successCount}/{importedWorkflows.Count} 个子流程", "导入完成");
                    Debug.WriteLine($"[SequenceViewModel] 成功导入 {successCount}/{importedWorkflows.Count} 个子流程");
                }
            }
            catch (Exception ex)
            {
                MessageBoxHelper.ShowError($"导入子流程时发生错误: {ex.Message}", "错误");
                Debug.WriteLine($"[SequenceViewModel] 导入子流程异常: {ex.Message}");
            }
        }

        /// <summary>
        /// 添加导入的子流程到当前流程后面
        /// </summary>
        private void AddImportedWorkflow(WorkFlowNode importedWorkflow)
        {
            if (importedWorkflow == null)
                return;

            // 检查是否已存在相同ID的子流程
            if (SubWorkflows.ContainsKey(importedWorkflow.Id))
            {
                // 如果已存在，生成新的ID
                importedWorkflow.Id = Guid.NewGuid().ToString();
                Debug.WriteLine($"[SequenceViewModel] 检测到ID冲突，已生成新ID: {importedWorkflow.Id}");
            }

            // 检查名称是否冲突，如果冲突则生成新名称
            var originalName = importedWorkflow.Name ?? "未命名流程";
            var existingNames = new HashSet<string>();
            
            // 从 SubWorkflows 字典中获取所有流程名
            foreach (var subWorkflow in SubWorkflows.Values)
            {
                if (!string.IsNullOrEmpty(subWorkflow.Name))
                {
                    existingNames.Add(subWorkflow.Name);
                }
            }
            
            // 从 SubWorkflowTabs 中获取所有标签页名（作为备用检查）
            foreach (var existingTab in SubWorkflowTabs)
            {
                if (!string.IsNullOrEmpty(existingTab.Name))
                {
                    existingNames.Add(existingTab.Name);
                }
            }

            // 如果名称冲突，生成新名称
            if (existingNames.Contains(originalName))
            {
                var newName = originalName;
                int suffix = 1;
                while (existingNames.Contains(newName))
                {
                    newName = $"{originalName}_{suffix}";
                    suffix++;
                }
                importedWorkflow.Name = newName;
                Debug.WriteLine($"[SequenceViewModel] 检测到名称冲突，已更新名称: {originalName} -> {newName}");
            }

            // 确保导入的工作流关系已重建（虽然序列化器已经调用过，但为了保险再次调用）
            importedWorkflow.RebuildRelationships();

            // 添加到子流程字典
            SubWorkflows[importedWorkflow.Id] = importedWorkflow;

            // 创建标签页
            var nodes = new ObservableCollection<Node>(importedWorkflow.Nodes ?? new List<Node>());
            var edges = new ObservableCollection<Edge>();

            // 将 Connections 转换为 Edges
            if (importedWorkflow.Connections != null && importedWorkflow.Connections.Count > 0)
            {
                Debug.WriteLine($"[SequenceViewModel] 导入的工作流包含 {importedWorkflow.Connections.Count} 个连接");
                foreach (var connection in importedWorkflow.Connections)
                {
                    if (connection == null)
                    {
                        Debug.WriteLine($"[SequenceViewModel] 警告：发现空的连接对象");
                        continue;
                    }

                    var edge = new Edge
                    {
                        Id = connection.Id,
                        SourceNodeId = connection.SourceNodeId,
                        TargetNodeId = connection.TargetNodeId,
                        SourcePortId = connection.SourcePortId,
                        TargetPortId = connection.TargetPortId
                    };
                    edges.Add(edge);
                    Debug.WriteLine($"[SequenceViewModel] 转换连接: {connection.SourceNodeId} -> {connection.TargetNodeId}");
                }
            }
            else
            {
                Debug.WriteLine($"[SequenceViewModel] 警告：导入的工作流没有连接（Connections 为 null 或空）");
            }

            var tab = new WorkflowTab
            {
                Name = importedWorkflow.Name ?? "未命名流程",
                Type = WorkflowType.Sub,
                WorkflowData = importedWorkflow,
                Nodes = nodes,
                Edges = edges,
                IsModified = false, // 导入的流程标记为未修改
                IsActive = false // 初始化为非活动状态
            };

            // 添加到标签页集合（添加到后面）
            WorkflowTabs.Add(tab);
            SubWorkflowTabs.Add(tab);

            // 如果主流程编辑界面已打开，刷新主流程节点
            if (IsMasterWorkflowViewVisible)
            {
                RefreshMasterWorkflowNodes();
            }

            Debug.WriteLine($"[SequenceViewModel] 已添加导入的子流程: {importedWorkflow.Name} (ID: {importedWorkflow.Id})");
        }

        /// <summary>
        /// 暂停命令
        /// </summary>
        [RelayCommand]
        private void Pause()
        {
            Debug.WriteLine("[SequenceViewModel] 暂停命令");
            StatusMessage = "流程已暂停";
            // TODO: 实现流程暂停逻辑
        }

        /// <summary>
        /// 停止命令
        /// </summary>
        [RelayCommand]
        private void Stop()
        {
            Debug.WriteLine("[SequenceViewModel] 停止命令");
            StatusMessage = "流程已停止";
            ProcessTime = 0.0;
            // TODO: 实现流程停止逻辑
        }

        /// <summary>
        /// 切换全屏命令
        /// </summary>
        [RelayCommand]
        private void ToggleFullscreen()
        {
            IsFullscreen = !IsFullscreen;
            Debug.WriteLine($"[SequenceViewModel] 切换全屏: {IsFullscreen}");
            // TODO: 实现全屏切换逻辑
        }

        /// <summary>
        /// 打开主流程编辑命令（切换主流程编辑界面显示/隐藏）
        /// </summary>
        [RelayCommand]
        private void OpenMasterWorkflow()
        {
            Debug.WriteLine($"[SequenceViewModel] 打开主流程编辑命令，当前状态: IsMasterWorkflowViewVisible={IsMasterWorkflowViewVisible}");

            // 如果当前是主流程编辑界面，则隐藏它，切回子流程编辑界面
            if (IsMasterWorkflowViewVisible)
            {
                // 先保存主流程状态（包括连线），确保数据不丢失
                SaveMasterWorkflowState();

                IsMasterWorkflowViewVisible = false;
                IsMasterWorkflow = false;

                // 切换到第一个子流程标签页（如果存在）
                var firstSubTab = SubWorkflowTabs.FirstOrDefault();
                if (firstSubTab != null)
                {
                    SwitchWorkflow(firstSubTab);
                }
                else
                {
                    CurrentTab = null;
                }

                Debug.WriteLine("[SequenceViewModel] 已切换到子流程编辑界面");
                return;
            }

            // 如果当前是子流程编辑界面，则显示主流程编辑界面
            // 创建或初始化主流程标签页（不添加到 WorkflowTabs）
            if (MasterWorkflowTab == null)
            {
                MasterWorkflowTab = new WorkflowTab
                {
                    Name = "主流程",
                    Type = WorkflowType.Master,
                    IsActive = true,
                    WorkflowData = MasterWorkflow
                };

                // 初始化主流程的节点和连线（用于显示流程引用节点）
                MasterWorkflowTab.Nodes = new ObservableCollection<Node>();
                MasterWorkflowTab.Edges = new ObservableCollection<Edge>();

                Debug.WriteLine("[SequenceViewModel] 创建新的 MasterWorkflowTab");
            }
            else
            {
                // 确保 MasterWorkflowTab 的数据正确
                if (MasterWorkflowTab.WorkflowData == null)
                {
                    MasterWorkflowTab.WorkflowData = MasterWorkflow;
                }
                if (MasterWorkflowTab.Nodes == null)
                {
                    MasterWorkflowTab.Nodes = new ObservableCollection<Node>();
                }
                if (MasterWorkflowTab.Edges == null)
                {
                    MasterWorkflowTab.Edges = new ObservableCollection<Edge>();
                }

                Debug.WriteLine($"[SequenceViewModel] 使用已存在的 MasterWorkflowTab，当前节点数: {MasterWorkflowTab.Nodes.Count}");
            }

            // 显示主流程编辑界面
            IsMasterWorkflowViewVisible = true;
            IsMasterWorkflow = true;

            // 初始化主流程编辑界面（订阅事件）
            InitializeMasterWorkflowView();

            // 检查是否需要刷新节点（只有在节点数量不匹配或首次打开时才刷新）
            bool needRefresh = false;
            if (MasterWorkflowTab.Nodes.Count == 0)
            {
                // 首次打开，需要刷新
                needRefresh = true;
                Debug.WriteLine("[SequenceViewModel] 首次打开主流程界面，需要刷新节点");
            }
            else
            {
                // 检查子流程数量是否与节点数量匹配
                var masterWorkflow = MasterWorkflowTab.GetMasterWorkflow();
                if (masterWorkflow != null)
                {
                    int subWorkflowCount = SubWorkflows.Count;
                    int nodeCount = MasterWorkflowTab.Nodes.Count;
                    if (subWorkflowCount != nodeCount)
                    {
                        needRefresh = true;
                        Debug.WriteLine($"[SequenceViewModel] 子流程数量 ({subWorkflowCount}) 与节点数量 ({nodeCount}) 不匹配，需要刷新");
                    }
                    else
                    {
                        // 检查是否有新增或删除的子流程
                        var nodeSubWorkflowIds = MasterWorkflowTab.Nodes.OfType<WorkflowReferenceNode>()
                            .Select(n => n.SubWorkflowId)
                            .ToHashSet();
                        var currentSubWorkflowIds = SubWorkflows.Keys.ToHashSet();

                        if (!nodeSubWorkflowIds.SetEquals(currentSubWorkflowIds))
                        {
                            needRefresh = true;
                            Debug.WriteLine("[SequenceViewModel] 子流程列表发生变化，需要刷新");
                        }
                    }
                }
            }

            // 只有在需要时才刷新，避免不必要的清空和重建
            if (needRefresh)
            {
                // 先保存当前状态（如果之前有修改）
                SaveMasterWorkflowState();

                // 立即刷新一次（确保数据准备就绪）
                RefreshMasterWorkflowNodes();

                // 延迟再次刷新，确保 UI 已完全加载并更新
                System.Windows.Application.Current?.Dispatcher.BeginInvoke(new System.Action(() =>
                {
                    RefreshMasterWorkflowNodes();
                    Debug.WriteLine($"[SequenceViewModel] 延迟刷新完成，节点数: {MasterWorkflowTab?.Nodes?.Count ?? 0}, 连线数: {MasterWorkflowTab?.Edges?.Count ?? 0}");
                }), System.Windows.Threading.DispatcherPriority.Loaded);
            }
            else
            {
                Debug.WriteLine($"[SequenceViewModel] 无需刷新，直接显示现有节点和连线。节点数: {MasterWorkflowTab.Nodes.Count}, 连线数: {MasterWorkflowTab.Edges.Count}");

                // 即使不刷新，也要确保数据绑定正确
                OnPropertyChanged(nameof(MasterWorkflowTab));
            }

            Debug.WriteLine($"[SequenceViewModel] 已切换到主流程编辑界面，子流程总数: {SubWorkflows.Count}");
        }

        /// <summary>
        /// 刷新主流程节点（显示所有已创建的子流程）
        /// </summary>
        private void RefreshMasterWorkflowNodes()
        {
            if (MasterWorkflowTab == null)
            {
                Debug.WriteLine("[SequenceViewModel] 刷新主流程节点失败: MasterWorkflowTab 为 null");
                return;
            }

            var masterWorkflow = MasterWorkflowTab.GetMasterWorkflow();
            if (masterWorkflow == null)
            {
                Debug.WriteLine("[SequenceViewModel] 刷新主流程节点失败: MasterWorkflow 为 null");
                return;
            }

            Debug.WriteLine($"[SequenceViewModel] 开始刷新主流程节点，子流程数量: {SubWorkflows.Count}");
            Debug.WriteLine($"[SequenceViewModel] 刷新前 MasterWorkflowTab.Nodes 数量: {MasterWorkflowTab.Nodes.Count}");

            // 创建节点ID到节点的映射（用于复用已存在的节点）
            var existingNodes = MasterWorkflowTab.Nodes.OfType<WorkflowReferenceNode>()
                .ToDictionary(n => n.Id, n => n);
            var existingEdges = MasterWorkflowTab.Edges.ToList();

            // 保存当前连线（在清空前保存，避免丢失）
            var edgesToRestore = MasterWorkflowTab.Edges.ToList();

            // 清空现有节点和连线（稍后会重新添加）
            MasterWorkflowTab.Nodes.Clear();
            MasterWorkflowTab.Edges.Clear();

            Debug.WriteLine($"[SequenceViewModel] 已清空节点和连线，准备重新添加，保存的连线数: {edgesToRestore.Count}");

            // 为每个子流程创建或复用引用节点
            int index = 0;

            // 检查是否需要重新分散排布（如果所有节点位置相同或都是默认位置）
            bool needRelayout = false;
            if (masterWorkflow.SubWorkflowReferences.Count > 1)
            {
                var firstPosition = masterWorkflow.SubWorkflowReferences[0].Position;
                var allSamePosition = masterWorkflow.SubWorkflowReferences.All(r =>
                    Math.Abs(r.Position.X - firstPosition.X) < 0.01 &&
                    Math.Abs(r.Position.Y - firstPosition.Y) < 0.01);
                var allDefaultPosition = masterWorkflow.SubWorkflowReferences.All(r =>
                    (Math.Abs(r.Position.X) < 0.01 && Math.Abs(r.Position.Y) < 0.01) ||
                    (Math.Abs(r.Position.X - 100) < 0.01 && Math.Abs(r.Position.Y - 100) < 0.01));

                needRelayout = allSamePosition || allDefaultPosition;

                if (needRelayout)
                {
                    Debug.WriteLine($"[SequenceViewModel] 检测到节点位置重合，重新分散排布");
                }
            }

            foreach (var subWorkflow in SubWorkflows.Values)
            {
                // 检查是否已存在引用
                var existingRef = masterWorkflow.SubWorkflowReferences
                    .FirstOrDefault(r => r.SubWorkflowId == subWorkflow.Id);

                // 始终从 subWorkflow.Name 获取最新名称（这是数据源，与 TabItem Header 绑定的是同一个对象）
                var workflowName = subWorkflow.Name ?? "未命名流程";

                if (existingRef == null)
                {
                    // 创建新的流程引用，使用分散排布
                    existingRef = new WorkflowReference
                    {
                        SubWorkflowId = subWorkflow.Id,
                        DisplayName = workflowName, // 使用最新名称
                        Position = new Point2D(100 + index * 250, 100),
                        Size = new Size2D(200, 150)
                    };
                    masterWorkflow.AddSubWorkflowReference(existingRef);
                    Debug.WriteLine($"[SequenceViewModel] 创建新的流程引用: SubWorkflowId={subWorkflow.Id}, DisplayName={workflowName}");
                }
                else
                {
                    // 更新引用的显示名称（确保使用最新名称）
                    if (existingRef.DisplayName != workflowName)
                    {
                        existingRef.DisplayName = workflowName;
                        Debug.WriteLine($"[SequenceViewModel] 更新流程引用显示名称: SubWorkflowId={subWorkflow.Id}, DisplayName={workflowName}");
                    }

                    if (needRelayout)
                    {
                        // 如果需要重新排布，更新位置为分散排布
                        existingRef.Position = new Point2D(100 + index * 250, 100);
                    }
                }

                // 复用已存在的节点或创建新节点（使用引用ID作为节点ID，保持稳定）
                // 注意：SubWorkflowName 和 Name 都从 subWorkflow.Name 获取，确保与 TabItem Header 绑定的是同一个数据源
                // WorkflowTab.Name 对于子流程会直接返回 WorkFlowNode.Name，所以它们绑定的是同一个数据源
                WorkflowReferenceNode workflowNode;

                if (existingNodes.TryGetValue(existingRef.Id, out var existingNode))
                {
                    // 复用已存在的节点，只更新属性
                    // 确保 SubWorkflowName 和 Name 都从子流程的 Name 获取（与 TabItem Header 绑定的是同一个对象）
                    workflowNode = existingNode;

                    // 始终使用最新名称更新节点（即使节点已存在，也要确保名称是最新的）
                    if (workflowNode.SubWorkflowName != workflowName || workflowNode.Name != workflowName)
                    {
                        workflowNode.SubWorkflowName = workflowName;
                        workflowNode.Name = workflowName;
                        Debug.WriteLine($"[SequenceViewModel] 更新已存在节点的名称: SubWorkflowId={subWorkflow.Id}, 旧名称={existingNode.Name}, 新名称={workflowName}");
                    }

                    workflowNode.Position = existingRef.Position;
                    workflowNode.Size = existingRef.Size;
                }
                else
                {
                    // 创建新节点，使用引用ID作为节点ID（保持稳定）
                    // 确保 SubWorkflowName 和 Name 都从子流程的 Name 获取（与 TabItem Header 绑定的是同一个对象）
                    workflowNode = new WorkflowReferenceNode
                    {
                        Id = existingRef.Id, // 使用引用ID作为节点ID，确保稳定
                        SubWorkflowId = subWorkflow.Id,
                        SubWorkflowName = workflowName,
                        Name = workflowName,
                        Position = existingRef.Position,
                        Size = existingRef.Size
                    };
                    Debug.WriteLine($"[SequenceViewModel] 创建新节点: SubWorkflowId={subWorkflow.Id}, Name={workflowName}");
                }

                MasterWorkflowTab.Nodes.Add(workflowNode);
                Debug.WriteLine($"[SequenceViewModel] 添加流程节点: {workflowNode.Name} (ID: {workflowNode.Id}, SubWorkflowId: {workflowNode.SubWorkflowId}), 当前节点数: {MasterWorkflowTab.Nodes.Count}");
                index++;
            }

            // 恢复连线：优先使用 masterWorkflow.Edges 中保存的连线，如果没有则使用之前保存的连线
            var edgesToAdd = masterWorkflow.Edges.Count > 0 ? masterWorkflow.Edges : edgesToRestore;
            int restoredEdgeCount = 0;
            foreach (var savedEdge in edgesToAdd)
            {
                // 验证连线的节点ID是否存在于当前节点集合中
                var sourceNodeExists = MasterWorkflowTab.Nodes.Any(n => n.Id == savedEdge.SourceNodeId);
                var targetNodeExists = MasterWorkflowTab.Nodes.Any(n => n.Id == savedEdge.TargetNodeId);

                if (sourceNodeExists && targetNodeExists)
                {
                    // 直接使用保存的 Edge，因为节点ID已经是引用ID（稳定）
                    MasterWorkflowTab.Edges.Add(savedEdge);
                    restoredEdgeCount++;
                    Debug.WriteLine($"[SequenceViewModel] 恢复连线: {savedEdge.SourceNodeId} -> {savedEdge.TargetNodeId}");
                }
                else
                {
                    Debug.WriteLine($"[SequenceViewModel] 跳过无效连线: {savedEdge.SourceNodeId} -> {savedEdge.TargetNodeId} (节点不存在)");
                }
            }

            Debug.WriteLine($"[SequenceViewModel] 连线恢复完成: 共 {edgesToAdd.Count} 条连线，成功恢复 {restoredEdgeCount} 条");

            Debug.WriteLine($"[SequenceViewModel] 刷新主流程节点完成: 显示 {MasterWorkflowTab.Nodes.Count} 个子流程, {MasterWorkflowTab.Edges.Count} 条连线");

            // 通知属性变更：通过 OnPropertyChanged 手动触发 MasterWorkflowTab 属性变更通知
            // 这样可以确保绑定到 MasterWorkflowTab.Nodes 的 UI 能够更新
            OnPropertyChanged(nameof(MasterWorkflowTab));

            Debug.WriteLine($"[SequenceViewModel] 已触发 MasterWorkflowTab 属性变更通知");
        }

        /// <summary>
        /// 更新主流程中对应子流程节点的名称（当子流程重命名时调用）
        /// </summary>
        private void UpdateMasterWorkflowNodeName(string subWorkflowId, string newName)
        {
            if (string.IsNullOrEmpty(subWorkflowId) || string.IsNullOrEmpty(newName))
            {
                return;
            }

            Debug.WriteLine($"[SequenceViewModel] UpdateMasterWorkflowNodeName: SubWorkflowId={subWorkflowId}, NewName={newName}");

            // 先更新主流程数据中的引用显示名称（无论主流程界面是否打开都要更新）
            var masterWorkflow = MasterWorkflow;
            if (masterWorkflow != null)
            {
                var reference = masterWorkflow.GetSubWorkflowReference(subWorkflowId);
                if (reference != null)
                {
                    reference.DisplayName = newName;
                    Debug.WriteLine($"[SequenceViewModel] 已更新主流程引用显示名称: SubWorkflowId={subWorkflowId}, DisplayName={newName}");
                }
                else
                {
                    Debug.WriteLine($"[SequenceViewModel] 警告: 未找到主流程引用: SubWorkflowId={subWorkflowId}");
                }
            }

            // 如果主流程标签页存在，更新对应的节点（用于UI显示）
            if (MasterWorkflowTab != null)
            {
                // 尝试通过 SubWorkflowId 查找节点
                var workflowNode = MasterWorkflowTab.Nodes.OfType<WorkflowReferenceNode>()
                    .FirstOrDefault(n => n.SubWorkflowId == subWorkflowId);

                if (workflowNode != null)
                {
                    Debug.WriteLine($"[SequenceViewModel] 找到主流程节点: ID={workflowNode.Id}, 当前名称={workflowNode.Name}");

                    // 更新节点的名称和子流程名称（它们应该保持一致）
                    workflowNode.SubWorkflowName = newName;
                    workflowNode.Name = newName;

                    Debug.WriteLine($"[SequenceViewModel] 已更新主流程节点名称: SubWorkflowId={subWorkflowId}, NewName={newName}");

                    // 由于 Node 没有实现 INotifyPropertyChanged，需要通过集合变更来触发UI更新
                    // 方法：临时移除节点再添加回来，触发 CollectionChanged 事件和 DataContextChanged
                    var nodeIndex = MasterWorkflowTab.Nodes.IndexOf(workflowNode);
                    if (nodeIndex >= 0)
                    {
                        // 保存节点的所有重要状态
                        var wasSelected = workflowNode.IsSelected;
                        var wasEnabled = workflowNode.IsEnabled;
                        var position = workflowNode.Position;
                        var size = workflowNode.Size;

                        // 临时移除节点（触发 Remove 事件，会触发 DataContextChanged）
                        MasterWorkflowTab.Nodes.RemoveAt(nodeIndex);

                        // 使用 Dispatcher 延迟添加，确保 UI 有时间处理移除事件
                        System.Windows.Application.Current?.Dispatcher.BeginInvoke(new System.Action(() =>
                        {
                            // 立即添加回来（在同一位置，触发 Add 事件，会再次触发 DataContextChanged）
                            MasterWorkflowTab.Nodes.Insert(nodeIndex, workflowNode);

                            // 恢复所有状态
                            workflowNode.IsSelected = wasSelected;
                            workflowNode.IsEnabled = wasEnabled;
                            workflowNode.Position = position;
                            workflowNode.Size = size;

                            Debug.WriteLine($"[SequenceViewModel] 已通过集合变更触发UI更新: 节点索引={nodeIndex}, 新名称={newName}");
                        }), System.Windows.Threading.DispatcherPriority.DataBind);
                    }

                    // 触发属性变更通知，确保UI更新
                    OnPropertyChanged(nameof(MasterWorkflowTab));
                }
                else
                {
                    Debug.WriteLine($"[SequenceViewModel] 未找到对应的主流程节点: SubWorkflowId={subWorkflowId}");
                    Debug.WriteLine($"[SequenceViewModel] 当前主流程节点列表 (共 {MasterWorkflowTab.Nodes.Count} 个节点):");
                    foreach (var node in MasterWorkflowTab.Nodes.OfType<WorkflowReferenceNode>())
                    {
                        Debug.WriteLine($"[SequenceViewModel]   - 节点: Name={node.Name}, Id={node.Id}, SubWorkflowId={node.SubWorkflowId}, SubWorkflowName={node.SubWorkflowName}");
                    }
                    Debug.WriteLine($"[SequenceViewModel] 所有子流程ID列表:");
                    foreach (var subWorkflow in SubWorkflows.Values)
                    {
                        Debug.WriteLine($"[SequenceViewModel]   - 子流程: Name={subWorkflow.Name}, Id={subWorkflow.Id}");
                    }
                }
            }
            else
            {
                Debug.WriteLine($"[SequenceViewModel] MasterWorkflowTab 为 null，主流程界面可能未打开，但已更新主流程引用显示名称");
                Debug.WriteLine($"[SequenceViewModel] 注意: 当主流程界面打开时，RefreshMasterWorkflowNodes 会使用最新名称创建节点");
            }
        }

        #endregion
    }

    /// <summary>
    /// 流程连接参数（用于 RelayCommand）
    /// </summary>
    public class WorkflowConnectionParams
    {
        public string SourceWorkflowId { get; set; }
        public string TargetWorkflowId { get; set; }
    }
}

#endregion