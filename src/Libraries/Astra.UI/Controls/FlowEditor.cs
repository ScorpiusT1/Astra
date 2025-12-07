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
        /// 工具箱宽度
        /// </summary>
        public double ToolBoxWidth
        {
            get => (double)GetValue(ToolBoxWidthProperty);
            set => SetValue(ToolBoxWidthProperty, value);
        }

        #endregion

        #region 私有字段

        private NodeToolBox _nodeToolBox;
        private InfiniteCanvas _infiniteCanvas;

        #endregion

        #region 构造函数

        public FlowEditor()
        {
            Loaded += OnLoaded;
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
                
                // 添加更多调试信息
                System.Diagnostics.Debug.WriteLine($"[FlowEditor] SubscribeToEvents: InfiniteCanvas AllowDrop={_infiniteCanvas.AllowDrop}, IsLoaded={_infiniteCanvas.IsLoaded}, IsVisible={_infiniteCanvas.IsVisible}, IsEnabled={_infiniteCanvas.IsEnabled}");
                System.Diagnostics.Debug.WriteLine($"[FlowEditor] SubscribeToEvents: InfiniteCanvas ActualWidth={_infiniteCanvas.ActualWidth}, ActualHeight={_infiniteCanvas.ActualHeight}");
                
                // 在 Loaded 事件中再次确认
                _infiniteCanvas.Loaded += (s, e) =>
                {
                    System.Diagnostics.Debug.WriteLine($"[FlowEditor] InfiniteCanvas Loaded: AllowDrop={_infiniteCanvas.AllowDrop}, IsHitTestVisible={_infiniteCanvas.IsHitTestVisible}");
                };
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[FlowEditor] SubscribeToEvents: _infiniteCanvas 为 null！");
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
            }
        }

        #endregion

        #region 拖放事件处理

        private void OnCanvasDragEnter(object sender, DragEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"[FlowEditor] DragEnter 事件触发，sender={sender?.GetType().Name}, AllowDrop: {_infiniteCanvas?.AllowDrop}, Source={e.Source?.GetType().Name}, OriginalSource={e.OriginalSource?.GetType().Name}");
            if (IsValidDragData(e.Data))
            {
                e.Effects = DragDropEffects.Copy;
                e.Handled = true;
                System.Diagnostics.Debug.WriteLine($"[FlowEditor] DragEnter: 拖拽数据有效，设置为 Copy");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[FlowEditor] DragEnter: 拖拽数据无效");
            }
        }

        private void OnCanvasDragOver(object sender, DragEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"[FlowEditor] DragOver 事件触发，sender={sender?.GetType().Name}, Source={e.Source?.GetType().Name}");
            
            // 检查拖拽数据是否有效
            bool isValid = IsValidDragData(e.Data);
            
            if (isValid)
            {
                e.Effects = DragDropEffects.Copy;
                e.Handled = true;
                System.Diagnostics.Debug.WriteLine($"[FlowEditor] DragOver: 拖拽数据有效，设置为 Copy");
            }
            else
            {
                // 只有在数据确实无效时才设置 None
                // 如果数据格式不匹配，记录调试信息
                if (e.Data != null)
                {
                    var formats = e.Data.GetFormats();
                    System.Diagnostics.Debug.WriteLine($"[FlowEditor] DragOver: 数据格式不匹配。可用格式: {string.Join(", ", formats)}");
                }
                e.Effects = DragDropEffects.None;
                e.Handled = true;
            }
        }

        private void OnCanvasDragLeave(object sender, DragEventArgs e)
        {
            // 可以在这里添加视觉反馈清除逻辑
        }

        private void OnCanvasDrop(object sender, DragEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"[FlowEditor] Drop 事件触发");
            if (!IsValidDragData(e.Data))
            {
                System.Diagnostics.Debug.WriteLine($"[FlowEditor] Drop: 拖拽数据无效，返回");
                return;
            }

            try
            {
                // 获取工具项
                var toolItem = e.Data.GetData(DragDropDataFormats.ToolItem) as IToolItem;
                if (toolItem == null)
                {
                    System.Diagnostics.Debug.WriteLine($"[FlowEditor] Drop: 无法从拖拽数据中获取 IToolItem");
                    return;
                }

                System.Diagnostics.Debug.WriteLine($"[FlowEditor] Drop: 成功获取工具项: {toolItem.Name}");

                // 获取鼠标在画布上的位置（画布坐标系）
                var dropPosition = e.GetPosition(_infiniteCanvas);
                var canvasPosition = _infiniteCanvas.ScreenToCanvas(dropPosition);

                // 创建节点（根据 IToolItem.NodeType 自动创建）
                Node node = CreateDefaultNode(toolItem, canvasPosition);

                // 验证节点必须不为空
                if (node == null)
                {
                    System.Diagnostics.Debug.WriteLine($"创建的节点为 null，无法添加到画布");
                    return;
                }

                // 添加到画布数据源
                AddNodeToCanvas(node, canvasPosition);

                e.Effects = DragDropEffects.Copy;
                e.Handled = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"拖放节点时发生错误: {ex.Message}");
                e.Effects = DragDropEffects.None;
            }
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 检查拖拽数据是否有效
        /// </summary>
        private bool IsValidDragData(IDataObject data)
        {
            if (data == null)
            {
                System.Diagnostics.Debug.WriteLine($"[FlowEditor] IsValidDragData: data 为 null");
                return false;
            }

            bool hasFormat = data.GetDataPresent(DragDropDataFormats.ToolItem);
            if (!hasFormat)
            {
                // 列出所有可用的数据格式，便于调试
                var formats = data.GetFormats();
                System.Diagnostics.Debug.WriteLine($"[FlowEditor] IsValidDragData: 未找到格式 '{DragDropDataFormats.ToolItem}'。可用格式: {string.Join(", ", formats)}");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[FlowEditor] IsValidDragData: 找到有效格式 '{DragDropDataFormats.ToolItem}'");
            }

            return hasFormat;
        }

        /// <summary>
        /// 根据工具项创建节点
        /// 要求：
        /// 1. IToolItem.NodeType 必须指定为 Node 的子类类型（Type 对象或类型名称字符串）
        /// 2. 节点类型必须不是抽象类
        /// 3. 节点类型必须有公共无参构造函数
        /// 如果创建失败，返回 null
        /// </summary>
        private Node CreateDefaultNode(IToolItem toolItem, Point position)
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
        private void SetNodePosition(object node, Point position)
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
                else if (positionType == typeof(Point))
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
        private void AddNodeToCanvas(Node node, Point position)
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
                // 确保数据源设置后，NodeToolBox 能正确初始化
                System.Diagnostics.Debug.WriteLine($"[FlowEditor] 设置 NodeToolBox.ItemsSource: {ToolBoxItemsSource != null}, Count: {(ToolBoxItemsSource as System.Collections.ICollection)?.Count ?? 0}");
            }

            if (_infiniteCanvas != null)
            {
                _infiniteCanvas.ItemsSource = CanvasItemsSource;
                System.Diagnostics.Debug.WriteLine($"[FlowEditor] 设置 InfiniteCanvas.ItemsSource: {CanvasItemsSource != null}, Count: {(CanvasItemsSource as System.Collections.ICollection)?.Count ?? 0}");
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
                System.Diagnostics.Debug.WriteLine($"[FlowEditor] ToolBoxItemsSource 变更: {e.NewValue != null}, Count: {(e.NewValue as System.Collections.ICollection)?.Count ?? 0}");
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

        #endregion

        #region 生命周期

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            ApplyDataSources();
            
            // 确保在加载后 InfiniteCanvas 的 AllowDrop 已设置
            if (_infiniteCanvas != null)
            {
                _infiniteCanvas.AllowDrop = true;
                System.Diagnostics.Debug.WriteLine($"[FlowEditor] OnLoaded: InfiniteCanvas AllowDrop={_infiniteCanvas.AllowDrop}, IsLoaded={_infiniteCanvas.IsLoaded}");
                
                // 确保内容画布也启用拖放
                if (_infiniteCanvas is InfiniteCanvas canvas)
                {
                    // 通过反射或直接访问内容画布（如果可能）
                    // 注意：内容画布是私有字段，我们已经在 InfiniteCanvas 的 OnApplyTemplate 中设置了
                }
            }
        }

        #endregion
    }
}
