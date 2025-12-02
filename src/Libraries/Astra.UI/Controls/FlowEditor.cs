using Astra.UI.Serivces;
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
        /// 节点创建委托 - 用于从工具项创建节点对象
        /// </summary>
        public static readonly DependencyProperty NodeFactoryProperty =
            DependencyProperty.Register(
                nameof(NodeFactory),
                typeof(Func<IToolItem, Point, object>),
                typeof(FlowEditor),
                new PropertyMetadata(null));

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
        /// 节点创建委托 - 参数: (工具项, 画布坐标位置) => 节点对象
        /// </summary>
        public Func<IToolItem, Point, object> NodeFactory
        {
            get => (Func<IToolItem, Point, object>)GetValue(NodeFactoryProperty);
            set => SetValue(NodeFactoryProperty, value);
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
                _infiniteCanvas.DragOver += OnCanvasDragOver;
                _infiniteCanvas.Drop += OnCanvasDrop;
                _infiniteCanvas.DragEnter += OnCanvasDragEnter;
                _infiniteCanvas.DragLeave += OnCanvasDragLeave;
            }
        }

        private void UnsubscribeFromEvents()
        {
            if (_infiniteCanvas != null)
            {
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
            if (IsValidDragData(e.Data))
            {
                e.Effects = DragDropEffects.Copy;
                e.Handled = true;
            }
        }

        private void OnCanvasDragOver(object sender, DragEventArgs e)
        {
            if (IsValidDragData(e.Data))
            {
                e.Effects = DragDropEffects.Copy;
                e.Handled = true;
            }
            else
            {
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
            if (!IsValidDragData(e.Data))
            {
                return;
            }

            try
            {
                // 获取工具项
                var toolItem = e.Data.GetData(DragDropDataFormats.ToolItem) as IToolItem;
                if (toolItem == null)
                {
                    return;
                }

                // 获取鼠标在画布上的位置（画布坐标系）
                var dropPosition = e.GetPosition(_infiniteCanvas);
                var canvasPosition = _infiniteCanvas.ScreenToCanvas(dropPosition);

                // 创建节点
                object node = null;
                if (NodeFactory != null)
                {
                    node = NodeFactory(toolItem, canvasPosition);
                }
                else
                {
                    // 默认实现：创建一个简单的节点对象
                    node = CreateDefaultNode(toolItem, canvasPosition);
                }

                if (node == null)
                {
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
            return data != null && data.GetDataPresent(DragDropDataFormats.ToolItem);
        }

        /// <summary>
        /// 创建默认节点（当未提供 NodeFactory 时使用）
        /// </summary>
        private object CreateDefaultNode(IToolItem toolItem, Point position)
        {
            // 创建一个简单的节点对象，包含位置信息
            return new
            {
                Name = toolItem.Name,
                X = position.X,
                Y = position.Y,
                Width = 100.0,
                Height = 50.0
            };
        }

        /// <summary>
        /// 添加节点到画布数据源
        /// </summary>
        private void AddNodeToCanvas(object node, Point position)
        {
            if (CanvasItemsSource == null)
            {
                return;
            }

            // 如果节点是 FrameworkElement，设置 Canvas 附加属性
            if (node is FrameworkElement element)
            {
                Canvas.SetLeft(element, position.X);
                Canvas.SetTop(element, position.Y);
            }
            else
            {
                // 尝试通过反射设置 X 和 Y 属性
                var xProp = node.GetType().GetProperty("X");
                var yProp = node.GetType().GetProperty("Position");

                if (xProp != null && xProp.CanWrite)
                {
                    xProp.SetValue(node, position.X);
                }

                if (yProp != null && yProp.CanWrite)
                {
                    // 如果 Position 是 Point 类型
                    var posType = yProp.PropertyType;
                    if (posType == typeof(Point))
                    {
                        yProp.SetValue(node, position);
                    }
                    else
                    {
                        // 尝试设置 Y 属性
                        var yProp2 = node.GetType().GetProperty("Y");
                        if (yProp2 != null && yProp2.CanWrite)
                        {
                            yProp2.SetValue(node, position.Y);
                        }
                    }
                }
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
                    System.Diagnostics.Debug.WriteLine($"CanvasItemsSource 不支持添加操作，请使用 IList 或 ObservableCollection<T>");
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
        }

        #endregion
    }
}
