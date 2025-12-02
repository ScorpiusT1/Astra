using Astra.Models;
using Astra.UI.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using NavStack.Core;
using NavStack.Services;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;

namespace Astra.ViewModels
{
    public partial class SequenceViewModel : ObservableObject
    {
        private readonly IFrameNavigationService _navigationService;

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
        /// 画布数据源 - 节点集合
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<CanvasNode> _canvasItemsSource;

        public SequenceViewModel(IFrameNavigationService navigationService)
        {
            _navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));
            
            // 订阅导航事件
            SubscribeToNavigationEvents();

            // 初始化数据源
            InitializeDataSources();
        }

        /// <summary>
        /// 初始化数据源
        /// </summary>
        private void InitializeDataSources()
        {
            // 初始化工具箱数据源
            ToolBoxItemsSource = new ObservableCollection<ToolCategory>();
            
            // 初始化画布数据源
            CanvasItemsSource = new ObservableCollection<CanvasNode>();

            // 创建示例工具类别
            CreateSampleToolCategories();
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
            basicCategory.Tools.Add(new ToolItem { Name = "开始", IconCode = FlowEditorIcons.Start, Description = "流程开始节点" });
            basicCategory.Tools.Add(new ToolItem { Name = "结束", IconCode = FlowEditorIcons.End, Description = "流程结束节点" });
            basicCategory.Tools.Add(new ToolItem { Name = "等待", IconCode = FlowEditorIcons.Wait, Description = "等待节点" });
            ToolBoxItemsSource.Add(basicCategory);
            Debug.WriteLine($"[SequenceViewModel] 添加基础节点类别: {basicCategory.Name}, 工具数量: {basicCategory.Tools.Count}");

            // 逻辑节点类别
            var logicCategory = new ToolCategory
            {
                Name = "逻辑节点",
                IconCode = FlowEditorIcons.LogicCategory,
                Description = "逻辑控制节点",
                CategoryColor = Application.Current?.FindResource("InfoBrush") as Brush,
                CategoryLightColor = Application.Current?.FindResource("LightInfoBrush") as Brush
            };

            logicCategory.Tools.Add(new ToolItem { Name = "条件判断", IconCode = FlowEditorIcons.Condition, Description = "条件判断节点" });
            logicCategory.Tools.Add(new ToolItem { Name = "循环", IconCode = FlowEditorIcons.Loop, Description = "循环节点" });
            logicCategory.Tools.Add(new ToolItem { Name = "并行", IconCode = FlowEditorIcons.Parallel, Description = "并行执行节点" });
            ToolBoxItemsSource.Add(logicCategory);
            Debug.WriteLine($"[SequenceViewModel] 添加逻辑节点类别: {logicCategory.Name}, 工具数量: {logicCategory.Tools.Count}");

            // 设备节点类别
            var deviceCategory = new ToolCategory
            {
                Name = "设备节点",
                IconCode = FlowEditorIcons.DeviceCategory,
                Description = "设备操作节点",
                CategoryColor = Application.Current?.FindResource("SuccessBrush") as Brush,
                CategoryLightColor = Application.Current?.FindResource("LightSuccessBrush") as Brush
            };
            deviceCategory.Tools.Add(new ToolItem { Name = "PLC控制", IconCode = FlowEditorIcons.PLC, Description = "PLC控制节点" });
            deviceCategory.Tools.Add(new ToolItem { Name = "扫码枪", IconCode = FlowEditorIcons.Scanner, Description = "扫码枪节点" });
            deviceCategory.Tools.Add(new ToolItem { Name = "传感器", IconCode = FlowEditorIcons.Sensor, Description = "传感器节点" });
            ToolBoxItemsSource.Add(deviceCategory);
            Debug.WriteLine($"[SequenceViewModel] 添加设备节点类别: {deviceCategory.Name}, 工具数量: {deviceCategory.Tools.Count}");

            Debug.WriteLine($"[SequenceViewModel] 工具类别总数: {ToolBoxItemsSource.Count}");
        }

        /// <summary>
        /// 节点创建工厂 - 从工具项创建画布节点
        /// </summary>
        public Func<IToolItem, Point, object> NodeFactory
        {
            get
            {
                return (toolItem, position) =>
                {
                    return new CanvasNode
                    {
                        Name = toolItem.Name,
                        X = position.X,
                        Y = position.Y,
                        Width = 120,
                        Height = 60
                    };
                };
            }
        }

        /// <summary>
        /// 订阅导航事件
        /// </summary>
        private void SubscribeToNavigationEvents()
        {
            // 订阅导航开始事件
            _navigationService.Navigating += OnNavigationStarted;
            
            // 订阅导航完成事件
            _navigationService.Navigated += OnNavigationCompleted;
            
            // 订阅导航失败事件
            _navigationService.NavigationFailed += OnNavigationFailed;
            
            Debug.WriteLine("[SequenceViewModel] 已订阅导航事件");
        }

        /// <summary>
        /// 取消订阅导航事件
        /// </summary>
        public void UnsubscribeFromNavigationEvents()
        {
            if (_navigationService != null)
            {
                _navigationService.Navigating -= OnNavigationStarted;
                _navigationService.Navigated -= OnNavigationCompleted;
                _navigationService.NavigationFailed -= OnNavigationFailed;
                
                Debug.WriteLine("[SequenceViewModel] 已取消订阅导航事件");
            }
        }

        #region 导航事件处理

        /// <summary>
        /// 导航开始事件处理
        /// </summary>
        private void OnNavigationStarted(object sender, NavigationEventArgs e)
        {
            Debug.WriteLine($"[SequenceViewModel] 导航开始事件触发");
            Debug.WriteLine($"[SequenceViewModel] 目标页面: {e.Context?.NavigationUri}");
            Debug.WriteLine($"[SequenceViewModel] 导航模式: {e.Context?.NavigationMode}");
            
            // 设置导航状态
            IsNavigating = true;
            
            // 这里可以添加导航开始时的逻辑
            // 例如：显示加载指示器、禁用某些操作等
            
            // 示例：如果是导航到当前页面，可以做一些特殊处理
            if (e.Context?.NavigationUri == NavigationKeys.Sequence)
            {
                Debug.WriteLine("[SequenceViewModel] 正在导航到序列编辑页面");
                // 可以在这里做一些准备工作
            }
        }

        /// <summary>
        /// 导航完成事件处理
        /// </summary>
        private void OnNavigationCompleted(object sender, NavigationEventArgs e)
        {
            Debug.WriteLine($"[SequenceViewModel] 导航完成事件触发");
            Debug.WriteLine($"[SequenceViewModel] 当前页面: {e.Context?.NavigationUri}");
            Debug.WriteLine($"[SequenceViewModel] 导航模式: {e.Context?.NavigationMode}");
            
            // 清除导航状态
            IsNavigating = false;
            
            // 这里可以添加导航完成时的逻辑
            // 例如：隐藏加载指示器、启用操作、刷新数据等
            
            // 示例：如果导航到了当前页面，可以做一些初始化工作
            if (e.Context?.NavigationUri == NavigationKeys.Sequence)
            {
                Debug.WriteLine("[SequenceViewModel] 已成功导航到序列编辑页面");
                // 可以在这里做一些页面初始化工作
                InitializeSequencePage();
            }
        }

        /// <summary>
        /// 导航失败事件处理
        /// </summary>
        private void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
        {
            Debug.WriteLine($"[SequenceViewModel] 导航失败事件触发");
            Debug.WriteLine($"[SequenceViewModel] 失败原因: {e.Exception?.Message}");
            Debug.WriteLine($"[SequenceViewModel] 目标页面: {e.Context?.NavigationUri}");
            
            // 清除导航状态
            IsNavigating = false;
            
            // 这里可以添加导航失败时的逻辑
            // 例如：显示错误消息、记录日志等
            
            // 示例：处理导航失败
            HandleNavigationFailure(e.Exception, e.Context?.NavigationUri);
        }

        #endregion

        #region 私有方法

        /// <summary>
        /// 初始化序列页面
        /// </summary>
        private void InitializeSequencePage()
        {
            Debug.WriteLine("[SequenceViewModel] 开始初始化序列页面");
            
            // 这里可以添加页面初始化逻辑
            // 例如：加载数据、设置UI状态等
            
            Debug.WriteLine("[SequenceViewModel] 序列页面初始化完成");
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

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            UnsubscribeFromNavigationEvents();
        }
    }
}
