using Astra.UI.Serivces;
using Astra.Core.Nodes.Models;
using Astra.Core.Nodes.Geometry;
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
        /// 是否可撤销/重做（连线相关）
        /// </summary>
        public bool CanUndo => _undoRedoManager.CanUndo;
        public bool CanRedo => _undoRedoManager.CanRedo;

        public void Undo() => _undoRedoManager.Undo();
        public void Redo() => _undoRedoManager.Redo();

        #endregion

        #region 私有字段

        private NodeToolBox _nodeToolBox;
        private InfiniteCanvas _infiniteCanvas;
        private ContextMenu _canvasContextMenu;  // 画布右键菜单
        private Window _hostWindow;
        private bool _windowEventsAttached;
        private readonly UndoRedoManager _undoRedoManager = new UndoRedoManager();

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

            // 订阅事件
            SubscribeToEvents();

            // 应用数据源
            ApplyDataSources();
        }

        #endregion

        #region 事件订阅

        private void SubscribeToEvents()
        {
            if (_infiniteCanvas != null)
            {
                // 确保 InfiniteCanvas 启用拖放功能
                _infiniteCanvas.AllowDrop = true;
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
            if (_infiniteCanvas == null)
                return;

            var dropPosition = e.GetPosition(_infiniteCanvas);
            TryHandleNodeDrop(e, dropPosition);
        }

        #endregion

        #region 键盘事件

        /// <summary>
        /// FlowEditor 键盘事件（支持 Delete 键删除）
        /// </summary>
        private void OnFlowEditorKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete)
            {
                DeleteSelectedNodes();
                e.Handled = true;
            }
        }

        #endregion

        #region 控件级拖放兼容处理

        /// <summary>
        /// 预览拖放（作用于整个 FlowEditor），确保靠近工具箱边缘时也能正确显示拷贝光标
        /// </summary>
        private void OnFlowEditorPreviewDragOver(object sender, DragEventArgs e)
        {
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
            if (_infiniteCanvas == null)
                return;

            var point = e.GetPosition(_infiniteCanvas);
            TryHandleNodeDrop(e, point);
        }

        #endregion

        #region 右键菜单

        /// <summary>
        /// 初始化画布右键菜单
        /// </summary>
        private void InitializeContextMenu()
        {
            _canvasContextMenu = new ContextMenu();
            
            // 应用主题样式
            var contextMenuStyle = TryFindResource("ThemedContextMenu") as Style;
            if (contextMenuStyle != null)
            {
                _canvasContextMenu.Style = contextMenuStyle;
            }

            // 删除菜单项
            var deleteMenuItem = new MenuItem()
            {
                Header = "删除选中项",
                InputGestureText = "Delete",  // 快捷键提示
                Tag = "Danger"  // 标记为危险操作，用于应用危险色样式
            };
            
            // 应用主题样式
            var menuItemStyle = TryFindResource("ThemedMenuItem") as Style;
            if (menuItemStyle != null)
            {
                deleteMenuItem.Style = menuItemStyle;
            }
            
            // 创建删除图标（使用 Viewbox 包裹 Canvas）
            var iconViewbox = new Viewbox
            {
                Width = 16,
                Height = 16,
                Stretch = Stretch.Uniform
            };
            
            var iconCanvas = new Canvas
            {
                Width = 24,
                Height = 24
            };
            
            var iconPath = new System.Windows.Shapes.Path
            {
                Data = Geometry.Parse("M19 7L18.1327 19.1425C18.0579 20.1891 17.187 21 16.1378 21H7.86224C6.81296 21 5.94208 20.1891 5.86732 19.1425L5 7M10 11V17M14 11V17M15 7V4C15 3.44772 14.5523 3 14 3H10C9.44772 3 9 3.44772 9 4V7M4 7H20"),
                Stroke = (System.Windows.Media.Brush)TryFindResource("DangerBrush") ?? System.Windows.Media.Brushes.Red,
                StrokeThickness = 2,
                StrokeLineJoin = PenLineJoin.Round,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round
            };
            
            iconCanvas.Children.Add(iconPath);
            iconViewbox.Child = iconCanvas;
            deleteMenuItem.Icon = iconViewbox;
            
            deleteMenuItem.Click += OnDeleteMenuItemClick;

            _canvasContextMenu.Items.Add(deleteMenuItem);
        }

        /// <summary>
        /// 画布右键按下事件
        /// </summary>
        private void OnCanvasRightMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (_infiniteCanvas == null || _canvasContextMenu == null)
                return;

            // 检查是否有选中项
            var selectedCount = _infiniteCanvas.SelectedItems?.Count ?? 0;
            if (selectedCount == 0)
            {
                // 没有选中项，不显示菜单
                return;
            }

            // 更新菜单项文本
            if (_canvasContextMenu.Items[0] is MenuItem deleteItem)
            {
                deleteItem.Header = selectedCount > 1 ? $"删除 {selectedCount} 个选中项" : "删除选中项";
            }

            // 显示菜单
            _canvasContextMenu.PlacementTarget = _infiniteCanvas;
            _canvasContextMenu.IsOpen = true;

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
        /// 删除选中的节点
        /// </summary>
        private void DeleteSelectedNodes()
        {
            if (_infiniteCanvas?.SelectedItems == null || _infiniteCanvas.SelectedItems.Count == 0)
                return;

            if (CanvasItemsSource == null)
                return;

            // 复制选中项列表（避免在迭代时修改集合）
            var itemsToDelete = new List<object>(_infiniteCanvas.SelectedItems.Cast<object>());

            // 从数据源中删除
            if (CanvasItemsSource is IList list)
            {
                foreach (var item in itemsToDelete)
                {
                    list.Remove(item);
                }
            }
            else
            {
                // 尝试通过反射调用 Remove 方法
                var removeMethod = CanvasItemsSource.GetType().GetMethod("Remove");
                if (removeMethod != null)
                {
                    foreach (var item in itemsToDelete)
                    {
                        try
                        {
                            removeMethod.Invoke(CanvasItemsSource, new[] { item });
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"删除节点时发生错误: {ex.Message}");
                        }
                    }
                }
            }

            // 同步删除关联的连线
            if (EdgeItemsSource is IList edgeList)
            {
                var removeIds = new HashSet<string>(
                    itemsToDelete.OfType<Node>().Select(n => n.Id));

                if (removeIds.Count > 0)
                {
                    var edgesToDelete = edgeList
                        .Cast<object>()
                        .OfType<Edge>()
                        .Where(e => removeIds.Contains(e.SourceNodeId) || removeIds.Contains(e.TargetNodeId))
                        .Cast<object>()
                        .ToList();

                    if (edgesToDelete.Count > 0)
                    {
                        if (_undoRedoManager != null)
                        {
                            _undoRedoManager.Do(new DeleteEdgeCommand(edgeList, edgesToDelete));
                        }
                        else
                        {
                            foreach (var edge in edgesToDelete)
                            {
                                edgeList.Remove(edge);
                            }
                        }
                    }
                }
            }

            // 清除选中状态
            _infiniteCanvas.ClearSelection();
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
        /// 添加节点到画布数据源
        /// 要求：节点必须是 Node 的子类
        /// </summary>
        private void AddNodeToCanvas(Node node, System.Windows.Point position)
        {
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

            // 添加到集合
            if (CanvasItemsSource is IList list)
            {
                list.Add(node);
            }
            else
            {
                // 尝试通过反射调用 Add 方法（适用于 ObservableCollection<T> 等泛型集合）
                var addMethod = CanvasItemsSource.GetType().GetMethod("Add");
                if (addMethod != null)
                {
                    try
                    {
                        addMethod.Invoke(CanvasItemsSource, new[] { node });
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
            }
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

        #endregion

        #region 生命周期

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            ApplyDataSources();
            
            // 确保在加载后 InfiniteCanvas 的 AllowDrop 已设置
            if (_infiniteCanvas != null)
            {
                _infiniteCanvas.AllowDrop = true;
                _infiniteCanvas.IsHitTestVisible = true;
            }

            AttachWindowDragHandlers();
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
            if (_infiniteCanvas == null)
                return;

            var screenPoint = GetCursorScreenPoint();
            if (!IsScreenPointInsideFlowEditor(screenPoint))
                return;

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
