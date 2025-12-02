using Astra.UI.Adapters;
using Astra.UI.Serivces;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Astra.UI.Controls
{
    /// <summary>
    /// 工具项拖拽事件参数
    /// </summary>
    public class ToolItemDragEventArgs : EventArgs
    {
        /// <summary>
        /// 被拖拽的工具项
        /// </summary>
        public IToolItem ToolItem { get; }

        /// <summary>
        /// 类别名称
        /// </summary>
        public string CategoryName { get; }

        /// <summary>
        /// 拖拽开始位置（相对于控件）
        /// </summary>
        public Point StartPosition { get; }

        /// <summary>
        /// 拖拽数据对象
        /// </summary>
        public IDataObject DragData { get; }

        /// <summary>
        /// 是否可以取消拖拽
        /// </summary>
        public bool CanCancel { get; set; }

        /// <summary>
        /// 是否已取消拖拽
        /// </summary>
        public bool IsCancelled { get; private set; }

        /// <summary>
        /// 取消拖拽
        /// </summary>
        public void Cancel()
        {
            if (CanCancel)
            {
                IsCancelled = true;
            }
        }

        public ToolItemDragEventArgs(IToolItem toolItem, string categoryName, Point startPosition, IDataObject dragData)
        {
            ToolItem = toolItem ?? throw new ArgumentNullException(nameof(toolItem));
         
            CategoryName = categoryName ?? string.Empty;
            StartPosition = startPosition;
            DragData = dragData ?? throw new ArgumentNullException(nameof(dragData));
            CanCancel = true;
        }
    }
    /// <summary>
    /// 工具项选择事件参数 - 只依赖接口，不依赖具体类型
    /// </summary>
    public class ToolItemSelectedEventArgs : EventArgs
    {
        /// <summary>
        /// 类别名称
        /// </summary>
        public string CategoryName { get; }

        /// <summary>
        /// 工具项名称
        /// </summary>
        public string ToolName { get; }

        /// <summary>
        /// 工具项接口（不依赖具体类型）
        /// </summary>
        public IToolItem ToolItem { get; }

        /// <summary>
        /// 工具项对象（可选，用于向后兼容，可能为 null）
        /// </summary>
        public object ToolItemObject { get; }

        public ToolItemSelectedEventArgs(string categoryName, string toolName, IToolItem toolItem, object toolItemObject = null)
        {
            CategoryName = categoryName ?? string.Empty;
            ToolName = toolName ?? string.Empty;
            ToolItem = toolItem ?? throw new ArgumentNullException(nameof(toolItem));
            ToolItemObject = toolItemObject;
        }
    }

    /// <summary>
    /// 工具项拖拽完成事件参数
    /// </summary>
    public class ToolItemDragCompletedEventArgs : EventArgs
    {
        /// <summary>
        /// 被拖拽的工具项
        /// </summary>
        public IToolItem ToolItem { get; }


        /// <summary>
        /// 类别名称
        /// </summary>
        public string CategoryName { get; }

        /// <summary>
        /// 拖拽结果
        /// </summary>
        public DragDropEffects Result { get; }

        /// <summary>
        /// 是否成功完成拖拽
        /// </summary>
        public bool IsSuccess => Result != DragDropEffects.None;

        public ToolItemDragCompletedEventArgs(IToolItem toolItem, string categoryName, DragDropEffects result)
        {
            ToolItem = toolItem ?? throw new ArgumentNullException(nameof(toolItem));
           
            CategoryName = categoryName ?? string.Empty;
            Result = result;
        }
    }
    /// <summary>
    /// 工具类别选择事件参数 - 只依赖接口，不依赖具体类型
    /// </summary>
    public class ToolCategorySelectedEventArgs : EventArgs
    {
        /// <summary>
        /// 类别名称
        /// </summary>
        public string CategoryName { get; }

        /// <summary>
        /// 类别对象（可选，用于向后兼容，可能为 null）
        /// </summary>
        public object CategoryObject { get; }

        public ToolCategorySelectedEventArgs(string categoryName, object categoryObject = null)
        {
            CategoryName = categoryName ?? string.Empty;
            CategoryObject = categoryObject;
        }
    }

    /// <summary>
    /// 拖拽数据构建器接口 - 遵循依赖倒置原则，允许外部自定义拖拽数据格式
    /// </summary>
    public interface IDragDropPayloadBuilder
    {
        /// <summary>
        /// 根据工具项和类别构建拖拽数据对象
        /// </summary>
        /// <param name="toolItem">工具项接口</param>
        /// <param name="category">类别接口</param>
        /// <returns>拖拽数据对象，如果构建失败返回 null</returns>
        DataObject BuildPayload(IToolItem toolItem, IToolCategory<IToolItem> category);
    }

    /// <summary>
    /// 工具类别接口 - 用于抽象工具类别的基本属性
    /// </summary>
    /// <typeparam name="TItem">工具项类型，必须实现 IToolItem 接口</typeparam>
    public interface IToolCategory<TItem> : INotifyPropertyChanged
        where TItem : IToolItem
    {
        /// <summary>
        /// 类别名称
        /// </summary>
        string Name { get; set; }

        /// <summary>
        /// 图标代码
        /// </summary>
        string IconCode { get; set; }

        /// <summary>
        /// 类别描述
        /// </summary>
        string Description { get; set; }

        /// <summary>
        /// 工具项集合
        /// </summary>
        ObservableCollection<TItem> Tools { get; set; }

        /// <summary>
        /// 是否被选中
        /// </summary>
        bool IsSelected { get; set; }

        /// <summary>
        /// 是否启用
        /// </summary>
        bool IsEnabled { get; set; }

        /// <summary>
        /// 类别主题颜色（用于图标背景和装饰条）
        /// </summary>
        Brush CategoryColor { get; set; }

        /// <summary>
        /// 类别浅色（用于图标背景渐变和边框）
        /// </summary>
        Brush CategoryLightColor { get; set; }
    }

    /// <summary>
    /// 工具项接口 - 用于抽象工具项的基本属性
    /// </summary>
    public interface IToolItem : INotifyPropertyChanged
    {
        /// <summary>
        /// 工具名称
        /// </summary>
        string Name { get; set; }

        /// <summary>
        /// 图标代码
        /// </summary>
        string IconCode { get; set; }

        /// <summary>
        /// 工具描述
        /// </summary>
        string Description { get; set; }

        /// <summary>
        /// 是否被选中
        /// </summary>
        bool IsSelected { get; set; }

        /// <summary>
        /// 是否启用
        /// </summary>
        bool IsEnabled { get; set; }
    }

    /// <summary>
    /// 模板部件名称常量 - 遵循单一职责原则，集中管理所有模板元素名称
    /// </summary>
    public static class TemplatePartNames
    {
        /// <summary>
        /// NodeToolBox 模板部件名称
        /// </summary>
        public static class NodeToolBox
        {
            public const string MainToolbar = "PART_MainToolbar";
            public const string PopupPanel = "PART_PopupPanel";
            public const string ToolPanel = "PART_ToolPanel";
            public const string ToolsScrollViewer = "PART_ToolsScrollViewer";
            public const string ToolsItemsControl = "PART_ToolsItemsControl";
            public const string ToolsUniformGrid = "PART_ToolsUniformGrid";
        }

        /// <summary>
        /// FlowEditor 模板部件名称
        /// </summary>
        public static class FlowEditor
        {
            public const string DragPreview = "DragPreview";
            public const string DragPreviewText = "DragPreviewText";
        }

        /// <summary>
        /// FlowNodeButton 模板部件名称
        /// </summary>
        public static class FlowNodeButton
        {
            public const string NodeTextBox = "NodeTextBox";
        }

        /// <summary>
        /// EmbeddedDialogWindow 模板部件名称
        /// </summary>
        public static class EmbeddedDialogWindow
        {
            public const string HeaderBar = "PART_HeaderBar";
            public const string CloseButton = "PART_CloseButton";
            public const string MinButton = "PART_MinButton";
            public const string MaxButton = "PART_MaxButton";
            public const string ResizeThumb = "PART_ResizeThumb";
        }
    }


    /// <summary>
    /// NodeToolBox 控件常量定义
    /// </summary>
    public static class NodeToolBoxConstants
    {
        /// <summary>
        /// 工具项按钮高度（像素）
        /// </summary>
        public const double ToolItemButtonHeight = 70.0;

        /// <summary>
        /// 工具项按钮间距（像素）
        /// </summary>
        public const double ToolItemButtonSpacing = 4.0;

        /// <summary>
        /// 面板固定高度（标题+分隔符+边距，像素）
        /// </summary>
        public const double PanelFixedHeight = 80.0;

        /// <summary>
        /// 面板最小边距（像素）
        /// </summary>
        public const double PanelMinMargin = 10.0;

        /// <summary>
        /// 面板顶部边距（像素）
        /// </summary>
        public const double PanelTopMargin = 20.0;

        /// <summary>
        /// 工具项图标尺寸（像素）
        /// </summary>
        public const double ToolItemIconSize = 24.0;

        /// <summary>
        /// 默认面板列数
        /// </summary>
        public const int DefaultPanelColumns = 3;

        /// <summary>
        /// 默认面板隐藏延迟（毫秒）
        /// </summary>
        public const int DefaultPanelHideDelay = 300;

        /// <summary>
        /// 默认面板最大高度（像素）
        /// </summary>
        public const double DefaultPanelMaxHeight = 400.0;

        /// <summary>
        /// 默认工具区域最大高度（像素）
        /// </summary>
        public const double DefaultToolsMaxHeight = 300.0;

        /// <summary>
        /// 默认面板水平偏移（像素）
        /// </summary>
        public const double DefaultPanelOffsetX = 2.0;

        /// <summary>
        /// 默认面板垂直偏移（像素）
        /// </summary>
        public const double DefaultPanelOffsetY = 0.0;
    }

    /// <summary>
    /// 节点工具箱控件 - 支持工具类别和工具项的展示、选择和拖拽
    /// 通过接口抽象实现，支持多种类型的工具类别和工具项
    /// </summary>
    [TemplatePart(Name = TemplatePartNames.NodeToolBox.MainToolbar, Type = typeof(Border))]
    [TemplatePart(Name = TemplatePartNames.NodeToolBox.PopupPanel, Type = typeof(Popup))]
    [TemplatePart(Name = TemplatePartNames.NodeToolBox.ToolPanel, Type = typeof(Border))]
    [TemplatePart(Name = TemplatePartNames.NodeToolBox.ToolsScrollViewer, Type = typeof(ScrollViewer))]
    [TemplatePart(Name = TemplatePartNames.NodeToolBox.ToolsItemsControl, Type = typeof(ItemsControl))]
    public class NodeToolBox : Control
    {
        #region 静态构造函数

        static NodeToolBox()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(NodeToolBox), new FrameworkPropertyMetadata(typeof(NodeToolBox)));
        }

        #endregion

        #region 依赖属性定义

        #region 核心属性

        /// <summary>
        /// 工具类别集合
        /// </summary>
        public static readonly DependencyProperty ItemsSourceProperty =
            DependencyProperty.Register(
                nameof(ItemsSource),
                typeof(IEnumerable),
                typeof(NodeToolBox),
                new PropertyMetadata(null, OnItemsSourceChanged));

        /// <summary>
        /// 当前选中的类别
        /// </summary>
        public static readonly DependencyProperty CurrentCategoryProperty =
            DependencyProperty.Register(
                nameof(CurrentCategory),
                typeof(object),
                typeof(NodeToolBox),
                new PropertyMetadata(null, OnCurrentCategoryChanged));

        /// <summary>
        /// 工具面板是否可见
        /// </summary>
        public static readonly DependencyProperty IsPanelVisibleProperty =
            DependencyProperty.Register(
                nameof(IsPanelVisible),
                typeof(bool),
                typeof(NodeToolBox),
                new PropertyMetadata(false));

        #endregion

        #region 布局配置属性

        /// <summary>
        /// 面板水平偏移量
        /// </summary>
        public static readonly DependencyProperty PanelOffsetXProperty =
            DependencyProperty.Register(
                nameof(PanelOffsetX),
                typeof(double),
                typeof(NodeToolBox),
                new PropertyMetadata(NodeToolBoxConstants.DefaultPanelOffsetX));

        /// <summary>
        /// 面板垂直偏移量
        /// </summary>
        public static readonly DependencyProperty PanelOffsetYProperty =
            DependencyProperty.Register(
                nameof(PanelOffsetY),
                typeof(double),
                typeof(NodeToolBox),
                new PropertyMetadata(NodeToolBoxConstants.DefaultPanelOffsetY));

        /// <summary>
        /// 面板最大高度
        /// </summary>
        public static readonly DependencyProperty PanelMaxHeightProperty =
            DependencyProperty.Register(
                nameof(PanelMaxHeight),
                typeof(double),
                typeof(NodeToolBox),
                new PropertyMetadata(NodeToolBoxConstants.DefaultPanelMaxHeight));

        /// <summary>
        /// 工具区域最大高度
        /// </summary>
        public static readonly DependencyProperty ToolsMaxHeightProperty =
            DependencyProperty.Register(
                nameof(ToolsMaxHeight),
                typeof(double),
                typeof(NodeToolBox),
                new PropertyMetadata(NodeToolBoxConstants.DefaultToolsMaxHeight));

        /// <summary>
        /// 工具面板列数
        /// </summary>
        public static readonly DependencyProperty PanelColumnsProperty =
            DependencyProperty.Register(
                nameof(PanelColumns),
                typeof(int),
                typeof(NodeToolBox),
                new PropertyMetadata(NodeToolBoxConstants.DefaultPanelColumns, OnPanelColumnsChanged));

        /// <summary>
        /// 工具栏对齐方式
        /// </summary>
        public static readonly DependencyProperty ToolbarAlignmentProperty =
            DependencyProperty.Register(
                nameof(ToolbarAlignment),
                typeof(System.Windows.VerticalAlignment),
                typeof(NodeToolBox),
                new PropertyMetadata(System.Windows.VerticalAlignment.Center));

        /// <summary>
        /// 面板隐藏延迟时间（毫秒）
        /// </summary>
        public static readonly DependencyProperty PanelHideDelayProperty =
            DependencyProperty.Register(
                nameof(PanelHideDelay),
                typeof(int),
                typeof(NodeToolBox),
                new PropertyMetadata(NodeToolBoxConstants.DefaultPanelHideDelay));

        #endregion

        #region 命令属性

        /// <summary>
        /// 类别悬停命令
        /// </summary>
        public static readonly DependencyProperty CategoryHoverCommandProperty =
            DependencyProperty.Register(
                nameof(CategoryHoverCommand),
                typeof(ICommand),
                typeof(NodeToolBox),
                new PropertyMetadata(null));

        /// <summary>
        /// 工具项点击命令
        /// </summary>
        public static readonly DependencyProperty ToolClickCommandProperty =
            DependencyProperty.Register(
                nameof(ToolClickCommand),
                typeof(ICommand),
                typeof(NodeToolBox),
                new PropertyMetadata(null));

        /// <summary>
        /// 工具项拖拽命令
        /// </summary>
        public static readonly DependencyProperty ToolDragCommandProperty =
            DependencyProperty.Register(
                nameof(ToolDragCommand),
                typeof(ICommand),
                typeof(NodeToolBox),
                new PropertyMetadata(null));

        /// <summary>
        /// 拖拽数据构建器
        /// </summary>
        public static readonly DependencyProperty DragDropPayloadBuilderProperty =
            DependencyProperty.Register(
                nameof(DragDropPayloadBuilder),
                typeof(IDragDropPayloadBuilder),
                typeof(NodeToolBox),
                new PropertyMetadata(null));

        #endregion

        #endregion

        #region 属性访问器

        /// <summary>
        /// 工具类别集合（支持任何实现 IToolCategory 接口的类型）
        /// </summary>
        public IEnumerable ItemsSource
        {
            get => (IEnumerable)GetValue(ItemsSourceProperty);
            set => SetValue(ItemsSourceProperty, value);
        }

        /// <summary>
        /// 当前选中的类别（支持任何实现 IToolCategory 接口的类型）
        /// </summary>
        public object CurrentCategory
        {
            get => GetValue(CurrentCategoryProperty);
            set => SetValue(CurrentCategoryProperty, value);
        }

        /// <summary>
        /// 工具面板是否可见
        /// </summary>
        public bool IsPanelVisible
        {
            get => (bool)GetValue(IsPanelVisibleProperty);
            set => SetValue(IsPanelVisibleProperty, value);
        }

        /// <summary>
        /// 面板水平偏移量
        /// </summary>
        public double PanelOffsetX
        {
            get => (double)GetValue(PanelOffsetXProperty);
            set => SetValue(PanelOffsetXProperty, value);
        }

        /// <summary>
        /// 面板垂直偏移量
        /// </summary>
        public double PanelOffsetY
        {
            get => (double)GetValue(PanelOffsetYProperty);
            set => SetValue(PanelOffsetYProperty, value);
        }

        /// <summary>
        /// 面板最大高度
        /// </summary>
        public double PanelMaxHeight
        {
            get => (double)GetValue(PanelMaxHeightProperty);
            set => SetValue(PanelMaxHeightProperty, value);
        }

        /// <summary>
        /// 工具区域最大高度
        /// </summary>
        public double ToolsMaxHeight
        {
            get => (double)GetValue(ToolsMaxHeightProperty);
            set => SetValue(ToolsMaxHeightProperty, value);
        }

        /// <summary>
        /// 工具面板列数
        /// </summary>
        public int PanelColumns
        {
            get => (int)GetValue(PanelColumnsProperty);
            set
            {
                SetValue(PanelColumnsProperty, value);
                UpdateUniformGridColumns();
            }
        }

        /// <summary>
        /// 工具栏对齐方式
        /// </summary>
        public System.Windows.VerticalAlignment ToolbarAlignment
        {
            get => (System.Windows.VerticalAlignment)GetValue(ToolbarAlignmentProperty);
            set => SetValue(ToolbarAlignmentProperty, value);
        }

        /// <summary>
        /// 面板隐藏延迟时间（毫秒）
        /// </summary>
        public int PanelHideDelay
        {
            get => (int)GetValue(PanelHideDelayProperty);
            set => SetValue(PanelHideDelayProperty, value);
        }

        /// <summary>
        /// 类别悬停命令
        /// </summary>
        public ICommand CategoryHoverCommand
        {
            get => (ICommand)GetValue(CategoryHoverCommandProperty);
            set => SetValue(CategoryHoverCommandProperty, value);
        }

        /// <summary>
        /// 工具项点击命令
        /// </summary>
        public ICommand ToolClickCommand
        {
            get => (ICommand)GetValue(ToolClickCommandProperty);
            set => SetValue(ToolClickCommandProperty, value);
        }

        /// <summary>
        /// 工具项拖拽命令
        /// </summary>
        public ICommand ToolDragCommand
        {
            get => (ICommand)GetValue(ToolDragCommandProperty);
            set => SetValue(ToolDragCommandProperty, value);
        }

        /// <summary>
        /// 拖拽数据构建器
        /// </summary>
        public IDragDropPayloadBuilder DragDropPayloadBuilder
        {
            get => (IDragDropPayloadBuilder)GetValue(DragDropPayloadBuilderProperty);
            set => SetValue(DragDropPayloadBuilderProperty, value);
        }

        #endregion

        #region 事件定义

        /// <summary>
        /// 类别选择事件
        /// </summary>
        public event EventHandler<ToolCategorySelectedEventArgs> CategorySelected;

        /// <summary>
        /// 工具项选择事件
        /// </summary>
        public event EventHandler<ToolItemSelectedEventArgs> ToolSelected;

        /// <summary>
        /// 工具项拖拽开始事件 - 在拖拽操作开始时触发，允许外部系统准备创建对象或验证拖拽
        /// </summary>
        public event EventHandler<ToolItemDragEventArgs> ToolItemDragStarted;

        /// <summary>
        /// 工具项拖拽完成事件 - 在拖拽操作完成时触发（无论成功或失败）
        /// </summary>
        public event EventHandler<ToolItemDragCompletedEventArgs> ToolItemDragCompleted;

        #endregion

        #region 私有字段

        private bool _isLoaded;
        private Button _lastHoveredButton;
        private DispatcherTimer _hideDelayTimer;
        private IEnumerable _boundItemsSource;
        private INotifyCollectionChanged _boundItemsSourceNotify;
        private Point _dragStartPoint;
        private bool _isDraggingFromToolbox;

        // 模板元素引用
        private Border _mainToolbar;
        private Popup _popupPanel;
        private Border _toolPanel;
        private ScrollViewer _toolsScrollViewer;
        private ItemsControl _toolsItemsControl;
        private UniformGrid _toolsUniformGrid;

        // 事件订阅管理
        private ItemsControl _toolbarItemsControl;
        private ItemsControl _toolItemsControl;

        #endregion

        #region 构造函数

        /// <summary>
        /// 初始化 NodeToolBox 控件
        /// </summary>
        public NodeToolBox()
        {
            InitializeHideTimer();
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        #endregion

        #region 模板应用

        /// <summary>
        /// 应用控件模板
        /// </summary>
        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            // 取消之前的事件订阅
            UnsubscribeFromTemplateEvents();

            // 获取模板元素
            GetTemplateParts();

            // 初始化 UniformGrid
            InitializeUniformGrid();

            // 订阅模板事件
            SubscribeToTemplateEvents();
            
            // 初始化面板裁剪
            InitializePanelClip();
        }

        /// <summary>
        /// 获取模板部件
        /// </summary>
        private void GetTemplateParts()
        {
            _mainToolbar = GetTemplateChild(TemplatePartNames.NodeToolBox.MainToolbar) as Border;
            _popupPanel = GetTemplateChild(TemplatePartNames.NodeToolBox.PopupPanel) as Popup;
            _toolPanel = GetTemplateChild(TemplatePartNames.NodeToolBox.ToolPanel) as Border;
            _toolsScrollViewer = GetTemplateChild(TemplatePartNames.NodeToolBox.ToolsScrollViewer) as ScrollViewer;
            _toolsItemsControl = GetTemplateChild(TemplatePartNames.NodeToolBox.ToolsItemsControl) as ItemsControl;
        }

        /// <summary>
        /// 初始化 UniformGrid
        /// </summary>
        private void InitializeUniformGrid()
        {
            if (_toolsItemsControl != null)
            {
                _toolsItemsControl.Loaded += (s, e) =>
                {
                    _toolsUniformGrid = FindVisualChild<UniformGrid>(_toolsItemsControl);
                    UpdateUniformGridColumns();
                };
            }
        }

        /// <summary>
        /// 更新 UniformGrid 列数
        /// </summary>
        private void UpdateUniformGridColumns()
        {
            if (_toolsUniformGrid != null)
            {
                _toolsUniformGrid.Columns = PanelColumns;
            }
        }

        #endregion

        #region 事件订阅管理

        /// <summary>
        /// 订阅模板事件
        /// </summary>
        private void SubscribeToTemplateEvents()
        {
            SubscribeToToolbarEvents();
            SubscribeToToolPanelEvents();
        }

        /// <summary>
        /// 订阅工具栏事件
        /// </summary>
        private void SubscribeToToolbarEvents()
        {
            if (_mainToolbar == null) return;

            _toolbarItemsControl = FindVisualChild<ItemsControl>(_mainToolbar);
            if (_toolbarItemsControl == null) return;

            _toolbarItemsControl.ItemContainerGenerator.StatusChanged += OnToolbarItemsControlStatusChanged;

            if (_toolbarItemsControl.IsLoaded)
            {
                SubscribeToToolbarButtons(_toolbarItemsControl);
            }
            else
            {
                _toolbarItemsControl.Loaded += (s, e) => SubscribeToToolbarButtons(_toolbarItemsControl);
            }
        }

        /// <summary>
        /// 订阅工具面板事件
        /// </summary>
        private void SubscribeToToolPanelEvents()
        {
            if (_toolPanel == null) return;

            _toolPanel.MouseLeave += OnToolPanelMouseLeave;

            _toolItemsControl = FindVisualChild<ItemsControl>(_toolPanel);
            if (_toolItemsControl == null) return;

            _toolItemsControl.ItemContainerGenerator.StatusChanged += OnToolItemsControlStatusChanged;

            if (_toolItemsControl.IsLoaded)
            {
                SubscribeToToolItemButtons(_toolItemsControl);
            }
            else
            {
                _toolItemsControl.Loaded += (s, e) => SubscribeToToolItemButtons(_toolItemsControl);
            }
        }

        /// <summary>
        /// 取消模板事件订阅
        /// </summary>
        private void UnsubscribeFromTemplateEvents()
        {
            if (_toolPanel != null)
            {
                _toolPanel.MouseLeave -= OnToolPanelMouseLeave;
                _toolPanel.SizeChanged -= OnToolPanelSizeChanged;
                _toolPanel.Loaded -= OnToolPanelLoaded;
            }

            if (_toolItemsControl != null)
            {
                _toolItemsControl.ItemContainerGenerator.StatusChanged -= OnToolItemsControlStatusChanged;
            }

            if (_toolbarItemsControl != null)
            {
                _toolbarItemsControl.ItemContainerGenerator.StatusChanged -= OnToolbarItemsControlStatusChanged;
            }
        }

        /// <summary>
        /// 工具栏容器生成状态变化处理
        /// </summary>
        private void OnToolbarItemsControlStatusChanged(object sender, EventArgs e)
        {
            var generator = sender as ItemContainerGenerator;
            if (generator?.Status != GeneratorStatus.ContainersGenerated) return;

            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
            {
                if (_toolbarItemsControl != null)
                {
                    SubscribeToToolbarButtons(_toolbarItemsControl);
                }
            }));
        }

        /// <summary>
        /// 工具项容器生成状态变化处理
        /// </summary>
        private void OnToolItemsControlStatusChanged(object sender, EventArgs e)
        {
            var generator = sender as ItemContainerGenerator;
            if (generator?.Status != GeneratorStatus.ContainersGenerated) return;

            if (_toolItemsControl != null)
            {
                SubscribeToToolItemButtons(_toolItemsControl);
            }
        }

        /// <summary>
        /// 订阅工具栏按钮事件
        /// </summary>
        private void SubscribeToToolbarButtons(ItemsControl itemsControl)
        {
            if (itemsControl == null) return;

            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
            {
                try
                {
                    if (itemsControl.ItemContainerGenerator.Status != GeneratorStatus.ContainersGenerated)
                    {
                        return;
                    }

                    int subscribedCount = 0;
                    for (int i = 0; i < itemsControl.Items.Count; i++)
                    {
                        try
                        {
                            var container = itemsControl.ItemContainerGenerator.ContainerFromIndex(i);
                            var button = FindVisualChild<Button>(container);
                            if (button != null)
                            {
                                // 先取消订阅，避免重复订阅
                                button.MouseEnter -= OnToolButtonMouseEnter;
                                button.MouseLeave -= OnToolButtonMouseLeave;

                                // 订阅事件
                                button.MouseEnter += OnToolButtonMouseEnter;
                                button.MouseLeave += OnToolButtonMouseLeave;
                                subscribedCount++;
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"订阅工具栏按钮事件时出错 (索引 {i}): {ex.Message}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"订阅工具栏按钮事件时出错: {ex.Message}");
                }
            }));
        }

        /// <summary>
        /// 订阅工具项按钮事件
        /// </summary>
        private void SubscribeToToolItemButtons(ItemsControl itemsControl)
        {
            if (itemsControl == null) return;

            for (int i = 0; i < itemsControl.Items.Count; i++)
            {
                if (itemsControl.ItemContainerGenerator.Status != GeneratorStatus.ContainersGenerated)
                    continue;

                var container = itemsControl.ItemContainerGenerator.ContainerFromIndex(i);
                var button = FindVisualChild<Button>(container);
                if (button == null) continue;

                // 取消之前的订阅
                button.Click -= OnToolItemButtonClick;
                button.PreviewMouseLeftButtonDown -= OnToolItemPreviewMouseLeftButtonDown;
                button.MouseMove -= OnToolItemMouseMove;
                button.PreviewMouseMove -= OnToolItemPreviewMouseMove;

                // 订阅事件
                button.Click += OnToolItemButtonClick;
                button.PreviewMouseLeftButtonDown += OnToolItemPreviewMouseLeftButtonDown;
                button.MouseMove += OnToolItemMouseMove;
                button.PreviewMouseMove += OnToolItemPreviewMouseMove;
            }
        }

        #endregion

        #region 依赖属性变更处理

        /// <summary>
        /// ItemsSource 属性变更处理
        /// </summary>
        private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (NodeToolBox)d;
            control.ApplyItemsSource(e.NewValue as IEnumerable);
        }

        /// <summary>
        /// CurrentCategory 属性变更处理
        /// </summary>
        private static void OnCurrentCategoryChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (NodeToolBox)d;
            control.UpdateSelectionFlags();
        }

        /// <summary>
        /// PanelColumns 属性变更处理
        /// </summary>
        private static void OnPanelColumnsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (NodeToolBox)d;
            control.UpdateUniformGridColumns();
        }

        /// <summary>
        /// 应用 ItemsSource
        /// </summary>
        private void ApplyItemsSource(IEnumerable categories)
        {
            // 取消之前的订阅
            if (_boundItemsSourceNotify != null)
            {
                _boundItemsSourceNotify.CollectionChanged -= OnExternalItemsSourceChanged;
            }

            _boundItemsSource = categories;
            _boundItemsSourceNotify = categories as INotifyCollectionChanged;

            // 订阅集合变更事件
            if (_boundItemsSourceNotify != null)
            {
                _boundItemsSourceNotify.CollectionChanged += OnExternalItemsSourceChanged;
            }

            // 设置第一个类别为当前类别
            var firstCategory = GetFirstCategory(_boundItemsSource);
            SetCurrentCategory(firstCategory, raiseEvent: false);

            // ItemsSource 变化后，重新订阅按钮事件
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
            {
                if (_toolbarItemsControl != null)
                {
                    SubscribeToToolbarButtons(_toolbarItemsControl);
                }
            }));
        }

        /// <summary>
        /// 外部 ItemsSource 集合变更处理
        /// </summary>
        private void OnExternalItemsSourceChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            var currentCategory = GetCurrentCategoryAsInterface();
            if (currentCategory != null && _boundItemsSource != null && !ContainsCategory(_boundItemsSource, currentCategory))
            {
                var firstCategory = GetFirstCategory(_boundItemsSource);
                SetCurrentCategory(firstCategory, raiseEvent: true);
            }
        }

        #endregion

        #region 面板位置和尺寸管理

        /// <summary>
        /// 更新面板位置（Popup 自动对齐，仅需调整偏移）
        /// </summary>
        private void UpdatePanelPosition(Button targetButton)
        {
            if (targetButton == null || _mainToolbar == null || _popupPanel == null) return;

            try
            {
                // Popup 会自动对齐到 PlacementTarget，只需调整垂直偏移
                var buttonPositionInToolbar = targetButton.TransformToAncestor(_mainToolbar).Transform(new Point(0, 0));

                // 让面板尽量对齐到当前按钮
                PanelOffsetY = buttonPositionInToolbar.Y - NodeToolBoxConstants.PanelMinMargin;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"更新面板位置时发生错误: {ex.Message}");
                PanelOffsetY = 0;
            }
        }

        /// <summary>
        /// 估算面板高度
        /// </summary>
        private double EstimatePanelHeight()
        {
            var category = GetCurrentCategoryAsInterface();
            if (category == null)
                return 200;

            int toolCount = GetToolCount(category);
            int rows = (int)Math.Ceiling((double)toolCount / PanelColumns);

            double toolsHeight = rows * (NodeToolBoxConstants.ToolItemButtonHeight + NodeToolBoxConstants.ToolItemButtonSpacing);

            double totalHeight = NodeToolBoxConstants.PanelFixedHeight + toolsHeight;
            return Math.Min(totalHeight, PanelMaxHeight);
        }

        /// <summary>
        /// 自动调整面板高度
        /// </summary>
        private void AutoAdjustPanelHeight(int toolCount)
        {
            if (toolCount <= 0) return;

            int rows = (int)Math.Ceiling((double)toolCount / PanelColumns);

            double toolsHeight = rows * (NodeToolBoxConstants.ToolItemButtonHeight + NodeToolBoxConstants.ToolItemButtonSpacing);

            if (toolsHeight > PanelMaxHeight - NodeToolBoxConstants.PanelFixedHeight)
            {
                ToolsMaxHeight = PanelMaxHeight - NodeToolBoxConstants.PanelFixedHeight;
            }
            else
            {
                ToolsMaxHeight = toolsHeight + NodeToolBoxConstants.PanelTopMargin;
            }
        }

        #endregion

        #region 延迟隐藏计时器

        /// <summary>
        /// 初始化隐藏延迟计时器
        /// </summary>
        private void InitializeHideTimer()
        {
            _hideDelayTimer = new DispatcherTimer();
            _hideDelayTimer.Interval = TimeSpan.FromMilliseconds(PanelHideDelay);
            _hideDelayTimer.Tick += OnHideDelayTimerTick;
        }

        /// <summary>
        /// 隐藏延迟计时器触发
        /// </summary>
        private void OnHideDelayTimerTick(object sender, EventArgs e)
        {
            _hideDelayTimer.Stop();

            if (!IsMouseInToolboxArea())
            {
                HidePanelAndClearSelection();
            }
        }

        /// <summary>
        /// 启动隐藏延迟
        /// </summary>
        private void StartHideDelay()
        {
            _hideDelayTimer.Stop();
            _hideDelayTimer.Interval = TimeSpan.FromMilliseconds(PanelHideDelay);
            _hideDelayTimer.Start();
        }

        /// <summary>
        /// 取消隐藏延迟
        /// </summary>
        private void CancelHideDelay()
        {
            if (_hideDelayTimer.IsEnabled)
            {
                _hideDelayTimer.Stop();
            }
        }

        #endregion

        #region 鼠标事件处理

        /// <summary>
        /// 工具栏按钮鼠标进入
        /// </summary>
        private void OnToolButtonMouseEnter(object sender, MouseEventArgs e)
        {
            CancelHideDelay();

            if (!(sender is Button button)) return;

            var category = GetCategoryFromDataContext(button.DataContext);
            if (category == null) return;

            _lastHoveredButton = button;
            SetCurrentCategory(button.DataContext, raiseEvent: true);

            int toolCount = GetToolCount(category);
            AutoAdjustPanelHeight(toolCount);

            UpdatePanelPosition(button);
            IsPanelVisible = true;

            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
            {
                _toolsScrollViewer?.ScrollToTop();
            }));
        }

        /// <summary>
        /// 工具栏按钮鼠标离开
        /// </summary>
        private void OnToolButtonMouseLeave(object sender, MouseEventArgs e)
        {
            StartHideDelay();
        }

        /// <summary>
        /// 工具面板鼠标进入
        /// </summary>
        private void OnToolPanelMouseEnter(object sender, MouseEventArgs e)
        {
            CancelHideDelay();
        }

        /// <summary>
        /// 工具面板鼠标离开
        /// </summary>
        private void OnToolPanelMouseLeave(object sender, MouseEventArgs e)
        {
            StartHideDelay();
        }

        /// <summary>
        /// 工具项按钮点击
        /// </summary>
        private void OnToolItemButtonClick(object sender, RoutedEventArgs e)
        {
            if (!(sender is Button btn)) return;

            var tool = GetToolItemFromDataContext(btn.DataContext);
            var category = GetCurrentCategoryAsInterface();
            if (tool == null || category == null) return;

            var categoryName = GetCategoryName(category);
            var toolName = GetToolItemName(tool);

            // 触发事件
            ToolSelected?.Invoke(this, new ToolItemSelectedEventArgs(
                categoryName,
                toolName,
                tool,
                tool));

            // 执行命令
            if (ToolClickCommand?.CanExecute(tool) == true)
            {
                ToolClickCommand.Execute(tool);
            }
        }

        #endregion

        #region 拖拽支持

        /// <summary>
        /// 工具项预览鼠标左键按下
        /// </summary>
        private void OnToolItemPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragStartPoint = e.GetPosition(this);
            _isDraggingFromToolbox = false;
        }

        /// <summary>
        /// 工具项鼠标移动（拖拽处理）
        /// </summary>
        private void OnToolItemMouseMove(object sender, MouseEventArgs e)
        {
            HandleDragOperation(sender, e, false);
        }

        /// <summary>
        /// 工具项预览鼠标移动（拖拽处理）
        /// </summary>
        private void OnToolItemPreviewMouseMove(object sender, MouseEventArgs e)
        {
            HandleDragOperation(sender, e, true);
        }

        /// <summary>
        /// 处理拖拽操作（统一处理拖拽逻辑）
        /// </summary>
        private void HandleDragOperation(object sender, MouseEventArgs e, bool markHandled)
        {
            if (e.LeftButton != MouseButtonState.Pressed) return;
            if (!(sender is Button btn)) return;

            var pos = e.GetPosition(this);
            var diff = pos - _dragStartPoint;
            if (_isDraggingFromToolbox ||
                (Math.Abs(diff.X) <= SystemParameters.MinimumHorizontalDragDistance &&
                 Math.Abs(diff.Y) <= SystemParameters.MinimumVerticalDragDistance))
            {
                return;
            }

            _isDraggingFromToolbox = true;

            var tool = GetToolItemFromDataContext(btn.DataContext);
            var category = GetCurrentCategoryAsInterface();
            if (tool == null || category == null)
            {
                _isDraggingFromToolbox = false;
                return;
            }

            var data = CreateDragDropData(tool, category);
            if (data == null)
            {
                _isDraggingFromToolbox = false;
                return;
            }

            var dragStartArgs = new ToolItemDragEventArgs(
                tool,              
                GetCategoryName(category),
                _dragStartPoint,
                data);

            // 触发拖拽开始事件
            ToolItemDragStarted?.Invoke(this, dragStartArgs);

            // 执行拖拽命令
            if (ToolDragCommand?.CanExecute(tool) == true)
            {
                ToolDragCommand.Execute(tool);
            }

            // 如果事件被取消，不执行拖拽
            if (dragStartArgs.IsCancelled)
            {
                _isDraggingFromToolbox = false;
                return;
            }

            if (markHandled)
            {
                e.Handled = true;
            }

            DragDropEffects result = DragDropEffects.None;
            try
            {
                result = DragDrop.DoDragDrop(btn, data, DragDropEffects.Copy);
            }
            finally
            {
                _isDraggingFromToolbox = false;

                // 触发拖拽完成事件
                var dragCompletedArgs = new ToolItemDragCompletedEventArgs(
                    tool,
                   
                    GetCategoryName(category),
                    result);
                ToolItemDragCompleted?.Invoke(this, dragCompletedArgs);
            }
        }

        /// <summary>
        /// 创建拖拽数据对象（使用构建器模式）
        /// </summary>
        private DataObject CreateDragDropData(IToolItem tool, IToolCategory<IToolItem> category)
        {
            // 使用注入的构建器，如果没有则使用默认实现
            var builder = DragDropPayloadBuilder ?? new DefaultDragDropPayloadBuilder();
            return builder.BuildPayload(tool, category);
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 检查鼠标是否在工具箱区域内
        /// </summary>
        private bool IsMouseInToolboxArea()
        {
            try
            {
                bool inToolbar = _mainToolbar?.IsMouseOver == true;
                bool inPanel = _toolPanel?.IsVisible == true && _toolPanel.IsMouseOver;
                return inToolbar || inPanel;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 隐藏面板并清除选择
        /// </summary>
        private void HidePanelAndClearSelection()
        {
            if (_boundItemsSource != null)
            {
                foreach (var item in _boundItemsSource)
                {
                    var category = GetCategoryFromDataContext(item);
                    if (category != null)
                    {
                        category.IsSelected = false;
                    }
                }
            }

            IsPanelVisible = false;
            _lastHoveredButton = null;
        }

        /// <summary>
        /// 初始化面板裁剪
        /// </summary>
        private void InitializePanelClip()
        {
            // 为主面板添加裁剪
            if (_toolPanel != null)
            {
                _toolPanel.SizeChanged += OnToolPanelSizeChanged;
                _toolPanel.Loaded += OnToolPanelLoaded;
                UpdateToolPanelClip();
            }
        }

        /// <summary>
        /// 主面板尺寸变化处理
        /// </summary>
        private void OnToolPanelSizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateToolPanelClip();
        }

        /// <summary>
        /// 主面板加载处理
        /// </summary>
        private void OnToolPanelLoaded(object sender, RoutedEventArgs e)
        {
            UpdateToolPanelClip();
        }

        /// <summary>
        /// 更新主面板的裁剪区域
        /// </summary>
        private void UpdateToolPanelClip()
        {
            if (_toolPanel == null) return;

            // 等待布局完成
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
            {
                if (_toolPanel.ActualWidth > 0 && _toolPanel.ActualHeight > 0)
                {
                    const double cornerRadius = 14.0;
                    _toolPanel.Clip = new RectangleGeometry
                    {
                        RadiusX = cornerRadius,
                        RadiusY = cornerRadius,
                        Rect = new Rect(0, 0, _toolPanel.ActualWidth, _toolPanel.ActualHeight)
                    };
                }
            }));
        }

        /// <summary>
        /// 查找可视化子元素
        /// </summary>
        private T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null) return null;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T result)
                    return result;

                var descendant = FindVisualChild<T>(child);
                if (descendant != null)
                    return descendant;
            }
            return null;
        }

        #endregion

        #region 接口辅助方法

        /// <summary>
        /// 从数据上下文获取类别接口（强类型版本）
        /// </summary>
        private IToolCategory<IToolItem> GetCategoryFromDataContext(object dataContext)
        {
            if (dataContext == null) return null;

            // 直接尝试转换为接口类型
            if (dataContext is IToolCategory<IToolItem> category)
            {
                return category;
            }

            // 检查是否实现了泛型接口（针对不同的 TItem 类型）
            var type = dataContext.GetType();
            var interfaceType = type.GetInterfaces()
                .FirstOrDefault(i => i.IsGenericType &&
                                    i.GetGenericTypeDefinition() == typeof(IToolCategory<>));

            if (interfaceType != null)
            {
                // 包装成适配器以提供统一接口
                return new ToolCategoryAdapter(dataContext, interfaceType);
            }

            return null;
        }

        /// <summary>
        /// 从数据上下文获取工具项接口
        /// </summary>
        private IToolItem GetToolItemFromDataContext(object dataContext)
        {
            return dataContext as IToolItem;
        }

        /// <summary>
        /// 获取当前类别作为接口（强类型版本）
        /// </summary>
        private IToolCategory<IToolItem> GetCurrentCategoryAsInterface()
        {
            return GetCategoryFromDataContext(CurrentCategory);
        }

        /// <summary>
        /// 获取类别名称（强类型版本）
        /// </summary>
        private string GetCategoryName(IToolCategory<IToolItem> category)
        {
            return category?.Name ?? string.Empty;
        }

        /// <summary>
        /// 获取工具项名称
        /// </summary>
        private string GetToolItemName(IToolItem tool)
        {
            return tool?.Name ?? string.Empty;
        }

        /// <summary>
        /// 获取工具数量（强类型版本）
        /// </summary>
        private int GetToolCount(IToolCategory<IToolItem> category)
        {
            if (category?.Tools == null) return 0;
            return category.Tools.Count;
        }

        /// <summary>
        /// 获取第一个类别
        /// </summary>
        private object GetFirstCategory(IEnumerable itemsSource)
        {
            if (itemsSource == null) return null;

            foreach (var item in itemsSource)
            {
                var category = GetCategoryFromDataContext(item);
                if (category != null)
                {
                    return item;
                }
            }
            return null;
        }

        /// <summary>
        /// 检查集合中是否包含指定类别（强类型版本）
        /// </summary>
        private bool ContainsCategory(IEnumerable itemsSource, IToolCategory<IToolItem> category)
        {
            if (itemsSource == null || category == null) return false;

            foreach (var item in itemsSource)
            {
                var itemCategory = GetCategoryFromDataContext(item);
                if (itemCategory == null) continue;

                // 比较适配器的源对象或直接比较
                var itemSource = (itemCategory as ToolCategoryAdapter)?.GetSourceCategory() ?? itemCategory;
                var categorySource = (category as ToolCategoryAdapter)?.GetSourceCategory() ?? category;

                if (ReferenceEquals(itemSource, categorySource))
                {
                    return true;
                }
            }
            return false;
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 设置面板距离
        /// </summary>
        /// <param name="distance">距离值</param>
        public void SetPanelDistance(double distance)
        {
            PanelOffsetX = Math.Max(0, distance);
        }

        /// <summary>
        /// 设置隐藏延迟
        /// </summary>
        /// <param name="delayMs">延迟时间（毫秒）</param>
        public void SetHideDelay(int delayMs)
        {
            PanelHideDelay = Math.Max(0, Math.Min(delayMs, 1000));
        }

        /// <summary>
        /// 立即隐藏面板
        /// </summary>
        public void HideImmediately()
        {
            CancelHideDelay();
            HidePanelAndClearSelection();
        }

        #endregion

        #region 生命周期管理

        /// <summary>
        /// 确保初始选择
        /// </summary>
        private void EnsureInitialSelection()
        {
            if (CurrentCategory == null && _boundItemsSource != null)
            {
                var firstCategory = GetFirstCategory(_boundItemsSource);
                SetCurrentCategory(firstCategory, raiseEvent: false);
            }
        }

        /// <summary>
        /// 控件加载事件处理
        /// </summary>
        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (_isLoaded) return;

            _isLoaded = true;
            Focusable = true;
            EnsureInitialSelection();

            // 确保在加载后重新订阅事件
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
            {
                if (_toolbarItemsControl != null)
                {
                    SubscribeToToolbarButtons(_toolbarItemsControl);
                }
            }));
        }

        /// <summary>
        /// 控件卸载事件处理
        /// </summary>
        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            _isLoaded = false;
            _lastHoveredButton = null;

            _hideDelayTimer?.Stop();
            _hideDelayTimer = null;

            if (_boundItemsSourceNotify != null)
            {
                _boundItemsSourceNotify.CollectionChanged -= OnExternalItemsSourceChanged;
                _boundItemsSourceNotify = null;
            }
            _boundItemsSource = null;
        }

        #endregion

        #region 私有方法

        /// <summary>
        /// 设置当前类别
        /// </summary>
        /// <param name="category">类别对象</param>
        /// <param name="raiseEvent">是否触发事件</param>
        private void SetCurrentCategory(object category, bool raiseEvent)
        {
            // 更新所有类别的选中状态
            if (_boundItemsSource != null)
            {
                foreach (var item in _boundItemsSource)
                {
                    var cat = GetCategoryFromDataContext(item);
                    if (cat != null)
                    {
                        cat.IsSelected = ReferenceEquals(item, category);
                    }
                }
            }

            CurrentCategory = category;
            if (raiseEvent && category != null)
            {
                var categoryInterface = GetCategoryFromDataContext(category);
                if (categoryInterface != null)
                {
                    string categoryName = GetCategoryName(categoryInterface);
                    CategorySelected?.Invoke(this, new ToolCategorySelectedEventArgs(
                        categoryName,
                        category));

                    // 执行命令
                    if (CategoryHoverCommand?.CanExecute(categoryInterface) == true)
                    {
                        CategoryHoverCommand.Execute(categoryInterface);
                    }
                }
            }
        }

        /// <summary>
        /// 更新选择标志
        /// </summary>
        private void UpdateSelectionFlags()
        {
            if (_boundItemsSource == null) return;

            var currentCategory = GetCurrentCategoryAsInterface();
            var currentSource = (currentCategory as ToolCategoryAdapter)?.GetSourceCategory() ?? currentCategory;

            foreach (var item in _boundItemsSource)
            {
                var category = GetCategoryFromDataContext(item);
                if (category == null) continue;

                var itemSource = (category as ToolCategoryAdapter)?.GetSourceCategory() ?? category;
                category.IsSelected = ReferenceEquals(itemSource, currentSource) || ReferenceEquals(item, CurrentCategory);
            }
        }

        #endregion
    }
}
