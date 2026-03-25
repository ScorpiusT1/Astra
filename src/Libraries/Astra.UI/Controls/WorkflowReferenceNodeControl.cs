using Astra.Core.Nodes.Geometry;
using Astra.Core.Nodes.Models;
using Astra.UI.Commands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace Astra.UI.Controls
{
    /// <summary>
    /// 主流程节点控件 - 用于显示子流程引用节点
    /// </summary>
    public class WorkflowReferenceNodeControl : Control
    {
        private Button _executeButton;
        private Button _pauseResumeButton;
        private Button _switchButton;
        private TextBlock _executionTimeText;
        private TextBlock _titleTextBlock;
        private InfiniteCanvas _parentCanvas;
        private PortControl _portTop;
        private PortControl _portBottom;
        private PortControl _portLeft;
        private PortControl _portRight;
        private ContentPresenter _contentPresenter;
        private ContextMenu _contextMenu;
        private MenuItem _renameMenuItem;
        private MenuItem _deleteMenuItem;
        private MenuItem _toggleEnabledMenuItem;
        private MenuItem _copyMenuItem;

        // 拖拽移动相关字段
        private bool _isDragging;
        private bool _isMouseDown;
        private Point _dragStartMousePosition;
        private Point _dragStartMousePositionRelative;
        private Point2D _dragStartNodePosition;
        private Dictionary<string, Point2D> _selectedNodesInitialPositions;
        private DateTime _mouseDownTime;
        private TranslateTransform _dragTransform;

        /// <summary>
        /// 双击节点事件（用于弹出属性编辑窗口）
        /// </summary>
        public static readonly RoutedEvent NodeDoubleClickEvent =
            EventManager.RegisterRoutedEvent(
                nameof(NodeDoubleClick),
                RoutingStrategy.Bubble,
                typeof(RoutedEventHandler),
                typeof(WorkflowReferenceNodeControl));

        public event RoutedEventHandler NodeDoubleClick
        {
            add => AddHandler(NodeDoubleClickEvent, value);
            remove => RemoveHandler(NodeDoubleClickEvent, value);
        }

        static WorkflowReferenceNodeControl()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(WorkflowReferenceNodeControl),
                new FrameworkPropertyMetadata(typeof(WorkflowReferenceNodeControl)));
        }

        public WorkflowReferenceNodeControl()
        {
            Loaded += WorkflowReferenceNodeControl_Loaded;
            Unloaded += WorkflowReferenceNodeControl_Unloaded;
            DataContextChanged += OnDataContextChanged;

            // 订阅鼠标事件
            PreviewMouseLeftButtonDown += OnPreviewMouseLeftButtonDown;
            PreviewMouseMove += OnPreviewMouseMove;
            PreviewMouseLeftButtonUp += OnPreviewMouseLeftButtonUp;
            PreviewMouseRightButtonDown += OnPreviewMouseRightButtonDown;

            IsHitTestVisible = true;
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            // 获取模板中的控件
            _executeButton = GetTemplateChild("PART_ExecuteButton") as Button;
            _pauseResumeButton = GetTemplateChild("PART_PauseResumeButton") as Button;
            _switchButton = GetTemplateChild("PART_SwitchButton") as Button;
            _executionTimeText = GetTemplateChild("PART_ExecutionTimeText") as TextBlock;
            _titleTextBlock = GetTemplateChild("PART_TitleTextBlock") as TextBlock;
            
            // 获取端口控件
            _portTop = GetTemplateChild("PortTop") as PortControl;
            _portBottom = GetTemplateChild("PortBottom") as PortControl;
            _portLeft = GetTemplateChild("PortLeft") as PortControl;
            _portRight = GetTemplateChild("PortRight") as PortControl;
            
            // 调试：检查端口是否正确获取
            System.Diagnostics.Debug.WriteLine($"[WorkflowReferenceNodeControl] OnApplyTemplate - 端口获取: Top={_portTop != null}, Bottom={_portBottom != null}, Left={_portLeft != null}, Right={_portRight != null}");
            
            // 确保端口拥有稳定的 ID
            EnsurePortIds();
            
            // 初始化右键菜单
            InitializeContextMenu();
            
            // 为端口订阅鼠标事件，确保能捕获点击
            if (_portTop != null)
            {
                _portTop.PreviewMouseLeftButtonDown += OnPortMouseLeftButtonDown;
                _portTop.MouseEnter += OnPortMouseEnter;
            }
            if (_portBottom != null)
            {
                _portBottom.PreviewMouseLeftButtonDown += OnPortMouseLeftButtonDown;
                _portBottom.MouseEnter += OnPortMouseEnter;
            }
            if (_portLeft != null)
            {
                _portLeft.PreviewMouseLeftButtonDown += OnPortMouseLeftButtonDown;
                _portLeft.MouseEnter += OnPortMouseEnter;
            }
            if (_portRight != null)
            {
                _portRight.PreviewMouseLeftButtonDown += OnPortMouseLeftButtonDown;
                _portRight.MouseEnter += OnPortMouseEnter;
            }

            // 订阅按钮事件
            if (_executeButton != null)
                _executeButton.Click += OnExecuteButtonClick;

            if (_pauseResumeButton != null)
                _pauseResumeButton.Click += OnPauseResumeButtonClick;

            if (_switchButton != null)
                _switchButton.Click += OnSwitchButtonClick;

            // 查找父画布（延迟到 Loaded 事件中查找，确保视觉树已完全构建）
            Loaded += (s, args) =>
            {
                _parentCanvas = FindParentCanvas(this);
                if (_parentCanvas == null)
                {
                    System.Diagnostics.Debug.WriteLine("[WorkflowReferenceNodeControl] 警告：无法找到父画布");
                }
            };
        }

        private void WorkflowReferenceNodeControl_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateDataBinding();
        }

        private void WorkflowReferenceNodeControl_Unloaded(object sender, RoutedEventArgs e)
        {
            // 清理资源
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            UpdateDataBinding();
            EnsurePortIds();
        }

        /// <summary>
        /// 为模板端口分配稳定的 PortId（基于节点 Id + 方向），确保切换界面后连线还能找到原端口
        /// </summary>
        private void EnsurePortIds()
        {
            if (DataContext is not Node node)
                return;

            // 使用节点 Id + 方向构造稳定标识
            TryAssignPortId(_portTop, $"{node.Id}:Top");
            TryAssignPortId(_portBottom, $"{node.Id}:Bottom");
            TryAssignPortId(_portLeft, $"{node.Id}:Left");
            TryAssignPortId(_portRight, $"{node.Id}:Right");
        }

        private void TryAssignPortId(PortControl port, string id)
        {
            if (port == null || string.IsNullOrWhiteSpace(id))
                return;

            if (string.IsNullOrWhiteSpace(port.PortId))
            {
                port.PortId = id;
            }
        }

        private void UpdateDataBinding()
        {
            if (DataContext is WorkflowReferenceNode node)
            {
                // 更新标题
                if (_titleTextBlock != null)
                {
                    _titleTextBlock.Text = node.SubWorkflowName ?? node.Name ?? "未命名流程";
                }

                // 同步选中状态
                if (IsSelected != node.IsSelected)
                {
                    IsSelected = node.IsSelected;
                }

                if (_executionTimeText != null)
                {
                    _executionTimeText.Text = node.LastExecutionResult?.Duration.HasValue == true
                        ? $"{node.LastExecutionResult.Duration.Value.TotalMilliseconds:F2} ms"
                        : "0.00 ms";
                }
            }
            else if (DataContext is Node node2)
            {
                // 也支持普通的 Node 类型
                // 同步选中状态
                if (IsSelected != node2.IsSelected)
                {
                    IsSelected = node2.IsSelected;
                }
            }
        }

        #region 按钮事件处理

        private void OnExecuteButtonClick(object sender, RoutedEventArgs e)
        {
            if (DataContext is WorkflowReferenceNode workflowNode && !workflowNode.IsEnabled)
            {
                System.Diagnostics.Debug.WriteLine("[WorkflowReferenceNodeControl] 当前子流程节点已禁用，忽略执行请求");
                return;
            }

            var subWorkflowId = GetSubWorkflowId();
            if (string.IsNullOrWhiteSpace(subWorkflowId))
            {
                return;
            }

            // 切换到目标子流程 Tab，并仅切换该子流程的执行状态
            SwitchToSubWorkflowTab();
            var host = FindWorkflowReferenceNodeHost();
            host?.ToggleRunSubWorkflowFromNode(subWorkflowId);
        }

        private void OnPauseResumeButtonClick(object sender, RoutedEventArgs e)
        {
            var subWorkflowId = GetSubWorkflowId();
            if (string.IsNullOrWhiteSpace(subWorkflowId))
            {
                return;
            }

            // 切换到目标子流程 Tab，并仅暂停/恢复该子流程
            SwitchToSubWorkflowTab();
            var host = FindWorkflowReferenceNodeHost();
            host?.TogglePauseResumeSubWorkflowFromNode(subWorkflowId);
        }

        private void OnSwitchButtonClick(object sender, RoutedEventArgs e)
        {
            SwitchToSubWorkflowTab();
        }

        private void SwitchToSubWorkflowTab()
        {
            var subWorkflowId = GetSubWorkflowId();

            if (string.IsNullOrEmpty(subWorkflowId))
            {
                System.Diagnostics.Debug.WriteLine($"[WorkflowReferenceNodeControl] 警告：无法获取子流程 ID");
                return;
            }

            var host = FindWorkflowReferenceNodeHost();
            if (host != null)
            {
                host.OpenSubWorkflowEditor(subWorkflowId);
                System.Diagnostics.Debug.WriteLine($"[WorkflowReferenceNodeControl] 切换到子流程编辑器: {subWorkflowId}");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[WorkflowReferenceNodeControl] 警告：无法找到 IWorkflowReferenceNodeHost 宿主");
            }
        }

        private string GetSubWorkflowId()
        {
            if (DataContext is WorkflowReferenceNode workflowNode)
            {
                return workflowNode.SubWorkflowId;
            }

            if (DataContext is Node node && node is WorkflowReferenceNode refNode)
            {
                return refNode.SubWorkflowId;
            }

            return null;
        }

        #endregion

        #region 拖拽移动逻辑（参考 NodeControl）

        private void OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _parentCanvas ??= FindParentCanvas(this);

            // 优先处理双击：与普通节点一致，双击直接触发属性编辑
            if (e.ClickCount == 2)
            {
                RaiseEvent(new RoutedEventArgs(NodeDoubleClickEvent, this));
                e.Handled = true;
                return;
            }

            // 如果点击在端口上，优先进入"连线"模式而不是拖拽
            // 注意：端口现在有直接的事件处理，这里作为备用检查
            var hitPort = FindHitPort(e.OriginalSource as DependencyObject);
            if (hitPort != null)
            {
                System.Diagnostics.Debug.WriteLine($"[WorkflowReferenceNodeControl] PreviewMouseLeftButtonDown 检测到端口点击: {hitPort.GetType().Name}, PortId: {(hitPort as PortControl)?.PortId ?? "null"}");
                if (DataContext is Node portNode)
                {
                    System.Diagnostics.Debug.WriteLine($"[WorkflowReferenceNodeControl] 开始连线，节点: {portNode.Name}, 画布: {(_parentCanvas != null ? "已找到" : "未找到")}");
                    if (_parentCanvas != null)
                    {
                        _parentCanvas.BeginConnection(portNode, hitPort);
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("[WorkflowReferenceNodeControl] 错误：无法找到父画布，无法开始连线");
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("[WorkflowReferenceNodeControl] 错误：DataContext 不是 Node 类型");
                }
                e.Handled = true;
                return;
            }

            // 检查是否点击在按钮（或按钮内部元素）上，如果是则不允许拖拽
            // 注意：OriginalSource 往往是图标/Border，而不是 Button 本身
            var sourceObject = e.OriginalSource as DependencyObject;
            if (sourceObject != null &&
                FindVisualParent<System.Windows.Controls.Primitives.ButtonBase>(sourceObject) != null)
            {
                return;
            }

            if (_parentCanvas == null || _parentCanvas.IsLocked)
                return;

            if (DataContext is not Node node)
                return;

            // 如果按下了 Ctrl 键，让 InfiniteCanvas 处理平移，不捕获鼠标
            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                return;  // 不处理，让事件继续传播到 InfiniteCanvas
            }

            // 准备拖拽节点
            if (!_isDragging)
            {
                _parentCanvas = FindParentCanvas(this);
                _contentPresenter = FindParentContentPresenter(this);

                if (_parentCanvas == null || _contentPresenter == null)
                {
                    return;
                }

                // 处理节点选中逻辑
                if (node != null)
                {
                    // 如果当前节点未选中，则选中它
                    if (!node.IsSelected)
                    {
                        ClearOtherNodesSelection(node);
                        node.IsSelected = true;

                        // 将当前节点添加到 SelectedItems 集合
                        if (_parentCanvas.SelectedItems != null)
                        {
                            _parentCanvas.SelectedItems.Clear();
                            _parentCanvas.SelectedItems.Add(node);
                        }

                        // 同步视觉状态
                        IsSelected = true;
                    }
                    else
                    {
                        // 如果当前节点已经被选中，确保它在 SelectedItems 中
                        if (_parentCanvas.SelectedItems != null && !_parentCanvas.SelectedItems.Contains(node))
                        {
                            _parentCanvas.SelectedItems.Add(node);
                        }
                    }
                }

                // 记录鼠标按下状态和位置
                _isMouseDown = true;
                _mouseDownTime = DateTime.Now;
                _dragStartMousePosition = e.GetPosition(_parentCanvas);
                _dragStartMousePositionRelative = e.GetPosition(this);

                // 获取当前节点在画布坐标系中的位置
                _dragStartNodePosition = GetCurrentCanvasPosition();

                // 捕获鼠标
                CaptureMouse();

                // 阻止事件冒泡（重要：防止 InfiniteCanvas 开始框选）
                e.Handled = true;
            }
        }

        private void OnPreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (_parentCanvas == null || _contentPresenter == null)
                return;

            // 只有鼠标被捕获时才处理
            if (!IsMouseCaptured)
                return;

            // 获取当前鼠标位置
            var currentMousePosition = e.GetPosition(_parentCanvas);
            var deltaX = currentMousePosition.X - _dragStartMousePosition.X;
            var deltaY = currentMousePosition.Y - _dragStartMousePosition.Y;
            var distance = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);

            // 如果还没有开始拖拽，检查是否应该开始
            if (_isMouseDown && !_isDragging)
            {
                // 移动距离超过阈值，开始拖拽
                if (distance > SystemParameters.MinimumHorizontalDragDistance)
                {
                    StartDragging();
                }
                else
                {
                    return; // 距离不够，不处理
                }
            }

            // 如果正在拖拽，直接更新节点位置（实时刷新连线）
            if (_isDragging)
            {
                var canvasDeltaX = deltaX / _parentCanvas.Scale;
                var canvasDeltaY = deltaY / _parentCanvas.Scale;

                var currentNode = DataContext as Node;
                if (currentNode != null)
                {
                    var newPos = new Point2D(_dragStartNodePosition.X + canvasDeltaX, _dragStartNodePosition.Y + canvasDeltaY);
                    UpdateNodePosition(newPos);
                    Canvas.SetLeft(_contentPresenter, newPos.X);
                    Canvas.SetTop(_contentPresenter, newPos.Y);

                    // 实时显示对齐辅助线
                    var nodeSize = GetNodeRenderSize(currentNode);
                    var movingBounds = new Rect(newPos.X, newPos.Y, nodeSize.Width, nodeSize.Height);
                    _parentCanvas?.UpdateAlignmentGuides(currentNode, movingBounds);

                    // 多选同步移动
                    if (_parentCanvas.SelectedItems != null && _parentCanvas.SelectedItems.Count > 1)
                    {
                        foreach (var item in _parentCanvas.SelectedItems)
                        {
                            if (item is Node node && node != currentNode)
                            {
                                // 从初始位置字典中获取原始位置
                                if (_selectedNodesInitialPositions != null && _selectedNodesInitialPositions.TryGetValue(node.Id, out var initialPos))
                                {
                                    var newOtherPos = new Point2D(
                                        initialPos.X + canvasDeltaX,
                                        initialPos.Y + canvasDeltaY);

                                    // 更新位置
                                    node.Position = newOtherPos;

                                    var otherContainer = FindItemsControl(_parentCanvas)?
                                        .ItemContainerGenerator.ContainerFromItem(node) as ContentPresenter;
                                    if (otherContainer != null)
                                    {
                                        Canvas.SetLeft(otherContainer, newOtherPos.X);
                                        Canvas.SetTop(otherContainer, newOtherPos.Y);
                                    }
                                }
                            }
                        }
                    }

                    // 实时刷新连线（连线会基于节点位置自动重新计算）
                    // 优化：仅当拖动的是已连接的节点时才刷新连线
                    // 检查当前移动的节点（或选中的其他节点）是否有关联的连线
                    bool needRefresh = false;
                    if (_parentCanvas.EdgeItemsSource != null)
                    {
                        var allEdges = _parentCanvas.EdgeItemsSource.OfType<Edge>().ToList();
                        var movedNodes = new List<string> { currentNode.Id };
                        
                        if (_parentCanvas.SelectedItems != null)
                        {
                            foreach (var item in _parentCanvas.SelectedItems)
                            {
                                if (item is Node node)
                                    movedNodes.Add(node.Id);
                            }
                        }

                        // 如果任何移动的节点是某条连线的起点或终点，则需要刷新
                        if (allEdges.Any(edge => movedNodes.Contains(edge.SourceNodeId) || movedNodes.Contains(edge.TargetNodeId)))
                        {
                            needRefresh = true;
                        }
                    }

                    if (needRefresh)
                    {
                        _parentCanvas.RefreshEdges();
                    }
                    
                    // 🗺️ 请求更新小地图（带节流，避免性能问题）
                    _parentCanvas?.RequestMinimapUpdate();
                }
            }
        }

        private void OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            var sourceObject = e.OriginalSource as DependencyObject;
            var releasedOnButton = sourceObject != null &&
                                   FindVisualParent<System.Windows.Controls.Primitives.ButtonBase>(sourceObject) != null;

            if (_isMouseDown || _isDragging)
            {
                // 如果正在拖拽，结束拖拽并同步最终位置
                if (_isDragging)
                {
                    EndDragging();
                }

                // 重置状态
                _isMouseDown = false;
                _isDragging = false;

                if (IsMouseCaptured)
                {
                    ReleaseMouseCapture();
                }

                Cursor = Cursors.Arrow;
                // 在按钮上释放时不吞事件，确保 Click 正常触发
                if (!releasedOnButton)
                {
                    e.Handled = true;
                }

                // 结束拖拽后隐藏对齐线
                _parentCanvas?.HideAlignmentLines();
            }
        }

        /// <summary>
        /// 初始化右键菜单，将重命名与删除放入同一菜单
        /// </summary>
        private void InitializeContextMenu()
        {
            // 统一使用主题菜单样式
            var contextMenuStyle = TryFindResource("ThemedContextMenu") as Style;
            var menuItemStyle = TryFindResource("ThemedMenuItem") as Style;

            if (_contextMenu == null)
            {
                _contextMenu = new ContextMenu();
                if (contextMenuStyle != null)
                    _contextMenu.Style = contextMenuStyle;

                // 重命名
                _renameMenuItem = new MenuItem
                {
                    Header = "重命名"
                };
                if (menuItemStyle != null)
                    _renameMenuItem.Style = menuItemStyle;
                _renameMenuItem.Click += (s, e) =>
                {
                    // 流程节点不支持重命名，可以留空或显示提示
                    // 如果需要重命名，应该重命名对应的子流程
                };
                _contextMenu.Items.Add(_renameMenuItem);

                // 复制
                _copyMenuItem = new MenuItem
                {
                    Header = "复制"
                };
                if (menuItemStyle != null)
                    _copyMenuItem.Style = menuItemStyle;
                _copyMenuItem.Click += OnCopyMenuItemClick;
                _contextMenu.Items.Add(_copyMenuItem);

                // 粘贴
                var pasteMenuItem = new MenuItem
                {
                    Header = "粘贴"
                };
                if (menuItemStyle != null)
                    pasteMenuItem.Style = menuItemStyle;
                pasteMenuItem.Click += OnPasteMenuItemClick;
                _contextMenu.Items.Add(pasteMenuItem);

                // 分隔符
                _contextMenu.Items.Add(new Separator());

                // 启用/禁用
                _toggleEnabledMenuItem = new MenuItem
                {
                    Header = "禁用"
                };
                if (menuItemStyle != null)
                    _toggleEnabledMenuItem.Style = menuItemStyle;
                _toggleEnabledMenuItem.Click += OnToggleEnabledMenuItemClick;
                _contextMenu.Items.Add(_toggleEnabledMenuItem);

                // 分隔符
                _contextMenu.Items.Add(new Separator());

                // 删除
                _deleteMenuItem = new MenuItem
                {
                    Header = "删除",
                    Tag = "Danger"  // 标记为危险操作
                };
                if (menuItemStyle != null)
                    _deleteMenuItem.Style = menuItemStyle;
                _deleteMenuItem.Click += OnDeleteMenuItemClick;
                _contextMenu.Items.Add(_deleteMenuItem);

                // 在菜单打开时，自动选中当前节点并更新菜单项状态
                _contextMenu.Opened += (s, e) =>
                {
                    EnsureSelectedForAction();
                    UpdateContextMenuItems();
                };
            }

            // 统一右键菜单，避免画布和节点出现不同菜单
            if (_contextMenu != null)
            {
                _contextMenu.PlacementTarget = this;
            }

            // 确保控件绑定该菜单
            ContextMenu = _contextMenu;
        }

        /// <summary>
        /// 更新右键菜单项的状态（如启用/禁用文本）
        /// </summary>
        private void UpdateContextMenuItems()
        {
            if (DataContext is not Node node)
                return;

            // 更新启用/禁用菜单项文本
            if (_toggleEnabledMenuItem != null)
            {
                _toggleEnabledMenuItem.Header = node.IsEnabled ? "禁用" : "启用";
            }
        }

        private void OnPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            // 如果画布当前存在多选组框，则右键应由组框/画布处理，节点不再抢占
            _parentCanvas ??= FindParentCanvas(this);
            if (_parentCanvas != null && _parentCanvas.SelectedItems != null && _parentCanvas.SelectedItems.Count > 1)
            {
                // 允许事件继续冒泡到 InfiniteCanvas / FlowEditor，由组框菜单接管
                return;
            }

            // 选中当前节点
            EnsureSelectedForAction();

            // 打开节点自己的菜单
            if (_contextMenu != null)
            {
                _contextMenu.PlacementTarget = this;
                _contextMenu.IsOpen = true;
            }

            // 阻止事件冒泡，防止触发画布右键菜单
            e.Handled = true;
        }

        /// <summary>
        /// 确保当前节点已选中；未选中则选中并直接返回 true
        /// </summary>
        private bool EnsureSelectedForAction()
        {
            _parentCanvas ??= FindParentCanvas(this);
            if (_parentCanvas == null || DataContext is not Node node)
                return false;

            if (!node.IsSelected)
            {
                // 先清除其他节点选中，再选中当前节点
                ClearOtherNodesSelection(node);
                node.IsSelected = true;
                IsSelected = true;

                if (_parentCanvas.SelectedItems != null)
                {
                    _parentCanvas.SelectedItems.Clear();
                    _parentCanvas.SelectedItems.Add(node);
                }
            }

            return true;
        }

        /// <summary>
        /// 右键菜单删除选中节点（委托给 InfiniteCanvas 统一处理）
        /// </summary>
        private void OnDeleteMenuItemClick(object sender, RoutedEventArgs e)
        {
            if (!EnsureSelectedForAction())
                return;

            _parentCanvas ??= FindParentCanvas(this);
            if (_parentCanvas == null)
                return;

            // 委托给 InfiniteCanvas 的统一删除方法
            _parentCanvas.DeleteSelectedItems();
        }

        /// <summary>
        /// 右键菜单启用/禁用节点
        /// </summary>
        private void OnToggleEnabledMenuItemClick(object sender, RoutedEventArgs e)
        {
            if (!EnsureSelectedForAction())
                return;

            _parentCanvas ??= FindParentCanvas(this);
            if (_parentCanvas == null)
                return;

            var node = DataContext as Node;
            if (node == null)
                return;

            // 获取所有选中的节点
            var selectedNodes = new List<Node>();
            if (_parentCanvas.SelectedItems != null && _parentCanvas.SelectedItems.Count > 0)
            {
                foreach (var item in _parentCanvas.SelectedItems)
                {
                    if (item is Node n)
                        selectedNodes.Add(n);
                }
            }
            else
            {
                selectedNodes.Add(node);
            }

            // 判断新状态：如果当前节点已启用，则禁用；否则启用
            var newState = !node.IsEnabled;

            // 使用撤销/重做命令
            var undoManager = _parentCanvas.UndoRedoManager;
            if (undoManager != null)
            {
                var command = new ToggleNodeEnabledCommand(selectedNodes, newState);
                undoManager.Do(command);
            }
            else
            {
                // 无撤销管理器，直接应用
                foreach (var n in selectedNodes)
                {
                    n.IsEnabled = newState;
                }
            }
        }

        /// <summary>
        /// 右键菜单复制节点到剪贴板（委托给 FlowEditor 处理以支持跨流程复制）
        /// </summary>
        private void OnCopyMenuItemClick(object sender, RoutedEventArgs e)
        {
            if (!EnsureSelectedForAction())
                return;

            _parentCanvas ??= FindParentCanvas(this);
            if (_parentCanvas == null)
                return;

            // 查找父级 FlowEditor
            var flowEditor = FindParentFlowEditor(this);
            if (flowEditor != null)
            {
                flowEditor.ExecuteCopyFromNodeContextMenu(sender, e);
                System.Diagnostics.Debug.WriteLine($"[WorkflowReferenceNodeControl] 已委托给 FlowEditor 处理复制操作");
                return;
            }

            // 后备方案：使用本地剪贴板
            System.Diagnostics.Debug.WriteLine($"[WorkflowReferenceNodeControl] 未找到 FlowEditor，使用本地剪贴板");

            // 获取所有选中的节点（保存原始节点，不克隆）
            var selectedNodes = new List<Node>();
            if (_parentCanvas.SelectedItems != null && _parentCanvas.SelectedItems.Count > 0)
            {
                foreach (var item in _parentCanvas.SelectedItems)
                {
                    if (item is Node n)
                        selectedNodes.Add(n);
                }
            }
            else if (DataContext is Node node)
            {
                selectedNodes.Add(node);
            }

            if (selectedNodes.Count == 0)
                return;

            // 保存到剪贴板（不克隆）
            _parentCanvas.ClipboardNodes = selectedNodes;

            // 计算并保存边界框（用于保持节点间的相对位置）
            double minX = double.MaxValue, minY = double.MaxValue;
            double maxX = double.MinValue, maxY = double.MinValue;

            foreach (var node in selectedNodes)
            {
                if (node.Position != null)
                {
                    var nodeWidth = node.Size.IsEmpty ? 200 : node.Size.Width;
                    var nodeHeight = node.Size.IsEmpty ? 150 : node.Size.Height;
                    
                    minX = Math.Min(minX, node.Position.X);
                    minY = Math.Min(minY, node.Position.Y);
                    maxX = Math.Max(maxX, node.Position.X + nodeWidth);
                    maxY = Math.Max(maxY, node.Position.Y + nodeHeight);
                }
            }

            if (minX != double.MaxValue)
            {
                _parentCanvas.ClipboardBounds = new Rect(minX, minY, maxX - minX, maxY - minY);
            }
        }

        /// <summary>
        /// 右键菜单粘贴节点（委托给 FlowEditor 处理以支持跨流程粘贴）
        /// </summary>
        private void OnPasteMenuItemClick(object sender, RoutedEventArgs e)
        {
            _parentCanvas ??= FindParentCanvas(this);
            if (_parentCanvas == null)
                return;

            // 查找父级 FlowEditor
            var flowEditor = FindParentFlowEditor(this);
            if (flowEditor != null)
            {
                flowEditor.ExecutePasteFromNodeContextMenu(sender, e);
                System.Diagnostics.Debug.WriteLine($"[WorkflowReferenceNodeControl] 已委托给 FlowEditor 处理粘贴操作");
                return;
            }

            // 后备方案：使用本地剪贴板
            System.Diagnostics.Debug.WriteLine($"[WorkflowReferenceNodeControl] 未找到 FlowEditor，粘贴操作失败");
        }


        #endregion

        #region 依赖属性

        public static readonly DependencyProperty SubWorkflowNameProperty =
            DependencyProperty.Register(
                nameof(SubWorkflowName),
                typeof(string),
                typeof(WorkflowReferenceNodeControl),
                new PropertyMetadata("未命名流程", OnSubWorkflowNameChanged));

        public string SubWorkflowName
        {
            get => (string)GetValue(SubWorkflowNameProperty);
            set => SetValue(SubWorkflowNameProperty, value);
        }

        private static void OnSubWorkflowNameChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is WorkflowReferenceNodeControl control && control._titleTextBlock != null)
            {
                control._titleTextBlock.Text = e.NewValue as string ?? "未命名流程";
            }
        }

        public static readonly DependencyProperty ExecutionCountProperty =
            DependencyProperty.Register(
                nameof(ExecutionCount),
                typeof(int),
                typeof(WorkflowReferenceNodeControl),
                new PropertyMetadata(0, OnExecutionCountChanged));

        public int ExecutionCount
        {
            get => (int)GetValue(ExecutionCountProperty);
            set => SetValue(ExecutionCountProperty, value);
        }

        private static void OnExecutionCountChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            // 执行次数 UI 已在精简样式中移除，保留属性用于兼容旧数据绑定。
        }

        public static readonly DependencyProperty ExecutionTimeProperty =
            DependencyProperty.Register(
                nameof(ExecutionTime),
                typeof(string),
                typeof(WorkflowReferenceNodeControl),
                new PropertyMetadata("0.00ms", OnExecutionTimeChanged));

        public string ExecutionTime
        {
            get => (string)GetValue(ExecutionTimeProperty);
            set => SetValue(ExecutionTimeProperty, value);
        }

        private static void OnExecutionTimeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is WorkflowReferenceNodeControl control && control._executionTimeText != null)
            {
                control._executionTimeText.Text = e.NewValue as string ?? "0.00ms";
            }
        }

        public static readonly DependencyProperty IsEnabledProperty =
            DependencyProperty.Register(
                nameof(IsEnabled),
                typeof(bool),
                typeof(WorkflowReferenceNodeControl),
                new PropertyMetadata(true, OnIsEnabledChanged));

        public bool IsEnabled
        {
            get => (bool)GetValue(IsEnabledProperty);
            set => SetValue(IsEnabledProperty, value);
        }

        private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            // 启用开关 UI 已在精简样式中移除，保留属性用于兼容旧数据绑定。
        }

        public static readonly DependencyProperty IsSelectedProperty =
            DependencyProperty.Register(
                nameof(IsSelected),
                typeof(bool),
                typeof(WorkflowReferenceNodeControl),
                new PropertyMetadata(false, OnIsSelectedChanged));

        public bool IsSelected
        {
            get => (bool)GetValue(IsSelectedProperty);
            set => SetValue(IsSelectedProperty, value);
        }

        private static void OnIsSelectedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = d as WorkflowReferenceNodeControl;
            if (control == null)
                return;
            
            var isSelected = (bool)e.NewValue;
            var node = control.DataContext as Node;
            
            // 同步数据模型的选中状态
            if (node != null && node.IsSelected != isSelected)
            {
                node.IsSelected = isSelected;
            }
        }

        #endregion

        #region 拖拽辅助方法

        private void StartDragging()
        {
            if (_parentCanvas == null || _contentPresenter == null)
                return;

            _isDragging = true;
            Cursor = Cursors.Hand;

            // 记录所有选中节点的初始位置
            _selectedNodesInitialPositions = new Dictionary<string, Point2D>();
            if (_parentCanvas.SelectedItems != null)
            {
                foreach (var item in _parentCanvas.SelectedItems)
                {
                    if (item is Node node)
                    {
                        _selectedNodesInitialPositions[node.Id] = node.Position;
                    }
                }
            }
        }

        private void EndDragging()
        {
            if (_parentCanvas == null || _contentPresenter == null)
                return;

            // 使用与 OnPreviewMouseMove 相同的逻辑计算最终位置
            var currentMousePosition = Mouse.GetPosition(_parentCanvas);
            var deltaX = currentMousePosition.X - _dragStartMousePosition.X;
            var deltaY = currentMousePosition.Y - _dragStartMousePosition.Y;

            var canvasDeltaX = deltaX / _parentCanvas.Scale;
            var canvasDeltaY = deltaY / _parentCanvas.Scale;

            var finalCanvasPosition = new Point2D(
                _dragStartNodePosition.X + canvasDeltaX,
                _dragStartNodePosition.Y + canvasDeltaY
            );

            // 获取当前节点
            var currentNode = DataContext as Node;

            // 对齐吸附（松开后自动对齐）
            if (currentNode != null)
            {
                var nodeSize = GetNodeRenderSize(currentNode);
                var snap = _parentCanvas?.CalculateAlignmentSnap(currentNode, new Rect(finalCanvasPosition.X, finalCanvasPosition.Y, nodeSize.Width, nodeSize.Height));
                if (snap.HasValue)
                {
                    finalCanvasPosition = new Point2D(finalCanvasPosition.X + snap.Value.dx, finalCanvasPosition.Y + snap.Value.dy);
                    canvasDeltaX += snap.Value.dx;
                    canvasDeltaY += snap.Value.dy;
                }
            }

            // 计算画布坐标系中的偏移量（用于多选移动）
            var offsetX = canvasDeltaX;
            var offsetY = canvasDeltaY;

            // 如果当前节点被选中，且有多选，则一起移动所有选中的节点
            if (currentNode != null &&
                currentNode.IsSelected &&
                _parentCanvas.SelectedItems != null &&
                _parentCanvas.SelectedItems.Count > 1)
            {
                var itemsControl = FindItemsControl(_parentCanvas);

                foreach (var item in _parentCanvas.SelectedItems)
                {
                    if (item is Node selectedNode && selectedNode.Position != null)
                    {
                        if (selectedNode == currentNode)
                        {
                            Canvas.SetLeft(_contentPresenter, finalCanvasPosition.X);
                            Canvas.SetTop(_contentPresenter, finalCanvasPosition.Y);
                            UpdateNodePosition(finalCanvasPosition);
                        }
                        else
                        {
                            // 其他选中节点：从初始位置开始偏移
                            if (_selectedNodesInitialPositions != null && _selectedNodesInitialPositions.TryGetValue(selectedNode.Id, out var initialPos))
                            {
                                var newPosition = new Point2D(
                                    initialPos.X + offsetX,
                                    initialPos.Y + offsetY
                                );

                                if (itemsControl != null)
                                {
                                    var container = itemsControl.ItemContainerGenerator.ContainerFromItem(selectedNode) as ContentPresenter;
                                    if (container != null)
                                    {
                                        Canvas.SetLeft(container, newPosition.X);
                                        Canvas.SetTop(container, newPosition.Y);
                                    }
                                }

                                selectedNode.Position = newPosition;
                            }
                        }
                    }
                }
            }
            else if (currentNode != null)
            {
                // 单个节点拖动
                Canvas.SetLeft(_contentPresenter, finalCanvasPosition.X);
                Canvas.SetTop(_contentPresenter, finalCanvasPosition.Y);
                UpdateNodePosition(finalCanvasPosition);
            }

            // 拖拽结束后刷新连线
            _parentCanvas?.RefreshEdgesImmediate();
        }

        private void UpdateNodePosition(Point2D newPosition)
        {
            if (DataContext is Node node)
            {
                node.Position = newPosition;
            }
        }

        private Point2D GetCurrentCanvasPosition()
        {
            if (DataContext is Node node)
            {
                return node.Position;
            }
            return Point2D.Zero;
        }

        private Size GetNodeRenderSize(Node node)
        {
            double width = ActualWidth;
            double height = ActualHeight;

            if (_contentPresenter != null)
            {
                if (width <= 0) width = _contentPresenter.ActualWidth;
                if (height <= 0) height = _contentPresenter.ActualHeight;
            }

            if (node != null && node.Size.IsEmpty == false)
            {
                if (width <= 0) width = node.Size.Width;
                if (height <= 0) height = node.Size.Height;
            }

            // 兜底尺寸
            if (width <= 0) width = 200;
            if (height <= 0) height = 150;

            return new Size(width, height);
        }

        private void ClearOtherNodesSelection(Node currentNode)
        {
            if (_parentCanvas == null || _parentCanvas.ItemsSource == null)
                return;

            var itemsControl = FindItemsControl(_parentCanvas);

            foreach (var item in _parentCanvas.ItemsSource)
            {
                if (item is Node otherNode && otherNode != currentNode)
                {
                    otherNode.IsSelected = false;

                    if (itemsControl != null)
                    {
                        var container = itemsControl.ItemContainerGenerator.ContainerFromItem(otherNode) as ContentPresenter;
                        if (container != null && VisualTreeHelper.GetChildrenCount(container) > 0)
                        {
                            if (VisualTreeHelper.GetChild(container, 0) is WorkflowReferenceNodeControl nodeControl)
                            {
                                nodeControl.IsSelected = false;
                            }
                        }
                    }
                }
            }
        }

        private PortControl FindHitPort(DependencyObject hitElement)
        {
            if (hitElement == null)
                return null;

            var current = hitElement;
            while (current != null && current != this)
            {
                if (current is PortControl port)
                {
                    System.Diagnostics.Debug.WriteLine($"[WorkflowReferenceNodeControl] 找到端口: {port.PortId ?? "未设置ID"}, 可见性: {port.Visibility}, 不透明度: {port.Opacity}");
                    return port;
                }
                current = VisualTreeHelper.GetParent(current);
            }
            
            // 如果没找到，检查是否是端口相关的元素
            System.Diagnostics.Debug.WriteLine($"[WorkflowReferenceNodeControl] 未找到端口，点击元素: {hitElement.GetType().Name}");
            return null;
        }

        private InfiniteCanvas FindParentCanvas(DependencyObject element)
        {
            // 优先走视觉树查找
            var parent = VisualTreeHelper.GetParent(element);
            while (parent != null)
            {
                if (parent is InfiniteCanvas canvas)
                {
                    return canvas;
                }
                parent = VisualTreeHelper.GetParent(parent);
            }

            // 如果视觉树未找到（部分情况下视觉树尚未完全生成），尝试逻辑树
            parent = LogicalTreeHelper.GetParent(element);
            while (parent != null)
            {
                if (parent is InfiniteCanvas canvas)
                {
                    return canvas;
                }
                parent = LogicalTreeHelper.GetParent(parent);
            }

            // 最后尝试通过模板父级获取
            if (element is FrameworkElement fe)
            {
                if (fe.TemplatedParent is InfiniteCanvas templatedCanvas)
                {
                    return templatedCanvas;
                }

                // 尝试从 TemplatedParent 再向上找
                parent = fe.TemplatedParent;
                while (parent != null)
                {
                    if (parent is InfiniteCanvas canvas)
                        return canvas;
                    parent = VisualTreeHelper.GetParent(parent) ?? LogicalTreeHelper.GetParent(parent);
                }
            }

            return null;
        }

        private ContentPresenter FindParentContentPresenter(DependencyObject element)
        {
            var parent = VisualTreeHelper.GetParent(element);
            while (parent != null)
            {
                if (parent is ContentPresenter presenter)
                {
                    return presenter;
                }
                parent = VisualTreeHelper.GetParent(parent);
            }
            return null;
        }

        private ItemsControl FindItemsControl(DependencyObject element)
        {
            if (element == null)
                return null;

            if (element is ItemsControl selfItems)
                return selfItems;

            var parent = VisualTreeHelper.GetParent(element);
            while (parent != null)
            {
                if (parent is ItemsControl parentItems)
                {
                    return parentItems;
                }
                parent = VisualTreeHelper.GetParent(parent);
            }

            return null;
        }

        #endregion

        #region 鼠标事件（显示/隐藏端口）

        protected override void OnMouseEnter(MouseEventArgs e)
        {
            base.OnMouseEnter(e);
            ShowPorts = true;
            System.Diagnostics.Debug.WriteLine($"[WorkflowReferenceNodeControl] 鼠标进入，显示端口。端口状态: Top={_portTop != null}, Bottom={_portBottom != null}, Left={_portLeft != null}, Right={_portRight != null}");
        }

        protected override void OnMouseLeave(MouseEventArgs e)
        {
            base.OnMouseLeave(e);
            // 只有当鼠标真正离开节点区域时才隐藏端口
            // 如果鼠标移动到端口上，不应该隐藏
            var mousePos = e.GetPosition(this);
            var bounds = new Rect(0, 0, ActualWidth, ActualHeight);
            if (!bounds.Contains(mousePos))
            {
                ShowPorts = false;
            }
        }

        /// <summary>
        /// 查找父级 FlowEditor
        /// </summary>
        private FlowEditor FindParentFlowEditor(DependencyObject element)
        {
            // 优先走视觉树查找
            var parent = VisualTreeHelper.GetParent(element);
            while (parent != null)
            {
                if (parent is FlowEditor flowEditor)
                {
                    return flowEditor;
                }
                parent = VisualTreeHelper.GetParent(parent);
            }

            // 如果视觉树未找到，尝试逻辑树
            parent = LogicalTreeHelper.GetParent(element);
            while (parent != null)
            {
                if (parent is FlowEditor flowEditor)
                {
                    return flowEditor;
                }
                parent = LogicalTreeHelper.GetParent(parent);
            }

            // 最后尝试通过模板父级获取
            if (element is FrameworkElement fe)
            {
                if (fe.TemplatedParent is FlowEditor templatedFlowEditor)
                {
                    return templatedFlowEditor;
                }

                // 尝试从 TemplatedParent 再向上找
                parent = fe.TemplatedParent;
                while (parent != null)
                {
                    if (parent is FlowEditor flowEditor)
                        return flowEditor;
                    parent = VisualTreeHelper.GetParent(parent) ?? LogicalTreeHelper.GetParent(parent);
                }
            }

            return null;
        }

        /// <summary>
        /// 端口鼠标进入事件 - 确保端口可见
        /// </summary>
        private void OnPortMouseEnter(object sender, MouseEventArgs e)
        {
            ShowPorts = true;
            System.Diagnostics.Debug.WriteLine($"[WorkflowReferenceNodeControl] 鼠标进入端口: {((PortControl)sender)?.PortId ?? "未知"}");
        }

        /// <summary>
        /// 端口鼠标左键按下事件 - 直接处理连线
        /// </summary>
        private void OnPortMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is PortControl port && DataContext is Node node)
            {
                System.Diagnostics.Debug.WriteLine($"[WorkflowReferenceNodeControl] 端口点击: PortId={port.PortId}, Node={node.Name}");
                _parentCanvas ??= FindParentCanvas(this);
                if (_parentCanvas != null)
                {
                    System.Diagnostics.Debug.WriteLine($"[WorkflowReferenceNodeControl] 开始连线，节点: {node.Name}");
                    _parentCanvas.BeginConnection(node, port);
                    e.Handled = true;
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("[WorkflowReferenceNodeControl] 错误：无法找到父画布");
                }
            }
        }

        #endregion

        #region 辅助方法

        private static T FindVisualParent<T>(DependencyObject child) where T : DependencyObject
        {
            var parentObject = VisualTreeHelper.GetParent(child);
            if (parentObject == null)
                return null;

            if (parentObject is T parent)
                return parent;

            return FindVisualParent<T>(parentObject);
        }

        private IWorkflowReferenceNodeHost FindWorkflowReferenceNodeHost()
        {
            DependencyObject current = this;
            while (current != null)
            {
                if (current is FrameworkElement fe && fe.DataContext is IWorkflowReferenceNodeHost host)
                {
                    return host;
                }

                current = VisualTreeHelper.GetParent(current);
            }

            return null;
        }

        #endregion

        #region ShowPorts 依赖属性

        public static readonly DependencyProperty ShowPortsProperty =
            DependencyProperty.Register(
                nameof(ShowPorts),
                typeof(bool),
                typeof(WorkflowReferenceNodeControl),
                new PropertyMetadata(false));

        public bool ShowPorts
        {
            get => (bool)GetValue(ShowPortsProperty);
            set => SetValue(ShowPortsProperty, value);
        }

        #endregion
    }
}

