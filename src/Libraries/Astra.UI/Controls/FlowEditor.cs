using Astra.UI.Services;
using Astra.Core.Nodes.Models;
using Astra.Core.Nodes.Geometry;
using Astra.UI.Commands;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Drawing;
using System.Windows.Media;
using CommandManager = Astra.UI.Commands.CommandManager;

namespace Astra.UI.Controls
{
    /// <summary>
    /// 流程编辑器自定义控件 - 支持从工具箱拖拽节点到画布
    /// 
    /// 节点创建机制：
    /// 1. 从工具箱拖拽工具项到画布时，会根据 IToolItem.NodeType 自动创建节点实例
    /// 2. IToolItem.NodeType 可以是 Type 对象或类型名称字符串（完整类型名）
    /// 3. 节点类型必须是 Node 的子类，且必须有公共无参构造函数
    /// 4. 创建的节点会自动设置 Name、NodeType、Position、Description 等属性
    /// 5. 画布数据源（CanvasItemsSource）中的所有对象必须是 Node 的子类
    /// </summary>
    [TemplatePart(Name = "PART_NodeToolBox", Type = typeof(NodeToolBox))]
    [TemplatePart(Name = "PART_InfiniteCanvas", Type = typeof(InfiniteCanvas))]
    public class FlowEditor : Control
    {
        #region 模板部件名称常量

        private const string PART_NodeToolBox = "PART_NodeToolBox";
        private const string PART_InfiniteCanvas = "PART_InfiniteCanvas";

        #endregion

        #region 静态构造函数

        static FlowEditor()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(FlowEditor), new FrameworkPropertyMetadata(typeof(FlowEditor)));
        }

        #endregion

        #region 依赖属性

        /// <summary>
        /// 工具箱数据源 - 工具类别集合
        /// </summary>
        public static readonly DependencyProperty ToolBoxItemsSourceProperty =
            DependencyProperty.Register(
                nameof(ToolBoxItemsSource),
                typeof(IEnumerable),
                typeof(FlowEditor),
                new PropertyMetadata(null, OnToolBoxItemsSourceChanged));

        /// <summary>
        /// 画布数据源 - 节点集合
        /// </summary>
        public static readonly DependencyProperty CanvasItemsSourceProperty =
            DependencyProperty.Register(
                nameof(CanvasItemsSource),
                typeof(IEnumerable),
                typeof(FlowEditor),
                new PropertyMetadata(null, OnCanvasItemsSourceChanged));

        /// <summary>
        /// 连线数据源
        /// </summary>
        public static readonly DependencyProperty EdgeItemsSourceProperty =
            DependencyProperty.Register(
                nameof(EdgeItemsSource),
                typeof(IEnumerable),
                typeof(FlowEditor),
                new PropertyMetadata(null, OnEdgeItemsSourceChanged));

        /// <summary>
        /// 工具箱宽度
        /// </summary>
        public static readonly DependencyProperty ToolBoxWidthProperty =
            DependencyProperty.Register(
                nameof(ToolBoxWidth),
                typeof(double),
                typeof(FlowEditor),
                new PropertyMetadata(80.0));

        /// <summary>
        /// 节点模板选择器 - 用于根据节点类型选择不同的 DataTemplate
        /// </summary>
        public static readonly DependencyProperty ItemTemplateSelectorProperty =
            DependencyProperty.Register(
                nameof(ItemTemplateSelector),
                typeof(System.Windows.Controls.DataTemplateSelector),
                typeof(FlowEditor),
                new PropertyMetadata(null, OnItemTemplateSelectorChanged));

        /// <summary>
        /// 是否显示工具箱
        /// </summary>
        public static readonly DependencyProperty IsToolBoxVisibleProperty =
            DependencyProperty.Register(
                nameof(IsToolBoxVisible),
                typeof(bool),
                typeof(FlowEditor),
                new PropertyMetadata(true, OnIsToolBoxVisibleChanged));

        /// <summary>
        /// 共享剪贴板：存储复制的节点（支持跨流程复制粘贴）
        /// </summary>
        public static readonly DependencyProperty SharedClipboardNodesProperty =
            DependencyProperty.Register(
                nameof(SharedClipboardNodes),
                typeof(List<Node>),
                typeof(FlowEditor),
                new PropertyMetadata(null));

        /// <summary>
        /// 共享剪贴板：存储复制的连线（支持跨流程复制粘贴）
        /// </summary>
        public static readonly DependencyProperty SharedClipboardEdgesProperty =
            DependencyProperty.Register(
                nameof(SharedClipboardEdges),
                typeof(List<Edge>),
                typeof(FlowEditor),
                new PropertyMetadata(null));

        /// <summary>
        /// 共享剪贴板：存储复制节点的边界框（用于保持粘贴时的相对位置）
        /// </summary>
        public static readonly DependencyProperty SharedClipboardBoundsProperty =
            DependencyProperty.Register(
                nameof(SharedClipboardBounds),
                typeof(Rect),
                typeof(FlowEditor),
                new PropertyMetadata(Rect.Empty));

        #endregion

        #region 属性访问器

        /// <summary>
        /// 工具箱数据源
        /// </summary>
        public IEnumerable ToolBoxItemsSource
        {
            get => (IEnumerable)GetValue(ToolBoxItemsSourceProperty);
            set => SetValue(ToolBoxItemsSourceProperty, value);
        }

        /// <summary>
        /// 画布数据源
        /// </summary>
        public IEnumerable CanvasItemsSource
        {
            get => (IEnumerable)GetValue(CanvasItemsSourceProperty);
            set => SetValue(CanvasItemsSourceProperty, value);
        }

        /// <summary>
        /// 连线数据源
        /// </summary>
        public IEnumerable EdgeItemsSource
        {
            get => (IEnumerable)GetValue(EdgeItemsSourceProperty);
            set => SetValue(EdgeItemsSourceProperty, value);
        }

        /// <summary>
        /// 工具箱宽度
        /// </summary>
        public double ToolBoxWidth
        {
            get => (double)GetValue(ToolBoxWidthProperty);
            set => SetValue(ToolBoxWidthProperty, value);
        }

        /// <summary>
        /// 节点模板选择器 - 用于根据节点类型选择不同的 DataTemplate
        /// </summary>
        public System.Windows.Controls.DataTemplateSelector ItemTemplateSelector
        {
            get => (System.Windows.Controls.DataTemplateSelector)GetValue(ItemTemplateSelectorProperty);
            set => SetValue(ItemTemplateSelectorProperty, value);
        }

        /// <summary>
        /// 是否显示工具箱
        /// </summary>
        public bool IsToolBoxVisible
        {
            get => (bool)GetValue(IsToolBoxVisibleProperty);
            set => SetValue(IsToolBoxVisibleProperty, value);
        }

        /// <summary>
        /// 共享剪贴板：存储复制的节点（支持跨流程复制粘贴）
        /// </summary>
        public List<Node> SharedClipboardNodes
        {
            get => (List<Node>)GetValue(SharedClipboardNodesProperty);
            set => SetValue(SharedClipboardNodesProperty, value);
        }

        /// <summary>
        /// 共享剪贴板：存储复制的连线（支持跨流程复制粘贴）
        /// </summary>
        public List<Edge> SharedClipboardEdges
        {
            get => (List<Edge>)GetValue(SharedClipboardEdgesProperty);
            set => SetValue(SharedClipboardEdgesProperty, value);
        }

        /// <summary>
        /// 共享剪贴板：存储复制节点的边界框（用于保持粘贴时的相对位置）
        /// </summary>
        public Rect SharedClipboardBounds
        {
            get => (Rect)GetValue(SharedClipboardBoundsProperty);
            set => SetValue(SharedClipboardBoundsProperty, value);
        }

        /// <summary>
        /// 是否可撤销/重做（连线相关）
        /// </summary>
        public bool CanUndo => _undoRedoManager.CanUndo;
        public bool CanRedo => _undoRedoManager.CanRedo;

        public void Undo() => _undoRedoManager.Undo();
        public void Redo() => _undoRedoManager.Redo();

        /// <summary>
        /// 命令管理器（用于撤销/重做）
        /// </summary>
        public CommandManager CommandManager
        {
            get => _undoRedoManager;
            set
            {
                if (_undoRedoManager != value)
                {
                    _undoRedoManager = value;
                    // 更新 InfiniteCanvas 的 CommandManager
                    if (_infiniteCanvas != null)
                    {
                        _infiniteCanvas.UndoRedoManager = _undoRedoManager;
                    }
                }
            }
        }

        #endregion

        #region 私有字段

        private NodeToolBox _nodeToolBox;
        private InfiniteCanvas _infiniteCanvas;
        private Border _toolBoxSeparator;  // 工具箱分割线
        private ContextMenu _canvasContextMenu;  // 选中项右键菜单（节点或框选组）
        private Window _hostWindow;
        private bool _windowEventsAttached;
        private CommandManager _undoRedoManager = new CommandManager();
        private System.Windows.Point? _lastRightClickPosition;  // 保存最后一次右键点击的位置（画布坐标系）

        #endregion

        #region 构造函数

        public FlowEditor()
        {
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
            
            // 确保控件可以获取键盘焦点
            Focusable = true;
            
            // 订阅键盘事件（支持 Delete 键删除）
            KeyDown += OnFlowEditorKeyDown;

            // 允许整个控件范围接受拖放，避免靠近工具箱边缘时事件未命中画布
            AllowDrop = true;
            PreviewDragOver += OnFlowEditorPreviewDragOver;
            PreviewDrop += OnFlowEditorPreviewDrop;
        }

        #endregion

        #region 模板应用

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            // 取消之前的事件订阅
            UnsubscribeFromEvents();

            // 获取模板部件
            _nodeToolBox = GetTemplateChild(PART_NodeToolBox) as NodeToolBox;
            _infiniteCanvas = GetTemplateChild(PART_InfiniteCanvas) as InfiniteCanvas;
            
            // 查找分割线（在 Grid.Column="1" 的 Border）
            if (_infiniteCanvas != null)
            {
                var parent = VisualTreeHelper.GetParent(_infiniteCanvas) as FrameworkElement;
                if (parent is Grid grid && grid.Children.Count > 1)
                {
                    // 分割线应该在 Grid.Column="1" 的位置
                    foreach (var child in grid.Children)
                    {
                        if (child is Border border && Grid.GetColumn(border) == 1)
                        {
                            _toolBoxSeparator = border;
                            break;
                        }
                    }
                }
            }

            // 订阅事件
            SubscribeToEvents();

            // 应用数据源
            ApplyDataSources();
            
            // 应用工具箱可见性
            ApplyToolBoxVisibility();
        }

        #endregion

        #region 事件订阅

        private void SubscribeToEvents()
        {
            if (_infiniteCanvas != null)
            {
                // 确保 InfiniteCanvas 的拖放状态与 FlowEditor 一致
                _infiniteCanvas.AllowDrop = AllowDrop;
                _infiniteCanvas.IsHitTestVisible = true;
                
                // 订阅拖放事件（使用预览事件确保能捕获到）
                _infiniteCanvas.PreviewDragOver += OnCanvasDragOver;
                _infiniteCanvas.PreviewDrop += OnCanvasDrop;
                _infiniteCanvas.PreviewDragEnter += OnCanvasDragEnter;
                _infiniteCanvas.PreviewDragLeave += OnCanvasDragLeave;
                
                // 同时订阅普通事件（作为备用）
                _infiniteCanvas.DragOver += OnCanvasDragOver;
                _infiniteCanvas.Drop += OnCanvasDrop;
                _infiniteCanvas.DragEnter += OnCanvasDragEnter;
                _infiniteCanvas.DragLeave += OnCanvasDragLeave;
                
                // 订阅右键菜单事件
                _infiniteCanvas.PreviewMouseRightButtonDown += OnCanvasPreviewRightMouseDown;
                _infiniteCanvas.MouseRightButtonDown += OnCanvasRightMouseDown;
                
                // 初始化右键菜单
                InitializeContextMenu();
            }
        }

        private void UnsubscribeFromEvents()
        {
            if (_infiniteCanvas != null)
            {
                // 取消预览事件订阅
                _infiniteCanvas.PreviewDragOver -= OnCanvasDragOver;
                _infiniteCanvas.PreviewDrop -= OnCanvasDrop;
                _infiniteCanvas.PreviewDragEnter -= OnCanvasDragEnter;
                _infiniteCanvas.PreviewDragLeave -= OnCanvasDragLeave;
                
                // 取消普通事件订阅
                _infiniteCanvas.DragOver -= OnCanvasDragOver;
                _infiniteCanvas.Drop -= OnCanvasDrop;
                _infiniteCanvas.DragEnter -= OnCanvasDragEnter;
                _infiniteCanvas.DragLeave -= OnCanvasDragLeave;
                
                // 取消右键菜单事件
                _infiniteCanvas.PreviewMouseRightButtonDown -= OnCanvasPreviewRightMouseDown;
                _infiniteCanvas.MouseRightButtonDown -= OnCanvasRightMouseDown;
            }
        }

        #endregion

        #region 拖放事件处理

        private void OnCanvasDragEnter(object sender, DragEventArgs e)
        {
            if (IsValidDragData(e.Data))
            {
                e.Effects = DragDropEffects.Copy;
            }
        }

        private void OnCanvasDragOver(object sender, DragEventArgs e)
        {
            // 检查当前 WorkflowTab 是否是活动状态
            if (DataContext is Models.WorkflowTab workflowTab && !workflowTab.IsActive)
            {
                e.Effects = DragDropEffects.None;
                e.Handled = false; // 不处理事件，让它冒泡到活动的 FlowEditor
                return;
            }

            // 检查拖拽数据是否有效
            if (IsValidDragData(e.Data))
            {
                e.Effects = DragDropEffects.Copy;
                // 不设置 Handled，让事件继续传播
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
        }

        private void OnCanvasDragLeave(object sender, DragEventArgs e)
        {
            // 可以在这里添加视觉反馈清除逻辑
        }

        private void OnCanvasDrop(object sender, DragEventArgs e)
        {
            var workflowTab = DataContext as Models.WorkflowTab;
            System.Diagnostics.Debug.WriteLine($"[FlowEditor.OnCanvasDrop] 收到拖放事件 - WorkflowTab: {workflowTab?.Name ?? "null"}, IsActive: {workflowTab?.IsActive}, Canvas: {_infiniteCanvas != null}");
            
            if (_infiniteCanvas == null)
            {
                System.Diagnostics.Debug.WriteLine($"[FlowEditor.OnCanvasDrop] _infiniteCanvas 为 null，返回");
                return;
            }

            // 检查当前 WorkflowTab 是否是活动状态
            if (workflowTab != null && !workflowTab.IsActive)
            {
                System.Diagnostics.Debug.WriteLine($"[FlowEditor.OnCanvasDrop] 拒绝处理拖放：WorkflowTab '{workflowTab.Name}' 不是活动状态（IsActive={workflowTab.IsActive}）");
                e.Handled = false; // 不处理事件，让它冒泡到活动的 FlowEditor
                return;
            }

            System.Diagnostics.Debug.WriteLine($"[FlowEditor.OnCanvasDrop] 处理拖放：WorkflowTab '{workflowTab?.Name ?? "null"}'");
            var dropPosition = e.GetPosition(_infiniteCanvas);
            TryHandleNodeDrop(e, dropPosition);
        }

        #endregion

        #region 键盘事件

        /// <summary>
        /// FlowEditor 键盘事件（支持 Delete 键删除、撤销/重做、复制/粘贴）
        /// </summary>
        private void OnFlowEditorKeyDown(object sender, KeyEventArgs e)
        {
            // Delete 键删除选中节点
            if (e.Key == Key.Delete)
            {
                DeleteSelectedNodes();
                e.Handled = true;
            }

            // Ctrl+C 复制
            if (e.Key == Key.C && Keyboard.Modifiers == ModifierKeys.Control)
            {
                OnCopyMenuItemClick(null, null);
                e.Handled = true;
            }

            // Ctrl+V 粘贴
            if (e.Key == Key.V && Keyboard.Modifiers == ModifierKeys.Control)
            {
                OnPasteMenuItemClick(null, null);
                e.Handled = true;
            }

            // 注意：Ctrl+Z 和 Ctrl+R 的撤销/重做由 MultiFlowEditor 的 InputBindings 处理
            // 这里不再处理，让事件冒泡到父级，以便触发 ViewModel 的 UndoCommand/RedoCommand
            // 这样可以确保撤销/重做时能够切换到对应的 TabItem
        }

        #endregion

        #region 控件级拖放兼容处理

        /// <summary>
        /// 预览拖放（作用于整个 FlowEditor），确保靠近工具箱边缘时也能正确显示拷贝光标
        /// </summary>
        private void OnFlowEditorPreviewDragOver(object sender, DragEventArgs e)
        {
            var workflowTab = DataContext as Models.WorkflowTab;
            
            // 检查当前 WorkflowTab 是否是活动状态
            if (workflowTab != null && !workflowTab.IsActive)
            {
                e.Effects = DragDropEffects.None;
                e.Handled = false; // 让事件传递到活动的 FlowEditor
                return;
            }
            
            if (_infiniteCanvas == null || !IsValidDragData(e.Data))
            {
                e.Effects = DragDropEffects.None;
                return;
            }

            var point = e.GetPosition(_infiniteCanvas);
            if (IsPointInsideCanvas(point))
            {
                e.Effects = DragDropEffects.Copy;
                e.Handled = true;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
        }

        /// <summary>
        /// 预览放置（作用于整个 FlowEditor），兜底处理在画布边缘的拖放
        /// </summary>
        private void OnFlowEditorPreviewDrop(object sender, DragEventArgs e)
        {
            var workflowTab = DataContext as Models.WorkflowTab;
            System.Diagnostics.Debug.WriteLine($"[FlowEditor.OnFlowEditorPreviewDrop] 收到拖放事件 - WorkflowTab: {workflowTab?.Name ?? "null"}, IsActive: {workflowTab?.IsActive}");
            
            if (_infiniteCanvas == null)
            {
                System.Diagnostics.Debug.WriteLine($"[FlowEditor.OnFlowEditorPreviewDrop] _infiniteCanvas 为 null，返回");
                return;
            }

            // 检查当前 WorkflowTab 是否是活动状态
            if (workflowTab != null && !workflowTab.IsActive)
            {
                System.Diagnostics.Debug.WriteLine($"[FlowEditor.OnFlowEditorPreviewDrop] 拒绝处理拖放：WorkflowTab '{workflowTab.Name}' 不是活动状态（IsActive={workflowTab.IsActive}）");
                e.Handled = false; // 不处理事件，让它传递到活动的 FlowEditor
                return;
            }

            System.Diagnostics.Debug.WriteLine($"[FlowEditor.OnFlowEditorPreviewDrop] 处理拖放：WorkflowTab '{workflowTab?.Name ?? "null"}'");
            var point = e.GetPosition(_infiniteCanvas);
            TryHandleNodeDrop(e, point);
        }

        #endregion

        #region 右键菜单

        /// <summary>
        /// 初始化画布右键菜单
        /// </summary>
     
        private ContextMenu _canvasBackgroundContextMenu; // 画布右键菜单（锁定）
        private MenuItem _lockCanvasMenuItem;

        /// <summary>
        /// 初始化画布右键菜单
        /// </summary>
        private void InitializeContextMenu()
        {
            var contextMenuStyle = TryFindResource("ThemedContextMenu") as Style;
            var menuItemStyle = TryFindResource("ThemedMenuItem") as Style;

            // 1) 选中项右键菜单（包含复制、粘贴、启用/禁用、删除）
            // 如果菜单已存在，先清空避免重复添加
            if (_canvasContextMenu != null)
            {
                _canvasContextMenu.Items.Clear();
            }
            else
            {
                _canvasContextMenu = new ContextMenu();
            }
            
            if (contextMenuStyle != null)
            {
                _canvasContextMenu.Style = contextMenuStyle;
            }

            // 复制菜单项
            var copyMenuItem = new MenuItem()
            {
                Header = "复制",
            };
            if (menuItemStyle != null)
                copyMenuItem.Style = menuItemStyle;
            copyMenuItem.Click += OnCopyMenuItemClick;
            _canvasContextMenu.Items.Add(copyMenuItem);

            // 粘贴菜单项（选中项右键菜单）
            var pasteMenuItemForSelection = new MenuItem()
            {
                Header = "粘贴",
            };
            if (menuItemStyle != null)
                pasteMenuItemForSelection.Style = menuItemStyle;
            pasteMenuItemForSelection.Click += OnPasteMenuItemClick;
            _canvasContextMenu.Items.Add(pasteMenuItemForSelection);

            // 分隔符
            _canvasContextMenu.Items.Add(new Separator());

            // 启用/禁用菜单项
            var toggleEnabledMenuItem = new MenuItem()
            {
                Header = "禁用",
            };
            if (menuItemStyle != null)
                toggleEnabledMenuItem.Style = menuItemStyle;
            toggleEnabledMenuItem.Click += OnToggleEnabledMenuItemClick;
            _canvasContextMenu.Items.Add(toggleEnabledMenuItem);

            // 分隔符
            _canvasContextMenu.Items.Add(new Separator());

            // 删除菜单项
            var deleteMenuItem = new MenuItem()
            {
                Header = "删除选中项",
                Tag = "Danger"  // 标记为危险操作
            };
            if (menuItemStyle != null)
                deleteMenuItem.Style = menuItemStyle;
            deleteMenuItem.Click += OnDeleteMenuItemClick;
            _canvasContextMenu.Items.Add(deleteMenuItem);

            // 在菜单打开时更新启用/禁用文本和粘贴菜单项状态
            _canvasContextMenu.Opened += (s, e) =>
            {
                if (_infiniteCanvas?.SelectedItems != null && _infiniteCanvas.SelectedItems.Count > 0)
                {
                    // 检查是否所有选中的节点都已启用
                    bool allEnabled = true;
                    foreach (var item in _infiniteCanvas.SelectedItems)
                    {
                        if (item is Node node && !node.IsEnabled)
                        {
                            allEnabled = false;
                            break;
                        }
                    }
                    toggleEnabledMenuItem.Header = allEnabled ? "禁用" : "启用";
                }

                // 更新粘贴菜单项可用状态（使用共享剪贴板）
                pasteMenuItemForSelection.IsEnabled = SharedClipboardNodes != null && SharedClipboardNodes.Count > 0;
            };

            // 2) 画布右键菜单（锁定画布）
            _canvasBackgroundContextMenu = new ContextMenu();
            if (contextMenuStyle != null)
            {
                _canvasBackgroundContextMenu.Style = contextMenuStyle;
            }

            _lockCanvasMenuItem = new MenuItem()
            {
                Header = "锁定画布",
                IsCheckable = true
            };
            if (menuItemStyle != null)
            {
                _lockCanvasMenuItem.Style = menuItemStyle;
            }
            _lockCanvasMenuItem.Click += (s, e) =>
            {
                if (_infiniteCanvas == null) return;
                _infiniteCanvas.IsLocked = _lockCanvasMenuItem.IsChecked;
            };

            _canvasBackgroundContextMenu.Items.Add(_lockCanvasMenuItem);

            // 分隔符
            _canvasBackgroundContextMenu.Items.Add(new Separator());

            // 粘贴菜单项（画布空白区域右键菜单）
            var pasteMenuItem = new MenuItem()
            {
                Header = "粘贴",
            };
            if (menuItemStyle != null)
                pasteMenuItem.Style = menuItemStyle;
            pasteMenuItem.Click += OnPasteMenuItemClick;
            _canvasBackgroundContextMenu.Items.Add(pasteMenuItem);

            // 在菜单打开时更新粘贴菜单项可用状态和锁定状态
            _canvasBackgroundContextMenu.Opened += (s, e) =>
            {
                // 粘贴菜单项：只有剪贴板有内容时才可用（使用共享剪贴板）
                pasteMenuItem.IsEnabled = SharedClipboardNodes != null && SharedClipboardNodes.Count > 0;
                
                // 更新锁定/解锁文本
                if (_lockCanvasMenuItem != null && _infiniteCanvas != null)
                {
                    _lockCanvasMenuItem.IsChecked = _infiniteCanvas.IsLocked;
                    _lockCanvasMenuItem.Header = _infiniteCanvas.IsLocked ? "解锁画布" : "锁定画布";
                }
            };
        }

        /// <summary>
        /// 画布右键按下预览事件
        /// 专门用于处理“框选组”的右键菜单，优先级高于节点右键菜单
        /// </summary>
        private void OnCanvasPreviewRightMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (_infiniteCanvas == null)
                return;

            var hit = e.OriginalSource as DependencyObject;

            // 如果命中到框选虚线框本身或其子元素，则认为是“框选框右键”
            if (IsHitOnSelectedGroupBox(hit))
            {
                // 直接显示“删除选中项”菜单，并阻止事件继续传递到节点
                ShowSelectionContextMenu();
                e.Handled = true;
            }
        }

        /// <summary>
        /// 画布右键按下事件（冒泡）
        /// </summary>
        private void OnCanvasRightMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (_infiniteCanvas == null)
                return;

            var selectedCount = _infiniteCanvas.SelectedItems?.Count ?? 0;
            var hit = e.OriginalSource as DependencyObject;
            bool isBackgroundClick = IsClickOnCanvasBackground(hit);

            if (!isBackgroundClick && selectedCount > 0 && _canvasContextMenu != null)
            {
                // 在选中节点或组上右键：显示选中项菜单
                ShowSelectionContextMenu();
            }
            else if (_canvasBackgroundContextMenu != null)
            {
                // 在画布空白区域右键：显示画布菜单（锁定/解锁、粘贴），与是否有选中项无关
                // 保存鼠标位置（转换为画布坐标系），用于粘贴
                var mousePos = e.GetPosition(_infiniteCanvas);
                _lastRightClickPosition = _infiniteCanvas.ScreenToCanvas(mousePos);

                if (_lockCanvasMenuItem != null)
                {
                    _lockCanvasMenuItem.IsChecked = _infiniteCanvas.IsLocked;
                    _lockCanvasMenuItem.Header = _infiniteCanvas.IsLocked ? "解锁画布" : "锁定画布";
                }

                _canvasBackgroundContextMenu.PlacementTarget = _infiniteCanvas;
                _canvasBackgroundContextMenu.IsOpen = true;
            }

            e.Handled = true;
        }

        /// <summary>
        /// 删除菜单项点击事件
        /// </summary>
        private void OnDeleteMenuItemClick(object sender, RoutedEventArgs e)
        {
            DeleteSelectedNodes();
        }

        /// <summary>
        /// 复制选中的节点到剪贴板（不立即粘贴）
        /// </summary>
        private void OnCopyMenuItemClick(object sender, RoutedEventArgs e)
        {
            if (_infiniteCanvas == null || _infiniteCanvas.SelectedItems == null || _infiniteCanvas.SelectedItems.Count == 0)
                return;

            // 获取所有选中的节点（克隆后保存，避免后续修改影响粘贴）
            // 🔧 使用共享剪贴板支持跨流程复制粘贴
            var clipboardNodes = new List<Node>();
            double minX = double.MaxValue, minY = double.MaxValue;
            double maxX = double.MinValue, maxY = double.MinValue;

            // 收集选中节点的ID集合（用于复制连线）
            var selectedNodeIds = new HashSet<string>();
            // 旧节点ID到克隆节点的映射（用于更新连线引用）
            var oldNodeIdToClonedNodeMap = new Dictionary<string, Node>();

            System.Diagnostics.Debug.WriteLine($"");
            System.Diagnostics.Debug.WriteLine($"========== 开始复制节点（支持跨流程） ==========");

            foreach (var item in _infiniteCanvas.SelectedItems)
            {
                if (item is Node node)
                {
                    System.Diagnostics.Debug.WriteLine($"复制节点: {node.Name} (ID: {node.Id}), 原始位置: ({node.Position.X:F2}, {node.Position.Y:F2})");
                    
                    // 保存原始属性（因为 Clone() 会丢失只读结构体）
                    var originalPosition = node.Position;
                    var originalSize = node.Size;
                    
                    // 克隆节点
                    var clonedNode = node.Clone();
                    
                    System.Diagnostics.Debug.WriteLine($"  克隆后节点ID: {clonedNode.Id}");
                    System.Diagnostics.Debug.WriteLine($"  克隆后 Position: ({clonedNode.Position.X:F2}, {clonedNode.Position.Y:F2})");
                    
                    // 🔧 手动恢复位置和尺寸
                    clonedNode.Position = originalPosition;
                    clonedNode.Size = originalSize;
                    
                    System.Diagnostics.Debug.WriteLine($"  恢复后 Position: ({clonedNode.Position.X:F2}, {clonedNode.Position.Y:F2})");
                    System.Diagnostics.Debug.WriteLine($"  恢复后 Size: ({clonedNode.Size.Width:F2}, {clonedNode.Size.Height:F2})");
                    
                    clipboardNodes.Add(clonedNode);
                    
                    // 记录原始ID和克隆节点的映射（用于更新连线的节点ID）
                    selectedNodeIds.Add(node.Id);
                    oldNodeIdToClonedNodeMap[node.Id] = clonedNode;
                    
                    System.Diagnostics.Debug.WriteLine($"  节点ID映射: {node.Id} -> {clonedNode.Id}");
                    
                    // 使用恢复后的位置计算边界框
                    var nodeWidth = clonedNode.Size.IsEmpty ? 220 : clonedNode.Size.Width;
                    var nodeHeight = clonedNode.Size.IsEmpty ? 40 : clonedNode.Size.Height;
                        
                    minX = Math.Min(minX, clonedNode.Position.X);
                    minY = Math.Min(minY, clonedNode.Position.Y);
                    maxX = Math.Max(maxX, clonedNode.Position.X + nodeWidth);
                    maxY = Math.Max(maxY, clonedNode.Position.Y + nodeHeight);
                    }
                }
            
            System.Diagnostics.Debug.WriteLine($"========== 复制节点完成 ==========");
            System.Diagnostics.Debug.WriteLine($"");

            // 保存边界框
            Rect clipboardBounds = Rect.Empty;
            if (clipboardNodes.Count > 0 && minX != double.MaxValue)
            {
                clipboardBounds = new Rect(minX, minY, maxX - minX, maxY - minY);
            }

            // 复制选中节点之间的连线，并更新节点ID引用
            // 注意：端口ID的格式是"节点ID:端口位置"（例如"8d7a0f62-1ae0-4802-bae8-b26805b83e66:Bottom"）
            var clipboardEdges = new List<Astra.Core.Nodes.Models.Edge>();
            if (EdgeItemsSource != null)
            {
                System.Diagnostics.Debug.WriteLine($"========== 开始复制连线 ==========");
                
                foreach (var item in EdgeItemsSource)
                {
                    if (item is Astra.Core.Nodes.Models.Edge edge)
                    {
                        // 只复制两端都在选中节点集合中的连线
                        if (selectedNodeIds.Contains(edge.SourceNodeId) && 
                            selectedNodeIds.Contains(edge.TargetNodeId))
                        {
                            System.Diagnostics.Debug.WriteLine($"复制连线: SourceNode={edge.SourceNodeId}, SourcePort={edge.SourcePortId}");
                            System.Diagnostics.Debug.WriteLine($"         -> TargetNode={edge.TargetNodeId}, TargetPort={edge.TargetPortId}");
                            
                            // 克隆连线
                            var clonedEdge = edge.Clone();
                            
                            // 更新连线的节点ID引用（指向剪贴板中的克隆节点）
                            if (oldNodeIdToClonedNodeMap.ContainsKey(edge.SourceNodeId))
                            {
                                var newSourceNodeId = oldNodeIdToClonedNodeMap[edge.SourceNodeId].Id;
                                clonedEdge.SourceNodeId = newSourceNodeId;
                                
                                // 🔧 更新端口ID中的节点ID部分（端口ID格式：节点ID:端口位置）
                                if (!string.IsNullOrEmpty(edge.SourcePortId) && edge.SourcePortId.Contains(":"))
                                {
                                    var parts = edge.SourcePortId.Split(':');
                                    if (parts.Length >= 2)
                                    {
                                        // 替换端口ID中的节点ID部分
                                        clonedEdge.SourcePortId = $"{newSourceNodeId}:{parts[1]}";
                                        System.Diagnostics.Debug.WriteLine($"  源端口ID更新: {edge.SourcePortId} -> {clonedEdge.SourcePortId}");
                                    }
                                }
                            }
                            
                            if (oldNodeIdToClonedNodeMap.ContainsKey(edge.TargetNodeId))
                            {
                                var newTargetNodeId = oldNodeIdToClonedNodeMap[edge.TargetNodeId].Id;
                                clonedEdge.TargetNodeId = newTargetNodeId;
                                
                                // 🔧 更新端口ID中的节点ID部分（端口ID格式：节点ID:端口位置）
                                if (!string.IsNullOrEmpty(edge.TargetPortId) && edge.TargetPortId.Contains(":"))
                                {
                                    var parts = edge.TargetPortId.Split(':');
                                    if (parts.Length >= 2)
                                    {
                                        // 替换端口ID中的节点ID部分
                                        clonedEdge.TargetPortId = $"{newTargetNodeId}:{parts[1]}";
                                        System.Diagnostics.Debug.WriteLine($"  目标端口ID更新: {edge.TargetPortId} -> {clonedEdge.TargetPortId}");
                                    }
                                }
                            }
                            
                            System.Diagnostics.Debug.WriteLine($"  最终连线: SourceNode={clonedEdge.SourceNodeId}, SourcePort={clonedEdge.SourcePortId}");
                            System.Diagnostics.Debug.WriteLine($"           -> TargetNode={clonedEdge.TargetNodeId}, TargetPort={clonedEdge.TargetPortId}");
                            
                            clipboardEdges.Add(clonedEdge);
                        }
                    }
                }
                
                System.Diagnostics.Debug.WriteLine($"========== 复制连线完成 ==========");
                System.Diagnostics.Debug.WriteLine($"");
            }

            // 🔧 保存到共享剪贴板（支持跨流程复制粘贴）
            SharedClipboardNodes = clipboardNodes;
            SharedClipboardEdges = clipboardEdges;
            SharedClipboardBounds = clipboardBounds;

            System.Diagnostics.Debug.WriteLine($"[复制] 节点数: {SharedClipboardNodes.Count}, 连线数: {SharedClipboardEdges.Count}");
            System.Diagnostics.Debug.WriteLine($"[复制] 已保存到共享剪贴板（支持跨流程粘贴）");
        }

        /// <summary>
        /// 粘贴剪贴板中的节点和连线（粘贴到鼠标位置，保持节点相对位置和连线关系）
        /// </summary>
        private void OnPasteMenuItemClick(object sender, RoutedEventArgs e)
        {
            // 🔧 使用共享剪贴板支持跨流程复制粘贴
            if (_infiniteCanvas == null || SharedClipboardNodes == null || SharedClipboardNodes.Count == 0)
                return;

            if (_infiniteCanvas.ItemsSource is not System.Collections.IList itemsList)
            {
                System.Diagnostics.Debug.WriteLine("警告：ItemsSource 不是 IList，无法粘贴节点");
                return;
            }

            System.Diagnostics.Debug.WriteLine($"[粘贴] 从共享剪贴板粘贴，节点数: {SharedClipboardNodes.Count}, 连线数: {SharedClipboardEdges?.Count ?? 0}");

            // 获取粘贴位置（画布坐标系，已考虑缩放）
            System.Windows.Point pastePosition;
            if (_lastRightClickPosition.HasValue)
            {
                // 使用保存的右键点击位置（右键菜单触发的粘贴）
                // _lastRightClickPosition 已经是画布坐标系
                pastePosition = _lastRightClickPosition.Value;
                }
            else
            {
                // 使用当前鼠标位置（快捷键 Ctrl+V 触发的粘贴）
                var mouseScreenPos = Mouse.GetPosition(_infiniteCanvas);
                pastePosition = _infiniteCanvas.ScreenToCanvas(mouseScreenPos);
            }

            // 获取原始边界框的左上角作为参考点（与拖拽创建节点的行为一致）
            // 这样粘贴时，节点组的左上角会对齐鼠标位置
            Point2D? originalTopLeft = null;
            if (SharedClipboardBounds != Rect.Empty)
            {
                var bounds = SharedClipboardBounds;
                originalTopLeft = new Point2D(bounds.Left, bounds.Top);
            }
            else if (SharedClipboardNodes.Count > 0 && SharedClipboardNodes[0].Position != null)
            {
                // 如果没有边界框，使用第一个节点的位置作为参考
                originalTopLeft = SharedClipboardNodes[0].Position;
            }

            if (!originalTopLeft.HasValue)
            {
                System.Diagnostics.Debug.WriteLine("❌ [粘贴] 错误：无法确定粘贴位置");
                return;
            }

            // 计算偏移量（鼠标位置 - 原始左上角）
            // 这样粘贴后，节点组的左上角会位于鼠标位置（与拖拽创建节点行为一致）
            var offsetX = pastePosition.X - originalTopLeft.Value.X;
            var offsetY = pastePosition.Y - originalTopLeft.Value.Y;

            // 🔧 性能优化：减少调试日志输出（特别是在粘贴多个节点时）
            int nodeCount = SharedClipboardNodes.Count;
            bool verboseLogging = nodeCount < 10; // 只有节点数少于10时才输出详细日志
            
            // 再次克隆节点（支持多次粘贴）并应用偏移量
            var clonedNodes = new List<Node>();
            var clipboardNodeIdToNewNodeIdMap = new Dictionary<string, string>(); // 剪贴板节点ID -> 新节点ID 的映射

            if (verboseLogging)
            {
                System.Diagnostics.Debug.WriteLine($"========== 开始粘贴克隆节点（跨流程） ==========");
            }
            
            foreach (var clipboardNode in SharedClipboardNodes)
            {
                // 保存剪贴板节点的属性
                var clipboardPosition = clipboardNode.Position;
                var clipboardSize = clipboardNode.Size;
                
                // 再次克隆（因为剪贴板中的节点已经是克隆的，但需要支持多次粘贴）
                var newNode = clipboardNode.Clone();
                
                // 🔧 修复：手动恢复位置和尺寸（因为 Clone() 会丢失只读结构体）
                newNode.Position = clipboardPosition;
                newNode.Size = clipboardSize;
                
                // 记录ID映射关系（用于更新连线）
                clipboardNodeIdToNewNodeIdMap[clipboardNode.Id] = newNode.Id;
                
                // 应用偏移量（将节点组的左上角移动到鼠标位置）
                newNode.Position = new Point2D(
                    newNode.Position.X + offsetX,
                    newNode.Position.Y + offsetY
                );
                
                // 🔧 粘贴后的节点不应该显示选中状态（虚线框）
                newNode.IsSelected = false;
                
                clonedNodes.Add(newNode);
            }
            
            if (verboseLogging)
            {
                System.Diagnostics.Debug.WriteLine($"========== 粘贴克隆完成 ==========");
            }

            // 克隆连线并更新节点ID和端口ID引用
            // 注意：端口ID的格式是"节点ID:端口位置"（例如"8d7a0f62-1ae0-4802-bae8-b26805b83e66:Bottom"）
            var clonedEdges = new List<Astra.Core.Nodes.Models.Edge>();
            if (SharedClipboardEdges != null && SharedClipboardEdges.Count > 0)
            {
                if (verboseLogging)
                {
                    System.Diagnostics.Debug.WriteLine($"========== 开始粘贴连线（跨流程） ==========");
                }
                
                foreach (var clipboardEdge in SharedClipboardEdges)
                {
                    // 检查连线的两端节点是否都在映射表中
                    if (clipboardNodeIdToNewNodeIdMap.ContainsKey(clipboardEdge.SourceNodeId) && 
                        clipboardNodeIdToNewNodeIdMap.ContainsKey(clipboardEdge.TargetNodeId))
                    {
                        var newEdge = clipboardEdge.Clone();
                        
                        // 更新节点ID引用（指向新粘贴的节点）
                        var newSourceNodeId = clipboardNodeIdToNewNodeIdMap[clipboardEdge.SourceNodeId];
                        var newTargetNodeId = clipboardNodeIdToNewNodeIdMap[clipboardEdge.TargetNodeId];
                        
                        newEdge.SourceNodeId = newSourceNodeId;
                        newEdge.TargetNodeId = newTargetNodeId;
                        
                        // 🔧 更新端口ID中的节点ID部分（端口ID格式：节点ID:端口位置）
                        if (!string.IsNullOrEmpty(clipboardEdge.SourcePortId) && clipboardEdge.SourcePortId.Contains(":"))
                        {
                            var parts = clipboardEdge.SourcePortId.Split(':');
                            if (parts.Length >= 2)
                            {
                                // 替换端口ID中的节点ID部分
                                newEdge.SourcePortId = $"{newSourceNodeId}:{parts[1]}";
                            }
                        }
                        else
                        {
                            // 如果端口ID不包含冒号，保持原值（可能是旧格式或空值）
                            newEdge.SourcePortId = clipboardEdge.SourcePortId;
                        }
                        
                        if (!string.IsNullOrEmpty(clipboardEdge.TargetPortId) && clipboardEdge.TargetPortId.Contains(":"))
                        {
                            var parts = clipboardEdge.TargetPortId.Split(':');
                            if (parts.Length >= 2)
                            {
                                // 替换端口ID中的节点ID部分
                                newEdge.TargetPortId = $"{newTargetNodeId}:{parts[1]}";
                            }
                        }
                        else
                        {
                            // 如果端口ID不包含冒号，保持原值（可能是旧格式或空值）
                            newEdge.TargetPortId = clipboardEdge.TargetPortId;
                        }
                        
                        clonedEdges.Add(newEdge);
                    }
                }
                
                if (verboseLogging)
                {
                    System.Diagnostics.Debug.WriteLine($"========== 粘贴连线完成 ==========");
                }
            }

            // 🔧 使用撤销/重做命令（支持批量操作优化）
            // 注意：不再区分同步/异步添加，因为批量操作已经优化了性能
            // 使用撤销/重做命令添加克隆的节点和连线
            if (EdgeItemsSource is System.Collections.IList edgesList)
            {
            if (_undoRedoManager != null)
            {
                    var command = new PasteNodesWithEdgesCommand(itemsList, edgesList, clonedNodes, clonedEdges);
                _undoRedoManager.Execute(command);
            }
            else
            {
                // 无撤销管理器，直接添加
                foreach (var clonedNode in clonedNodes)
                {
                    itemsList.Add(clonedNode);
                }
                    foreach (var clonedEdge in clonedEdges)
                    {
                        edgesList.Add(clonedEdge);
                    }
                }
            }
            else
            {
                // 只有节点列表，没有连线列表
                if (_undoRedoManager != null)
                {
                    var command = new PasteNodesCommand(itemsList, clonedNodes);
                    _undoRedoManager.Execute(command);
                }
                else
                {
                    foreach (var clonedNode in clonedNodes)
                    {
                        itemsList.Add(clonedNode);
                    }
                }
            }

            // 🔧 粘贴后清除选中状态（不显示虚线框）
            _infiniteCanvas.ClearSelection();

            // 🔧 性能优化：减少调试日志输出（特别是在粘贴多个节点时）
            if (verboseLogging)
            {
                System.Diagnostics.Debug.WriteLine($"✅ 成功粘贴 {clonedNodes.Count} 个节点，{clonedEdges.Count} 条连线");
            }
        }

        /// <summary>
        /// 启用/禁用选中的节点
        /// </summary>
        private void OnToggleEnabledMenuItemClick(object sender, RoutedEventArgs e)
        {
            if (_infiniteCanvas == null || _infiniteCanvas.SelectedItems == null || _infiniteCanvas.SelectedItems.Count == 0)
                return;

            // 获取所有选中的节点
            var selectedNodes = new List<Node>();
            foreach (var item in _infiniteCanvas.SelectedItems)
            {
                if (item is Node node)
                    selectedNodes.Add(node);
            }

            if (selectedNodes.Count == 0)
                return;

            // 判断新状态：如果所有节点都已启用，则禁用；否则启用
            bool allEnabled = selectedNodes.All(n => n.IsEnabled);
            var newState = !allEnabled;

            // 使用撤销/重做命令
            if (_undoRedoManager != null)
            {
                var command = new ToggleNodeEnabledCommand(selectedNodes, newState);
                _undoRedoManager.Execute(command);
            }
            else
            {
                // 无撤销管理器，直接应用
                foreach (var node in selectedNodes)
                {
                    node.IsEnabled = newState;
                }
            }
        }

        /// <summary>
        /// 删除选中的节点（委托给 InfiniteCanvas 统一处理）
        /// </summary>
        private void DeleteSelectedNodes()
        {
            // 委托给 InfiniteCanvas 的统一删除方法
            _infiniteCanvas?.DeleteSelectedItems();
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 检查拖拽数据是否有效
        /// </summary>
        private bool IsValidDragData(IDataObject data)
        {
            if (data == null)
                return false;

            // 优先检查自定义格式
            bool hasFormat = data.GetDataPresent(DragDropDataFormats.ToolItem);

            // 如果自定义格式不存在，尝试按类型检查（向后兼容）
            if (!hasFormat)
            {
                hasFormat = data.GetDataPresent(typeof(IToolItem));
            }

            return hasFormat;
        }

        /// <summary>
        /// 检查坐标是否位于画布可见区域内
        /// </summary>
        private bool IsPointInsideCanvas(System.Windows.Point pointOnCanvasControl)
        {
            if (_infiniteCanvas == null)
                return false;

            // 允许一定的安全边距，避免列分隔线/浮层裁剪导致的临界点失效（尤其是靠近左边界时）
            const double safeMargin = 48.0;

            // 若尺寸尚未计算，避免阻断拖放
            if (_infiniteCanvas.ActualWidth <= 0 || _infiniteCanvas.ActualHeight <= 0)
                return true;

            return pointOnCanvasControl.X >= -safeMargin && pointOnCanvasControl.X <= _infiniteCanvas.ActualWidth + safeMargin &&
                   pointOnCanvasControl.Y >= -safeMargin && pointOnCanvasControl.Y <= _infiniteCanvas.ActualHeight + safeMargin;
        }

        /// <summary>
        /// 判断一次右键是否点击在画布空白区域（非节点、非内容元素）
        /// 用于区分“删除选中项”和“画布锁定”菜单
        /// </summary>
        private bool IsClickOnCanvasBackground(DependencyObject hitElement)
        {
            if (hitElement == null || _infiniteCanvas == null)
                return true;

            var current = hitElement;
            while (current != null && current != _infiniteCanvas)
            {
                // 如果命中到节点或常见交互控件，则认为不是空白区域
                if (current is NodeControl ||
                    current is TextBox ||
                    current is System.Windows.Controls.Primitives.ButtonBase ||
                    current is ContentPresenter)
                {
                    return false;
                }

                current = VisualTreeHelper.GetParent(current);
            }

            // 走到这里说明中间没有遇到节点等控件，视为画布空白
            return true;
        }

        /// <summary>
        /// 判断是否命中到 InfiniteCanvas 的“框选组虚线框”
        /// </summary>
        private bool IsHitOnSelectedGroupBox(DependencyObject hitElement)
        {
            if (hitElement == null || _infiniteCanvas == null)
                return false;

            var current = hitElement;
            while (current != null && current != _infiniteCanvas)
            {
                if (current is FrameworkElement fe && fe.Name == "PART_SelectedGroupBox")
                {
                    return true;
                }

                current = VisualTreeHelper.GetParent(current);
            }

            return false;
        }

        /// <summary>
        /// 显示选中项的右键菜单（节点或框选组通用，包含复制、粘贴、启用/禁用、删除）
        /// </summary>
        private void ShowSelectionContextMenu()
        {
            if (_infiniteCanvas == null || _canvasContextMenu == null)
                return;

            var selectedCount = _infiniteCanvas.SelectedItems?.Count ?? 0;
            if (selectedCount <= 0)
                return;

            // 查找删除菜单项并更新标题
            foreach (var item in _canvasContextMenu.Items)
            {
                if (item is MenuItem menuItem && menuItem.Tag?.ToString() == "Danger")
                {
                    menuItem.Header = selectedCount > 1 ? $"删除 {selectedCount} 个选中项" : "删除选中项";
                    break;
                }
            }

            _canvasContextMenu.PlacementTarget = _infiniteCanvas;
            _canvasContextMenu.IsOpen = true;
        }

        /// <summary>
        /// 统一的节点拖放处理逻辑，支持画布与外层控件的拖放事件
        /// </summary>
        private bool TryHandleNodeDrop(DragEventArgs e, System.Windows.Point pointOnCanvasControl)
        {
            if (_infiniteCanvas == null || !IsValidDragData(e.Data) || !IsPointInsideCanvas(pointOnCanvasControl))
                return false;

            try
            {
                // 获取工具项
                var toolItem = e.Data.GetData(DragDropDataFormats.ToolItem) as IToolItem;

                // 如果自定义格式获取失败，尝试通过类型获取（向后兼容）
                if (toolItem == null && e.Data.GetDataPresent(typeof(IToolItem)))
                {
                    toolItem = e.Data.GetData(typeof(IToolItem)) as IToolItem;
                }

                if (toolItem == null)
                    return false;

                // 将控件坐标转换到画布坐标系
                var canvasPosition = _infiniteCanvas.ScreenToCanvas(pointOnCanvasControl);

                // 创建节点
                Node node = CreateDefaultNode(toolItem, canvasPosition);

                if (node == null)
                    return false;

                // 添加到画布
                AddNodeToCanvas(node, canvasPosition);

                e.Effects = DragDropEffects.Copy;
                e.Handled = true;
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FlowEditor] Drop 错误: {ex.Message}");
                e.Effects = DragDropEffects.None;
                return false;
            }
        }

        /// <summary>
        /// 根据工具项创建节点
        /// 要求：
        /// 1. IToolItem.NodeType 必须指定为 Node 的子类类型（Type 对象或类型名称字符串）
        /// 2. 节点类型必须不是抽象类
        /// 3. 节点类型必须有公共无参构造函数
        /// 如果创建失败，返回 null
        /// </summary>
        private Node CreateDefaultNode(IToolItem toolItem, System.Windows.Point position)
        {
            // 如果工具项没有指定节点类型，返回 null
            if (toolItem.NodeType == null)
            {
                System.Diagnostics.Debug.WriteLine($"工具项 {toolItem.Name} 未指定 NodeType，无法创建节点");
                return null;
            }

            try
            {
                Type nodeType = null;

                // 如果 NodeType 是 Type 对象，直接使用
                if (toolItem.NodeType is Type type)
                {
                    nodeType = type;
                }
                // 如果 NodeType 是字符串，尝试解析为类型
                else if (toolItem.NodeType is string typeName && !string.IsNullOrWhiteSpace(typeName))
                {
                    // 首先在当前程序集中查找
                    nodeType = Type.GetType(typeName, false);
                    
                    // 如果找不到，尝试在所有已加载的程序集中查找
                    if (nodeType == null)
                    {
                        foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
                        {
                            nodeType = assembly.GetType(typeName, false);
                            if (nodeType != null)
                                break;
                        }
                    }
                }

                // 如果找不到类型，返回 null
                if (nodeType == null)
                {
                    System.Diagnostics.Debug.WriteLine($"无法找到节点类型: {toolItem.NodeType}");
                    return null;
                }

                // 验证类型必须是 Node 的子类
                if (!typeof(Node).IsAssignableFrom(nodeType))
                {
                    System.Diagnostics.Debug.WriteLine($"节点类型 {nodeType.FullName} 不是 Node 的子类，返回 null");
                    return null;
                }

                // 检查是否是抽象类
                if (nodeType.IsAbstract)
                {
                    System.Diagnostics.Debug.WriteLine($"节点类型 {nodeType.FullName} 是抽象类，无法创建实例，返回 null");
                    return null;
                }

                // 尝试使用无参构造函数创建实例
                var instance = System.Activator.CreateInstance(nodeType) as Node;
                if (instance == null)
                {
                    System.Diagnostics.Debug.WriteLine($"无法创建节点实例: {nodeType.FullName}");
                    return null;
                }

                // 设置节点名称
                instance.Name = toolItem.Name ?? "未命名节点";

                // 设置节点类型字符串（用于序列化和识别）
                instance.NodeType = nodeType.Name;

                // 设置位置属性（使用 Point2D）
                instance.Position = new Point2D(position.X, position.Y);

                // 如果工具项有描述，设置描述
                if (!string.IsNullOrWhiteSpace(toolItem.Description))
                {
                    instance.Description = toolItem.Description;
                }

                return instance;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"使用 NodeType 创建节点时发生错误: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 设置节点位置（支持多种位置属性格式）
        /// </summary>
        private void SetNodePosition(object node, System.Windows.Point position)
        {
            if (node == null) return;

            var nodeType = node.GetType();

            // 尝试设置 Position 属性（Point2D 类型）
            var positionProp = nodeType.GetProperty("Position");
            if (positionProp != null && positionProp.CanWrite)
            {
                var positionType = positionProp.PropertyType;
                // 检查是否是 Point2D 类型
                if (positionType.Name == "Point2D")
                {
                    // 尝试创建 Point2D 实例
                    var point2DType = positionType;
                    var xProp = point2DType.GetProperty("X");
                    var yProp = point2DType.GetProperty("Y");
                    if (xProp != null && yProp != null)
                    {
                        // 使用构造函数创建 Point2D
                        var constructor = point2DType.GetConstructor(new[] { typeof(double), typeof(double) });
                        if (constructor != null)
                        {
                            var point2D = constructor.Invoke(new object[] { position.X, position.Y });
                            positionProp.SetValue(node, point2D);
                            return;
                        }
                    }
                }
                // 如果是 System.Windows.Point 类型
                else if (positionType == typeof(System.Windows.Point))
                {
                    positionProp.SetValue(node, position);
                    return;
                }
            }

            // 尝试设置 X 和 Y 属性
            var xProp2 = nodeType.GetProperty("X");
            var yProp2 = nodeType.GetProperty("Y");
            if (xProp2 != null && xProp2.CanWrite)
            {
                xProp2.SetValue(node, position.X);
            }
            if (yProp2 != null && yProp2.CanWrite)
            {
                yProp2.SetValue(node, position.Y);
            }
        }

        /// <summary>
        /// 添加节点到画布数据源（使用撤销/重做命令）
        /// 要求：节点必须是 Node 的子类
        /// </summary>
        private void AddNodeToCanvas(Node node, System.Windows.Point position)
        {
            var workflowTab = DataContext as Models.WorkflowTab;
            System.Diagnostics.Debug.WriteLine($"[FlowEditor.AddNodeToCanvas] 添加节点: {node?.Name}, WorkflowTab: {workflowTab?.Name ?? "null"}, IsActive: {workflowTab?.IsActive}");
            System.Diagnostics.Debug.WriteLine($"[FlowEditor.AddNodeToCanvas] CanvasItemsSource: {CanvasItemsSource?.GetHashCode()}, Count: {(CanvasItemsSource as IList)?.Count}");
            System.Diagnostics.Debug.WriteLine($"[FlowEditor.AddNodeToCanvas] WorkflowTab.Nodes: {workflowTab?.Nodes?.GetHashCode()}, Count: {workflowTab?.Nodes?.Count}");
            System.Diagnostics.Debug.WriteLine($"[FlowEditor.AddNodeToCanvas] 集合是否相同: {ReferenceEquals(CanvasItemsSource, workflowTab?.Nodes)}");
            
            if (CanvasItemsSource == null)
            {
                System.Diagnostics.Debug.WriteLine("CanvasItemsSource 为 null，无法添加节点");
                return;
            }

            // 验证节点类型（参数已经是 Node 类型，这里只检查是否为 null）
            if (node == null)
            {
                System.Diagnostics.Debug.WriteLine("节点为 null，无法添加到画布");
                return;
            }

            // 确保位置已设置（Node 使用 Point2D）
            if (node.Position == null || node.Position.X != position.X || node.Position.Y != position.Y)
            {
                node.Position = new Point2D(position.X, position.Y);
            }

            // 使用撤销/重做命令添加节点
            if (CanvasItemsSource is IList list)
            {
                System.Diagnostics.Debug.WriteLine($"[FlowEditor.AddNodeToCanvas] 准备添加节点到集合，集合当前节点数: {list.Count}");
                if (_undoRedoManager != null)
                {
                    var command = new AddNodeCommand(list, node);
                    // 设置命令的 WorkflowTab
                    if (DataContext is Models.WorkflowTab workflowTab2)
                    {
                        command.WorkflowTab = workflowTab2;
                    }
                    _undoRedoManager.Execute(command);
                    System.Diagnostics.Debug.WriteLine($"[FlowEditor.AddNodeToCanvas] 节点已添加（通过命令），集合当前节点数: {list.Count}");
                }
                else
                {
                    // 回退：直接添加
                    list.Add(node);
                    System.Diagnostics.Debug.WriteLine($"[FlowEditor.AddNodeToCanvas] 节点已添加（直接），集合当前节点数: {list.Count}");
                }
            }
            else
            {
                // 尝试通过反射调用 Add 方法（适用于 ObservableCollection<T> 等泛型集合）
                var addMethod = CanvasItemsSource.GetType().GetMethod("Add");
                if (addMethod != null)
                {
                    try
                    {
                        // 注意：如果不是 IList，无法使用撤销/重做
                        addMethod.Invoke(CanvasItemsSource, new[] { node });
                        System.Diagnostics.Debug.WriteLine("警告：CanvasItemsSource 不是 IList，无法使用撤销/重做功能");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"添加节点到集合时发生错误: {ex.Message}");
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"CanvasItemsSource 不支持添加操作，请使用 IList 或 ObservableCollection<Node>");
                }
            }
        }

        /// <summary>
        /// 应用数据源
        /// </summary>
        private void ApplyDataSources()
        {
            if (_nodeToolBox != null)
            {
                _nodeToolBox.ItemsSource = ToolBoxItemsSource;
            }

            if (_infiniteCanvas != null)
            {
                _infiniteCanvas.ItemsSource = CanvasItemsSource;
                
                // 初始化 SelectedItems 集合
                if (_infiniteCanvas.SelectedItems == null)
                {
                    _infiniteCanvas.SelectedItems = new System.Collections.ObjectModel.ObservableCollection<object>();
                }

                _infiniteCanvas.EdgeItemsSource = EdgeItemsSource;
                _infiniteCanvas.UndoRedoManager = _undoRedoManager;

                // 应用 ItemTemplateSelector 到内部的 ItemsControl
                ApplyItemTemplateSelector();
            }
        }

        /// <summary>
        /// 应用 ItemTemplateSelector 到 InfiniteCanvas
        /// </summary>
        private void ApplyItemTemplateSelector()
        {
            if (_infiniteCanvas == null)
            {
                System.Diagnostics.Debug.WriteLine("[FlowEditor] InfiniteCanvas 为 null，无法应用 ItemTemplateSelector");
                return;
            }

            // 直接设置 InfiniteCanvas 的 ItemTemplateSelector，模板会自动绑定到内部的 ItemsControl
            _infiniteCanvas.ItemTemplateSelector = ItemTemplateSelector;
            
            System.Diagnostics.Debug.WriteLine($"[FlowEditor] 已为 InfiniteCanvas 设置 ItemTemplateSelector: {ItemTemplateSelector?.GetType().Name ?? "null"}");
            
            // 如果 ItemTemplateSelector 不为 null，清除 ItemTemplate（让 ItemTemplateSelector 生效）
            if (ItemTemplateSelector != null && _infiniteCanvas.ItemTemplate != null)
            {
                System.Diagnostics.Debug.WriteLine("[FlowEditor] 清除 InfiniteCanvas 的 ItemTemplate，让 ItemTemplateSelector 生效");
                _infiniteCanvas.ItemTemplate = null;
            }
        }

        /// <summary>
        /// 在可视化树中查找指定类型的子元素
        /// </summary>
        private static T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null)
                return null;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T result)
                {
                    return result;
                }
                var childOfChild = FindVisualChild<T>(child);
                if (childOfChild != null)
                {
                    return childOfChild;
                }
            }
            return null;
        }

        #endregion

        #region 依赖属性变更处理

        private static void OnToolBoxItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var editor = (FlowEditor)d;
            if (editor._nodeToolBox != null)
            {
                editor._nodeToolBox.ItemsSource = e.NewValue as IEnumerable;
            }
            else
            {
                // 如果模板部件还未加载，延迟到 Loaded 事件中处理
                editor.Loaded += (s, args) =>
                {
                    if (editor._nodeToolBox != null)
                    {
                        editor._nodeToolBox.ItemsSource = e.NewValue as IEnumerable;
                    }
                };
            }
        }

        private static void OnCanvasItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var editor = (FlowEditor)d;
            
            // 验证集合中的元素必须是 Node 的子类
            if (e.NewValue is IEnumerable items)
            {
                foreach (var item in items)
                {
                    if (item != null && !(item is Node))
                    {
                        System.Diagnostics.Debug.WriteLine($"警告：CanvasItemsSource 中包含非 Node 类型的对象: {item.GetType().FullName}");
                    }
                }
            }
            
            if (editor._infiniteCanvas != null)
            {
                editor._infiniteCanvas.ItemsSource = e.NewValue as IEnumerable;
            }
        }

        private static void OnEdgeItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var editor = (FlowEditor)d;
            if (editor._infiniteCanvas != null)
            {
                editor._infiniteCanvas.EdgeItemsSource = e.NewValue as IEnumerable;
            }
            else
            {
                // 如果模板部件还未加载，延迟到 ApplyDataSources 中处理
                editor.Loaded += (s, args) =>
                {
                    if (editor._infiniteCanvas != null)
                    {
                        editor._infiniteCanvas.EdgeItemsSource = e.NewValue as IEnumerable;
                    }
                };
            }
        }

        private static void OnItemTemplateSelectorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var editor = (FlowEditor)d;
            var oldSelector = e.OldValue as System.Windows.Controls.DataTemplateSelector;
            var newSelector = e.NewValue as System.Windows.Controls.DataTemplateSelector;
            
            System.Diagnostics.Debug.WriteLine($"[FlowEditor] ItemTemplateSelector 变更: {oldSelector?.GetType().Name ?? "null"} -> {newSelector?.GetType().Name ?? "null"}");
            
            // 如果 InfiniteCanvas 已加载，立即应用
            if (editor._infiniteCanvas != null)
            {
                editor.ApplyItemTemplateSelector();
            }
            // 否则会在 ApplyDataSources 中应用
        }

        private static void OnIsToolBoxVisibleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var editor = (FlowEditor)d;
            editor.ApplyToolBoxVisibility();
        }

        /// <summary>
        /// 应用工具箱可见性
        /// </summary>
        private void ApplyToolBoxVisibility()
        {
            var visibility = IsToolBoxVisible ? Visibility.Visible : Visibility.Collapsed;
            
            System.Diagnostics.Debug.WriteLine($"[FlowEditor] 应用工具箱可见性: IsToolBoxVisible={IsToolBoxVisible}, Visibility={visibility}, _nodeToolBox={(_nodeToolBox != null ? "已找到" : "未找到")}");
            
            if (_nodeToolBox != null)
            {
                _nodeToolBox.Visibility = visibility;
                System.Diagnostics.Debug.WriteLine($"[FlowEditor] NodeToolBox.Visibility 已设置为: {visibility}");
            }
            
            if (_toolBoxSeparator != null)
            {
                _toolBoxSeparator.Visibility = visibility;
            }
        }

        #endregion

        #region 生命周期

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            ApplyDataSources();
            
            // 确保在加载后 InfiniteCanvas 的 AllowDrop 已设置
            if (_infiniteCanvas != null)
            {
                // 如果 FlowEditor 禁用了拖放，InfiniteCanvas 也应该禁用
                _infiniteCanvas.AllowDrop = AllowDrop;
                _infiniteCanvas.IsHitTestVisible = true;
            }

            // 只有允许拖放的 FlowEditor 才注册窗口级别的拖放处理器
            if (AllowDrop)
            {
                AttachWindowDragHandlers();
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            DetachWindowDragHandlers();
        }

        #endregion

        #region 窗口级拖放兜底

        private void AttachWindowDragHandlers()
        {
            if (_windowEventsAttached)
                return;

            _hostWindow = Window.GetWindow(this);
            if (_hostWindow == null)
                return;

            _hostWindow.AllowDrop = true;
            _hostWindow.PreviewDragOver += OnWindowPreviewDragOver;
            _hostWindow.PreviewDrop += OnWindowPreviewDrop;
            _windowEventsAttached = true;
        }

        private void DetachWindowDragHandlers()
        {
            if (!_windowEventsAttached || _hostWindow == null)
                return;

            _hostWindow.PreviewDragOver -= OnWindowPreviewDragOver;
            _hostWindow.PreviewDrop -= OnWindowPreviewDrop;
            _windowEventsAttached = false;
            _hostWindow = null;
        }

        /// <summary>
        /// 当鼠标经过主窗口的任意区域时兜底设置拖放效果，避免 Popup 等非同一视觉树元素遮挡导致事件丢失
        /// </summary>
        private void OnWindowPreviewDragOver(object sender, DragEventArgs e)
        {
            var workflowTab = DataContext as Models.WorkflowTab;
            
            // 检查当前 WorkflowTab 是否是活动状态
            if (workflowTab != null && !workflowTab.IsActive)
            {
                return; // 不处理非活动状态的 FlowEditor
            }
            
            // 如果 FlowEditor 禁用了拖放，不处理
            if (!AllowDrop)
                return;
                
            if (_infiniteCanvas == null || !IsValidDragData(e.Data))
                return;

            var screenPoint = GetCursorScreenPoint();
            if (!IsScreenPointInsideFlowEditor(screenPoint))
                return;

            e.Effects = DragDropEffects.Copy;
            e.Handled = true;
        }

        /// <summary>
        /// 窗口级放置兜底，将屏幕坐标转换到画布并调用统一的节点创建逻辑
        /// </summary>
        private void OnWindowPreviewDrop(object sender, DragEventArgs e)
        {
            var workflowTab = DataContext as Models.WorkflowTab;
            System.Diagnostics.Debug.WriteLine($"[FlowEditor.OnWindowPreviewDrop] 收到拖放事件 - WorkflowTab: {workflowTab?.Name ?? "null"}, IsActive: {workflowTab?.IsActive}");
            
            // 检查当前 WorkflowTab 是否是活动状态
            if (workflowTab != null && !workflowTab.IsActive)
            {
                System.Diagnostics.Debug.WriteLine($"[FlowEditor.OnWindowPreviewDrop] 拒绝处理拖放：WorkflowTab '{workflowTab.Name}' 不是活动状态（IsActive={workflowTab.IsActive}）");
                return;
            }
            
            // 如果 FlowEditor 禁用了拖放，不处理
            if (!AllowDrop)
                return;
                
            if (_infiniteCanvas == null)
                return;

            var screenPoint = GetCursorScreenPoint();
            if (!IsScreenPointInsideFlowEditor(screenPoint))
                return;

            System.Diagnostics.Debug.WriteLine($"[FlowEditor.OnWindowPreviewDrop] 处理拖放：WorkflowTab '{workflowTab?.Name ?? "null"}'");
            var pointOnCanvas = _infiniteCanvas.PointFromScreen(screenPoint);
            TryHandleNodeDrop(e, pointOnCanvas);
        }

        /// <summary>
        /// 判断屏幕坐标是否位于当前 FlowEditor 区域
        /// </summary>
        private bool IsScreenPointInsideFlowEditor(System.Windows.Point screenPoint)
        {
            var topLeft = PointToScreen(new System.Windows.Point(0, 0));
            var bounds = new Rect(topLeft, new System.Windows.Size(ActualWidth, ActualHeight));
            return bounds.Contains(screenPoint);
        }


        /// <summary>
        /// 获取当前鼠标的屏幕坐标
        /// </summary>
        private System.Windows.Point GetCursorScreenPoint()
        {
            var p = new System.Drawing.Point();
            GetCursorPos(ref p);
            return new System.Windows.Point(p.X, p.Y);
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool GetCursorPos(ref System.Drawing.Point lpPoint);

        #endregion
    }
}
