using Astra.Core.Nodes.Geometry;
using Astra.Core.Nodes.Models;
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
using System.Windows.Media;

namespace Astra.UI.Controls
{
    /// <summary>
    /// 流程节点控件
    /// </summary>
    public class NodeControl : Control
    {
        private TextBox _editTextBox;
        private TextBlock _titleTextBlock;
        private PortControl _portTop;
        private PortControl _portBottom;
        private PortControl _portLeft;
        private PortControl _portRight;
        private ContextMenu _contextMenu;
        private MenuItem _renameMenuItem;
        private MenuItem _deleteMenuItem;
        private MenuItem _toggleEnabledMenuItem;
        private MenuItem _copyMenuItem;
        
        // 拖拽移动相关字段
        private bool _isDragging;
        private bool _isMouseDown;
        private Point _dragStartMousePosition;  // 鼠标按下时的屏幕坐标（相对于 InfiniteCanvas）
        private Point _dragStartMousePositionRelative;  // 鼠标按下时相对于 NodeControl 的位置
        private Point2D _dragStartNodePosition;  // 拖拽开始时节点在画布坐标系中的位置
        private Dictionary<string, Point2D> _selectedNodesInitialPositions; // 选中节点的初始位置（用于多选拖动）
        private InfiniteCanvas _parentCanvas;
        private DateTime _mouseDownTime;  // 鼠标按下的时间（用于长按检测，无需定时器）
        private ContentPresenter _contentPresenter;  // 缓存的 ContentPresenter 引用
        private TranslateTransform _dragTransform;  // 拖拽时的临时变换（用于流畅拖拽）

        static NodeControl()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(NodeControl),
                new FrameworkPropertyMetadata(typeof(NodeControl)));
        }

        public NodeControl()
        {
            Loaded += NodeControl_Loaded;
            Unloaded += NodeControl_Unloaded;
            DataContextChanged += OnDataContextChanged;
            
            // 只使用 Preview 事件，避免重复处理
            PreviewMouseLeftButtonDown += OnPreviewMouseLeftButtonDown;
            PreviewMouseMove += OnPreviewMouseMove;
            PreviewMouseLeftButtonUp += OnPreviewMouseLeftButtonUp;
            PreviewMouseRightButtonDown += OnPreviewMouseRightButtonDown; // 拦截右键，避免冒泡到画布
            
            // 确保控件可以接收鼠标事件
            IsHitTestVisible = true;
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            // 获取模板中的控件
            _editTextBox = GetTemplateChild("PART_EditTextBox") as TextBox;
            _titleTextBlock = GetTemplateChild("PART_TitleTextBlock") as TextBlock;
            _portTop = GetTemplateChild("PortTop") as PortControl;
            _portBottom = GetTemplateChild("PortBottom") as PortControl;
            _portLeft = GetTemplateChild("PortLeft") as PortControl;
            _portRight = GetTemplateChild("PortRight") as PortControl;
            InitializeContextMenu();

            if (_editTextBox != null)
            {
                _editTextBox.LostFocus += EditTextBox_LostFocus;
                _editTextBox.KeyDown += EditTextBox_KeyDown;
            }

            if (_titleTextBlock != null)
            {
                _titleTextBlock.MouseLeftButtonDown += TitleTextBlock_MouseLeftButtonDown;
            }

            // 模板应用后，确保端口拥有稳定的 ID，便于连线复原
            EnsurePortIds();
        }

        private void NodeControl_Loaded(object sender, RoutedEventArgs e)
        {
            // 初始化时设置默认图标和颜色
            UpdateStatusColors();
            UpdateDefaultIcon();
        }
        
        private void NodeControl_Unloaded(object sender, RoutedEventArgs e)
        {
            // 目前不再使用应用程序级别的鼠标监听
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            // 当节点数据切换时重新分配端口 ID，保证 Edge 可通过端口ID定位到正确端口
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
                    IsEditing = true;
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

        private void TryAssignPortId(PortControl port, string id)
        {
            if (port == null || string.IsNullOrWhiteSpace(id))
                return;

            if (string.IsNullOrWhiteSpace(port.PortId))
            {
                port.PortId = id;
            }
        }
        
        /// <summary>
        /// 应用程序鼠标按下事件处理（用于检测点击节点外部）
        /// </summary>
        private void OnApplicationMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (!IsEditing)
                return;
            
            // 检查点击位置是否在当前节点外部
            var mousePosition = e.GetPosition(this);
            var hitResult = VisualTreeHelper.HitTest(this, mousePosition);
            
            // 如果点击在节点外部（hitResult 为 null），退出编辑模式
            if (hitResult == null)
            {
                ExitEditMode();
            }
        }

        #region 依赖属性

        /// <summary>
        /// 节点标题
        /// </summary>
        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register("Title", typeof(string), typeof(NodeControl),
                new PropertyMetadata("节点标题"));

        public string Title
        {
            get { return (string)GetValue(TitleProperty); }
            set { SetValue(TitleProperty, value); }
        }

        /// <summary>
        /// 执行时间
        /// </summary>
        public static readonly DependencyProperty ExecutionTimeProperty =
            DependencyProperty.Register("ExecutionTime", typeof(string), typeof(NodeControl),
                new PropertyMetadata("0 s"));

        public string ExecutionTime
        {
            get { return (string)GetValue(ExecutionTimeProperty); }
            set { SetValue(ExecutionTimeProperty, value); }
        }

        /// <summary>
        /// 节点状态 - 使用 NodeExecutionState 枚举
        /// </summary>
        public static readonly DependencyProperty StatusProperty =
            DependencyProperty.Register("Status", typeof(NodeExecutionState), typeof(NodeControl),
                new PropertyMetadata(NodeExecutionState.Idle, OnStatusChanged));

        public NodeExecutionState Status
        {
            get { return (NodeExecutionState)GetValue(StatusProperty); }
            set { SetValue(StatusProperty, value); }
        }

        private static void OnStatusChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = d as NodeControl;
            control?.UpdateStatusColors();
            control?.UpdateDefaultIcon();
        }

        /// <summary>
        /// 图标颜色
        /// </summary>
        public static readonly DependencyProperty IconColorProperty =
            DependencyProperty.Register("IconColor", typeof(Brush), typeof(NodeControl),
                new PropertyMetadata(null));

        public Brush IconColor
        {
            get { return (Brush)GetValue(IconColorProperty); }
            set { SetValue(IconColorProperty, value); }
        }

        /// <summary>
        /// 自定义图标路径数据
        /// </summary>
        public static readonly DependencyProperty IconDataProperty =
            DependencyProperty.Register("IconData", typeof(Geometry), typeof(NodeControl),
                new PropertyMetadata(null, OnIconDataChanged));

        public Geometry IconData
        {
            get { return (Geometry)GetValue(IconDataProperty); }
            set { SetValue(IconDataProperty, value); }
        }

        private static void OnIconDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = d as NodeControl;
            // 如果设置了自定义图标，标记为自定义
            if (e.NewValue != null)
            {
                control.HasCustomIcon = true;
            }
            else
            {
                // 清空图标时，恢复默认图标
                control?.UpdateDefaultIcon();
            }
        }

        /// <summary>
        /// 是否使用自定义图标
        /// </summary>
        public static readonly DependencyProperty HasCustomIconProperty =
            DependencyProperty.Register("HasCustomIcon", typeof(bool), typeof(NodeControl),
                new PropertyMetadata(false));

        public bool HasCustomIcon
        {
            get { return (bool)GetValue(HasCustomIconProperty); }
            private set { SetValue(HasCustomIconProperty, value); }
        }

        /// <summary>
        /// 是否显示端口
        /// </summary>
        public static readonly DependencyProperty ShowPortsProperty =
            DependencyProperty.Register("ShowPorts", typeof(bool), typeof(NodeControl),
                new PropertyMetadata(false));

        public bool ShowPorts
        {
            get { return (bool)GetValue(ShowPortsProperty); }
            set { SetValue(ShowPortsProperty, value); }
        }

        /// <summary>
        /// 是否处于编辑状态
        /// </summary>
        public static readonly DependencyProperty IsEditingProperty =
            DependencyProperty.Register("IsEditing", typeof(bool), typeof(NodeControl),
                new PropertyMetadata(false, OnIsEditingChanged));

        public bool IsEditing
        {
            get { return (bool)GetValue(IsEditingProperty); }
            set { SetValue(IsEditingProperty, value); }
        }

        private static void OnIsEditingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = d as NodeControl;
            if ((bool)e.NewValue)
            {
                control?.EnterEditMode();
            }
        }
        
        /// <summary>
        /// 是否被选中（用于框选等）
        /// </summary>
        public static readonly DependencyProperty IsSelectedProperty =
            DependencyProperty.Register("IsSelected", typeof(bool), typeof(NodeControl),
                new PropertyMetadata(false, OnIsSelectedChanged));

        public bool IsSelected
        {
            get { return (bool)GetValue(IsSelectedProperty); }
            set { SetValue(IsSelectedProperty, value); }
        }
        
        private static void OnIsSelectedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = d as NodeControl;
            if (control == null)
                return;
            
            var isSelected = (bool)e.NewValue;
            var node = control.DataContext as Astra.Core.Nodes.Models.Node;
            
            // 同步数据模型的选中状态
            if (node != null && node.IsSelected != isSelected)
            {
                node.IsSelected = isSelected;
            }
        }

        #endregion

        #region 事件处理

        protected override void OnMouseEnter(MouseEventArgs e)
        {
            base.OnMouseEnter(e);
            if (!IsEditing)
            {
                ShowPorts = true;
            }
        }

        protected override void OnMouseLeave(MouseEventArgs e)
        {
            base.OnMouseLeave(e);
            if (!IsEditing)
            {
                ShowPorts = false;
            }
        }

        private void OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _parentCanvas ??= FindParentCanvas(this);

            // 如果点击在端口上，优先进入“连线”模式而不是拖拽
            var hitPort = FindHitPort(e.OriginalSource as DependencyObject);
            if (hitPort != null)
            {
                var node = DataContext as Astra.Core.Nodes.Models.Node;
                _parentCanvas?.BeginConnection(node, hitPort);
                e.Handled = true;
                return;
            }

            // 检查是否点击在按钮上，如果是则不允许拖拽
            if (e.OriginalSource is System.Windows.Controls.Button ||
                e.OriginalSource is System.Windows.Controls.Primitives.ButtonBase)
            {
                return;
            }
            
            // 如果当前处于编辑状态，直接交给 TextBox 处理，不在此结束编辑
            // 结束编辑只依赖 LostFocus / Enter / Esc
            if (IsEditing)
            {
                return;
            }
            
            // 如果画布被锁定，仅允许选中，不允许拖拽
            if (_parentCanvas != null && _parentCanvas.IsLocked)
            {
                // 简单处理选中状态，其余交互（拖拽）由锁定禁止
                var node = DataContext as Astra.Core.Nodes.Models.Node;
                if (node != null)
                {
                    if (!node.IsSelected)
                    {
                        ClearOtherNodesSelection(node);
                        node.IsSelected = true;
                        IsSelected = true;

                        if (_parentCanvas.SelectedItems != null)
                        {
                            _parentCanvas.SelectedItems.Clear();
                            _parentCanvas.SelectedItems.Add(node);
                        }
                    }
                }
                e.Handled = true;
                return;
            }
            
            // 如果按下了 Ctrl 键，让 InfiniteCanvas 处理平移，不捕获鼠标
            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                return;  // 不处理，让事件继续传播到 InfiniteCanvas
            }
            
            // 准备拖拽节点
            if (!IsEditing && !_isDragging)
            {
                _parentCanvas = FindParentCanvas(this);
                _contentPresenter = FindParentContentPresenter(this);
                
                if (_parentCanvas == null || _contentPresenter == null)
                {
                    return;
                }
                
                // 处理节点选中逻辑
                var node = DataContext as Astra.Core.Nodes.Models.Node;
                if (node != null)
                {
                    // 如果当前节点未选中，则选中它
                    if (!node.IsSelected)
                    {
                        // 清除其他节点的选中状态（同时更新数据、视觉状态和 SelectedItems 集合）
                        ClearOtherNodesSelection(node);
                        
                        // 选中当前节点（同时更新数据和 SelectedItems 集合）
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
                        // （可能是通过框选或其他方式选中的，但还未添加到 SelectedItems）
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

                var currentNode = DataContext as Astra.Core.Nodes.Models.Node;
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
                            if (item is Astra.Core.Nodes.Models.Node node && node != currentNode)
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
                                if (item is Astra.Core.Nodes.Models.Node node)
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
                    _parentCanvas.RequestMinimapUpdate();
                }

                e.Handled = true;
            }
        }
        
        /// <summary>
        /// 更新其他选中节点的变换（用于拖动过程中的实时显示）
        /// </summary>
        private void UpdateOtherSelectedNodesTransform(double canvasDeltaX, double canvasDeltaY, Astra.Core.Nodes.Models.Node excludeNode)
        {
            if (_parentCanvas == null || _parentCanvas.SelectedItems == null || _parentCanvas.ItemsSource == null)
                return;
            
            var itemsControl = FindItemsControl(_parentCanvas);
            if (itemsControl == null)
                return;
            
            foreach (var item in _parentCanvas.SelectedItems)
            {
                if (item is Astra.Core.Nodes.Models.Node node && node != excludeNode)
                {
                    var container = itemsControl.ItemContainerGenerator.ContainerFromItem(node) as ContentPresenter;
                    if (container != null)
                    {
                        // 获取或创建变换组
                        var transformGroup = container.RenderTransform as TransformGroup;
                        if (transformGroup == null)
                        {
                            transformGroup = new TransformGroup();
                            container.RenderTransform = transformGroup;
                        }
                        
                        // 获取或创建 TranslateTransform
                        var translateTransform = transformGroup.Children.OfType<TranslateTransform>().FirstOrDefault();
                        if (translateTransform == null)
                        {
                            translateTransform = new TranslateTransform();
                            transformGroup.Children.Add(translateTransform);
                        }
                        
                        // 应用画布坐标系中的偏移量（与当前节点相同）
                        translateTransform.X = canvasDeltaX;
                        translateTransform.Y = canvasDeltaY;
                    }
                }
            }
        }
        
        private void OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
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
                e.Handled = true;

                // 结束拖拽后隐藏对齐线
                _parentCanvas?.HideAlignmentLines();
            }
        }
        
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
                    if (item is Astra.Core.Nodes.Models.Node node)
                    {
                        _selectedNodesInitialPositions[node.Id] = node.Position;
                    }
                }

                // 🔧 多节点拖拽时启用智能路径优化（避免重复计算A*）
                if (_selectedNodesInitialPositions.Count > 1)
                {
                    System.Diagnostics.Debug.WriteLine($"[批量拖拽] 开始拖拽 {_selectedNodesInitialPositions.Count} 个节点");
                    var movedNodeIds = new HashSet<string>(_selectedNodesInitialPositions.Keys);
                    _parentCanvas.EnableSmartEdgeUpdate(movedNodeIds);
                }
            }
        }
        
        private void EndDragging()
        {
            if (_parentCanvas == null || _contentPresenter == null)
                return;

            // 使用与 OnPreviewMouseMove 相同的逻辑计算最终位置
            // 这样可以避免计算方式不一致导致的跳变
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
            var currentNode = DataContext as Astra.Core.Nodes.Models.Node;

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
                    if (item is Astra.Core.Nodes.Models.Node selectedNode && selectedNode.Position != null)
                    {
                        if (selectedNode == currentNode)
                        {
                            // 🔧 当前节点：先更新视觉位置，再更新数据位置（避免端口位置计算时序错误）
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
                                        // 🔧 先更新视觉位置，再更新数据位置
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
                // 🔧 单个节点拖动：先更新视觉位置，再更新数据位置
                Canvas.SetLeft(_contentPresenter, finalCanvasPosition.X);
                Canvas.SetTop(_contentPresenter, finalCanvasPosition.Y);
                UpdateNodePosition(finalCanvasPosition);
            }

            // 🔧 结束批量拖拽（禁用智能优化）
            if (_selectedNodesInitialPositions != null && _selectedNodesInitialPositions.Count > 1)
            {
                _parentCanvas?.DisableSmartEdgeUpdate();
                // 🔧 对齐吸附后立即刷新连线，确保连线重新计算（避免对齐时连线保持折线形状）
                _parentCanvas?.RefreshEdgesImmediate();
                System.Diagnostics.Debug.WriteLine($"[批量拖拽] 完成，强制刷新连线");
            }
            else
            {
                // 单节点拖拽，立即刷新连线
            _parentCanvas?.RefreshEdgesImmediate();
            }

            // 拖拽结束后隐藏对齐辅助线
            _parentCanvas?.HideAlignmentLines();
        }
        
        /// <summary>
        /// 当画布缩放到最小值时，将节点限制在可见区域内
        /// </summary>
        private Point2D ClampToVisibleArea(Point2D desiredCanvasPosition)
        {
            if (_parentCanvas == null)
                return desiredCanvasPosition;

            // 获取节点的实际尺寸
            var nodeWidth = ActualWidth > 0 ? ActualWidth : 220;
            var nodeHeight = ActualHeight > 0 ? ActualHeight : 120;

            // 获取画布的实际可视区域大小
            var canvasWidth = _parentCanvas.ActualWidth;
            var canvasHeight = _parentCanvas.ActualHeight;

            // 如果画布尺寸无效，不做限制
            if (canvasWidth <= 0 || canvasHeight <= 0)
                return desiredCanvasPosition;

            // 考虑缩放：将画布屏幕尺寸转换为画布坐标系尺寸
            var canvasLogicalWidth = canvasWidth / _parentCanvas.Scale;
            var canvasLogicalHeight = canvasHeight / _parentCanvas.Scale;

            // 考虑平移：计算当前可见区域在画布坐标系中的范围
            var visibleLeft = -_parentCanvas.PanX / _parentCanvas.Scale;
            var visibleTop = -_parentCanvas.PanY / _parentCanvas.Scale;
            var visibleRight = visibleLeft + canvasLogicalWidth;
            var visibleBottom = visibleTop + canvasLogicalHeight;

            // 边界限制：节点必须完全在可见区域内
            // 左边界：节点左边不能超出可见区域左边
            var clampedX = Math.Max(visibleLeft, desiredCanvasPosition.X);
            // 右边界：节点右边不能超出可见区域右边
            clampedX = Math.Min(visibleRight - nodeWidth, clampedX);

            // 上边界：节点上边不能超出可见区域上边
            var clampedY = Math.Max(visibleTop, desiredCanvasPosition.Y);
            // 下边界：节点下边不能超出可见区域下边
            clampedY = Math.Min(visibleBottom - nodeHeight, clampedY);

            return new Point2D(clampedX, clampedY);
        }

        /// <summary>
        /// 查找最近的 ItemsControl（支持从当前元素向上、向下搜索）
        /// 说明：节点所在的 ItemsControl 通常是 InfiniteCanvas 的子元素而非父元素，
        /// 仅向上查找会导致获取失败，进而无法批量移动其他选中节点。
        /// </summary>
        private ItemsControl FindItemsControl(DependencyObject element)
        {
            if (element == null)
                return null;

            // 1) 如果自身就是 ItemsControl，直接返回
            if (element is ItemsControl selfItems)
                return selfItems;

            // 2) 先尝试向上查找（兼容原有逻辑）
            var parent = VisualTreeHelper.GetParent(element);
            while (parent != null)
            {
                if (parent is ItemsControl parentItems)
                {
                    return parentItems;
                }
                parent = VisualTreeHelper.GetParent(parent);
            }

            // 3) 向下广度优先查找子级 ItemsControl（适用于 InfiniteCanvas 这种子层级）
            var queue = new Queue<DependencyObject>();
            queue.Enqueue(element);
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                var childCount = VisualTreeHelper.GetChildrenCount(current);
                for (int i = 0; i < childCount; i++)
                {
                    var child = VisualTreeHelper.GetChild(current, i);
                    if (child is ItemsControl childItems)
                    {
                        return childItems;
                    }
                    queue.Enqueue(child);
                }
            }

            return null;
        }

        /// <summary>
        /// 获取节点当前渲染尺寸，用于计算对齐辅助线的矩形
        /// </summary>
        private Size GetNodeRenderSize(Astra.Core.Nodes.Models.Node node)
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

            // 兜底尺寸，避免零宽高导致的对齐计算异常
            if (width <= 0) width = 220;
            if (height <= 0) height = 40;

            return new Size(width, height);
        }

        private void TitleTextBlock_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // 保持空实现，禁用双击重命名
        }

    /// <summary>
    /// 拦截右键，统一只弹出节点菜单，避免冒泡到画布菜单
    /// </summary>
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

        private void EnterEditMode()
        {
            if (_editTextBox != null)
            {
                _editTextBox.Text = Title;
                _editTextBox.Visibility = Visibility.Visible;

                // 延迟设置焦点，确保文本框已经可见
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    _editTextBox.Focus();
                    _editTextBox.SelectAll();
                }), System.Windows.Threading.DispatcherPriority.Input);
            }

            if (_titleTextBlock != null)
            {
                _titleTextBlock.Visibility = Visibility.Collapsed;
            }

            // 编辑时隐藏端口
            ShowPorts = false;
        }

        private void ExitEditMode()
        {
            if (!IsEditing)
                return;

            if (_editTextBox != null && _titleTextBlock != null)
            {
                // 自动保存更改（只要文本不为空）
                ApplyRenameWithUndo(_editTextBox.Text);

                _editTextBox.Visibility = Visibility.Collapsed;
                _titleTextBlock.Visibility = Visibility.Visible;
            }

            IsEditing = false;
        }

        private void EditTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            // 失去焦点时自动完成重命名
            ExitEditMode();
        }

        private void EditTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                // Enter 键确认并移除焦点
                ExitEditMode();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                // Esc 键取消编辑，恢复原标题
                if (_editTextBox != null)
                {
                    _editTextBox.Text = Title; // 恢复原值
                }
                ExitEditMode();
                e.Handled = true;
            }
        }

        /// <summary>
        /// 通过可撤销命令应用重命名
        /// </summary>
        /// <param name="newTitleRaw">用户输入的新标题</param>
        private void ApplyRenameWithUndo(string newTitleRaw)
        {
            var newTitle = newTitleRaw?.Trim();
            if (string.IsNullOrWhiteSpace(newTitle))
                return;

            var oldTitle = Title;
            if (newTitle == oldTitle)
            {
                // 同步模型名称，防止两者不一致
                if (DataContext is Node nodeSync)
                {
                    nodeSync.Name = newTitle;
                }
                return;
            }

            if (DataContext is not Node node)
            {
                Title = newTitle;
                return;
            }

            _parentCanvas ??= FindParentCanvas(this);
            var undoManager = _parentCanvas?.UndoRedoManager;

            if (undoManager != null)
            {
                var command = new Commands.RenameNodeCommand(node, this, oldTitle, newTitle);
                // 设置命令的 WorkflowTab
                var workflowTab = FindWorkflowTab();
                if (workflowTab != null)
                {
                    command.WorkflowTab = workflowTab;
                }
                undoManager.Execute(command);
            }
            else
            {
                // 无撤销管理器，直接应用
                node.Name = newTitle;
                Title = newTitle;
            }
        }

        #endregion

        #region 颜色和图标管理

        /// <summary>
        /// 从资源字典获取画刷
        /// 所有颜色资源都应在 Colors.xaml 中定义
        /// </summary>
        private Brush GetBrushFromResource(string resourceKey)
        {
            try
            {
                var resource = Application.Current.TryFindResource(resourceKey);
                if (resource is Brush brush)
                {
                    return brush;
                }
                if (resource is Color color)
                {
                    return new SolidColorBrush(color);
                }
            }
            catch { }

            // 如果找不到资源，尝试使用 PrimaryBrush 作为 fallback
            // 这应该不会发生，因为所有颜色都在 Colors.xaml 中定义
            try
            {
                var fallbackResource = Application.Current.TryFindResource("PrimaryBrush");
                if (fallbackResource is Brush fallbackBrush)
                {
                    return fallbackBrush;
                }
                if (fallbackResource is Color fallbackColor)
                {
                    return new SolidColorBrush(fallbackColor);
                }
            }
            catch { }

            // 最后的 fallback：返回透明画刷（不应该到达这里）
            return new SolidColorBrush(Colors.Transparent);
        }

        private void UpdateStatusColors()
        {
            // 如果已经手动设置了IconColor，不自动更新
            if (IconColor != null && ReadLocalValue(IconColorProperty) != DependencyProperty.UnsetValue)
            {
                return;
            }

            // 所有颜色都从 Colors.xaml 资源字典中获取，使用现有颜色资源
            switch (Status)
            {
                case NodeExecutionState.Success:
                    IconColor = GetBrushFromResource("SuccessBrush");
                    break;
                case NodeExecutionState.Running:
                    IconColor = GetBrushFromResource("WarningBrush");
                    break;
                case NodeExecutionState.Skipped:
                    IconColor = GetBrushFromResource("InfoBrush");
                    break;
                case NodeExecutionState.Failed:
                case NodeExecutionState.Cancelled:
                    IconColor = GetBrushFromResource("DangerBrush");
                    break;
                case NodeExecutionState.Idle:
                    IconColor = GetBrushFromResource("PrimaryBrush");
                    break;
                default:
                    IconColor = GetBrushFromResource("PrimaryBrush");
                    break;
            }
        }

        private void UpdateDefaultIcon()
        {
            // 如果已经设置了自定义图标，不覆盖
            if (HasCustomIcon)
            {
                return;
            }

            string pathData = "";
            switch (Status)
            {
                case NodeExecutionState.Success:
                    // 勾选图标
                    pathData = "M9 12L11 14L15 10M21 12C21 16.9706 16.9706 21 12 21C7.02944 21 3 16.9706 3 12C3 7.02944 7.02944 3 12 3C16.9706 3 21 7.02944 21 12Z";
                    break;
                case NodeExecutionState.Running:
                    // 加载/旋转图标
                    pathData = "M12 2V6M12 18V22M4.93 4.93L7.76 7.76M16.24 16.24L19.07 19.07M2 12H6M18 12H22M4.93 19.07L7.76 16.24M16.24 7.76L19.07 4.93";
                    break;
                case NodeExecutionState.Skipped:
                    // 时钟图标
                    pathData = "M21 12C21 16.9706 16.9706 21 12 21C7.02944 21 3 16.9706 3 12C3 7.02944 7.02944 3 12 3C16.9706 3 21 7.02944 21 12Z M12 7V12L15 15";
                    break;
                case NodeExecutionState.Failed:
                case NodeExecutionState.Cancelled:
                    // 警告图标
                    pathData = "M12 8V12M12 16H12.01M21 12C21 16.9706 16.9706 21 12 21C7.02944 21 3 16.9706 3 12C3 7.02944 7.02944 3 12 3C16.9706 3 21 7.02944 21 12Z";
                    break;
                case NodeExecutionState.Idle:
                    // 通知图标
                    pathData = "M15 17H20L18.5951 15.5951C18.2141 15.2141 18 14.6973 18 14.1585V11C18 8.38757 16.3304 6.16509 14 5.34142V5C14 3.89543 13.1046 3 12 3C10.8954 3 10 3.89543 10 5V5.34142C7.66962 6.16509 6 8.38757 6 11V14.1585C6 14.6973 5.78595 15.2141 5.40493 15.5951L4 17H9M15 17V18C15 19.6569 13.6569 21 12 21C10.3431 21 9 19.6569 9 18V17M15 17H9";
                    break;
            }

            // 设置默认图标时，不标记为自定义图标
            var oldValue = HasCustomIcon;
            HasCustomIcon = false;
            IconData = Geometry.Parse(pathData);
            HasCustomIcon = oldValue;
        }

        #endregion
        
        #region 拖拽移动辅助方法
        
        /// <summary>
        /// 清除其他节点的选中状态（同时更新数据模型和视觉状态）
        /// </summary>
        private void ClearOtherNodesSelection(Astra.Core.Nodes.Models.Node currentNode)
        {
            if (_parentCanvas == null || _parentCanvas.ItemsSource == null)
                return;
            
            var itemsControl = FindItemsControl(_parentCanvas);
            
            foreach (var item in _parentCanvas.ItemsSource)
            {
                if (item is Astra.Core.Nodes.Models.Node otherNode && otherNode != currentNode)
                {
                    // 清除数据模型的选中状态
                    otherNode.IsSelected = false;
                    
                    // 清除视觉状态（NodeControl.IsSelected）
                    if (itemsControl != null)
                    {
                        var container = itemsControl.ItemContainerGenerator.ContainerFromItem(otherNode) as ContentPresenter;
                        if (container != null && VisualTreeHelper.GetChildrenCount(container) > 0)
                        {
                            if (VisualTreeHelper.GetChild(container, 0) is NodeControl nodeControl)
                            {
                                nodeControl.IsSelected = false;
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 确保当前节点已选中；未选中则选中并直接返回 true
        /// </summary>
        private bool EnsureSelectedForAction()
        {
            _parentCanvas ??= FindParentCanvas(this);
            if (_parentCanvas == null || DataContext is not Astra.Core.Nodes.Models.Node node)
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
        /// 查找父 InfiniteCanvas
        /// </summary>
        private InfiniteCanvas FindParentCanvas(DependencyObject element)
        {
            var parent = VisualTreeHelper.GetParent(element);
            while (parent != null)
            {
                if (parent is InfiniteCanvas canvas)
                {
                    return canvas;
                }
                parent = VisualTreeHelper.GetParent(parent);
            }
            return null;
        }

        /// <summary>
        /// 查找父级的 FlowEditor 并获取其 WorkflowTab
        /// </summary>
        private UI.Models.WorkflowTab FindWorkflowTab()
        {
            var parent = VisualTreeHelper.GetParent(this);
            while (parent != null)
            {
                if (parent is InfiniteCanvas canvas)
                {
                    // 继续向上查找 FlowEditor
                    var flowEditorParent = VisualTreeHelper.GetParent(canvas);
                    while (flowEditorParent != null)
                    {
                        if (flowEditorParent is FlowEditor flowEditor)
                        {
                            return flowEditor.DataContext as UI.Models.WorkflowTab;
                        }
                        flowEditorParent = VisualTreeHelper.GetParent(flowEditorParent);
                    }
                }
                parent = VisualTreeHelper.GetParent(parent);
            }
            return null;
        }
        
        /// <summary>
        /// 获取当前节点在画布坐标系中的位置
        /// </summary>
        private Point2D GetCurrentCanvasPosition()
        {
            var dataContext = DataContext;
            if (dataContext == null) 
                return Point2D.Zero;
            
            // 如果 DataContext 是 Node 类型，直接获取 Position 属性
            if (dataContext is Node node)
            {
                return node.Position;
            }
            
            // 尝试通过反射获取 Position 属性
            var positionProp = dataContext.GetType().GetProperty("Position");
            if (positionProp != null)
            {
                var position = positionProp.GetValue(dataContext);
                if (position != null)
                {
                    var positionType = position.GetType();
                    
                    // 如果是 Point2D 类型
                    if (positionType.Name == "Point2D")
                    {
                        var xProp = positionType.GetProperty("X");
                        var yProp = positionType.GetProperty("Y");
                        if (xProp != null && yProp != null)
                        {
                            var x = Convert.ToDouble(xProp.GetValue(position) ?? 0);
                            var y = Convert.ToDouble(yProp.GetValue(position) ?? 0);
                            return new Point2D(x, y);
                        }
                    }
                }
            }
            
            // 如果都没有，从 Canvas 附加属性获取
            if (_parentCanvas != null)
            {
                var left = Canvas.GetLeft(this);
                var top = Canvas.GetTop(this);
                if (!double.IsNaN(left) && !double.IsNaN(top))
                {
                    // 转换为画布坐标系
                    var screenPoint = new Point(left, top);
                    var canvasPoint = _parentCanvas.ScreenToCanvas(screenPoint);
                    return new Point2D(canvasPoint.X, canvasPoint.Y);
                }
            }
            
            return Point2D.Zero;
        }
        
        /// <summary>
        /// 更新节点位置（只在拖拽结束时调用一次）
        /// </summary>
        private void UpdateNodePosition(Point2D canvasPosition)
        {
            var dataContext = DataContext;
            if (dataContext == null) return;
            
            try
            {
                // 更新数据模型，让绑定系统自动更新 UI 位置
                if (dataContext is Node node)
                {
                    node.Position = canvasPosition;
                }
                else
                {
                    // 尝试通过反射设置 Position 属性
                    var positionProp = dataContext.GetType().GetProperty("Position");
                    if (positionProp != null && positionProp.CanWrite)
                    {
                        var positionType = positionProp.PropertyType;
                        
                        if (positionType.Name == "Point2D")
                        {
                            var constructor = positionType.GetConstructor(new[] { typeof(double), typeof(double) });
                            if (constructor != null)
                            {
                                var newPoint2D = constructor.Invoke(new object[] { canvasPosition.X, canvasPosition.Y });
                                positionProp.SetValue(dataContext, newPoint2D);
                            }
                        }
                        else if (positionType == typeof(Point))
                        {
                            positionProp.SetValue(dataContext, new Point(canvasPosition.X, canvasPosition.Y));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[NodeControl] 更新节点位置失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 判断是否点在端口上（PortControl），返回命中的端口
        /// </summary>
        private FrameworkElement FindHitPort(DependencyObject source)
        {
            var current = source;
            while (current != null)
            {
                if (current is PortControl)
                {
                    return current as FrameworkElement;
                }
                current = VisualTreeHelper.GetParent(current);
            }
            return null;
        }

        /// <summary>
        /// 查找父 ContentPresenter（节点被包裹在其中）
        /// </summary>
        private ContentPresenter FindParentContentPresenter(DependencyObject element)
        {
            var parent = VisualTreeHelper.GetParent(element);
            while (parent != null)
            {
                if (parent is ContentPresenter cp)
                {
                    return cp;
                }
                parent = VisualTreeHelper.GetParent(parent);
            }
            return null;
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
        /// 右键菜单复制节点到剪贴板（不立即粘贴）
        /// </summary>
        private void OnCopyMenuItemClick(object sender, RoutedEventArgs e)
        {
            if (!EnsureSelectedForAction())
                return;

            _parentCanvas ??= FindParentCanvas(this);
            if (_parentCanvas == null)
                return;

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
                    var nodeWidth = node.Size.IsEmpty ? 220 : node.Size.Width;
                    var nodeHeight = node.Size.IsEmpty ? 40 : node.Size.Height;
                    
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


        
        #endregion
    }

    /// <summary>
    /// 端口控件
    /// </summary>
    public class PortControl : Control
    {
        static PortControl()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(PortControl),
                new FrameworkPropertyMetadata(typeof(PortControl)));
        }

        /// <summary>
        /// 端口唯一标识符
        /// </summary>
        public static readonly DependencyProperty PortIdProperty =
            DependencyProperty.Register(
                nameof(PortId),
                typeof(string),
                typeof(PortControl),
                new PropertyMetadata(null));

        public string PortId
        {
            get => (string)GetValue(PortIdProperty);
            set => SetValue(PortIdProperty, value);
        }

        /// <summary>
        /// 端口类型（Input/Output）
        /// </summary>
        public static readonly DependencyProperty PortTypeProperty =
            DependencyProperty.Register(
                nameof(PortType),
                typeof(string),
                typeof(PortControl),
                new PropertyMetadata("Output"));

        public string PortType
        {
            get => (string)GetValue(PortTypeProperty);
            set => SetValue(PortTypeProperty, value);
        }
    }
}
