﻿﻿﻿using Astra.Core.Nodes.Geometry;
using Astra.Core.Nodes.Models;
using System;
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
        
        // 拖拽移动相关字段
        private bool _isDragging;
        private bool _isMouseDown;
        private Point _dragStartMousePosition;  // 鼠标按下时的屏幕坐标（相对于 InfiniteCanvas）
        private Point _dragStartMousePositionRelative;  // 鼠标按下时相对于 NodeControl 的位置
        private Point2D _dragStartNodePosition;  // 拖拽开始时节点在画布坐标系中的位置
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
            
            // 只使用 Preview 事件，避免重复处理
            PreviewMouseLeftButtonDown += OnPreviewMouseLeftButtonDown;
            PreviewMouseMove += OnPreviewMouseMove;
            PreviewMouseLeftButtonUp += OnPreviewMouseLeftButtonUp;
            
            // 确保控件可以接收鼠标事件
            IsHitTestVisible = true;
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            // 获取模板中的控件
            _editTextBox = GetTemplateChild("PART_EditTextBox") as TextBox;
            _titleTextBlock = GetTemplateChild("PART_TitleTextBlock") as TextBlock;

            if (_editTextBox != null)
            {
                _editTextBox.LostFocus += EditTextBox_LostFocus;
                _editTextBox.KeyDown += EditTextBox_KeyDown;
            }

            if (_titleTextBlock != null)
            {
                _titleTextBlock.MouseLeftButtonDown += TitleTextBlock_MouseLeftButtonDown;
            }
        }

        private void NodeControl_Loaded(object sender, RoutedEventArgs e)
        {
            // 初始化时设置默认图标和颜色
            UpdateStatusColors();
            UpdateDefaultIcon();
            
            // 订阅应用程序级别的鼠标按下事件，用于检测点击节点外部
            if (Application.Current?.MainWindow != null)
            {
                Application.Current.MainWindow.PreviewMouseLeftButtonDown += OnApplicationMouseDown;
            }
        }
        
        private void NodeControl_Unloaded(object sender, RoutedEventArgs e)
        {
            // 取消订阅应用程序级别事件，避免内存泄漏
            if (Application.Current?.MainWindow != null)
            {
                Application.Current.MainWindow.PreviewMouseLeftButtonDown -= OnApplicationMouseDown;
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
            // 检查是否点击在按钮上，如果是则不允许拖拽
            if (e.OriginalSource is System.Windows.Controls.Button ||
                e.OriginalSource is System.Windows.Controls.Primitives.ButtonBase)
            {
                return;
            }
            
            // 检查是否是双击（用于重命名）
            if (e.ClickCount == 2)
            {
                // 检查是否点击在标题区域
                var clickedElement = e.OriginalSource as DependencyObject;
                bool isClickOnTitle = false;
                var current = clickedElement;
                while (current != null)
                {
                    if (current == _titleTextBlock)
                    {
                        isClickOnTitle = true;
                        break;
                    }
                    current = VisualTreeHelper.GetParent(current);
                }
                
                if (isClickOnTitle)
                {
                    // 双击标题，进入编辑模式
                    IsEditing = true;
                    e.Handled = true;
                    return;
                }
            }
            
            // 如果当前处于编辑状态
            if (IsEditing && _editTextBox != null)
            {
                // 获取点击的元素
                var clickedElement = e.OriginalSource as DependencyObject;

                // 检查点击的是否是编辑文本框或其子元素
                bool isClickOnTextBox = false;
                var current = clickedElement;
                while (current != null)
                {
                    if (current == _editTextBox)
                    {
                        isClickOnTextBox = true;
                        break;
                    }
                    current = VisualTreeHelper.GetParent(current);
                }

                // 如果点击的不是文本框，退出编辑模式
                if (!isClickOnTextBox)
                {
                    ExitEditMode();
                }
                return;
            }
            
            // 如果不是编辑状态，并且没有按下修饰键，准备拖拽移动
            if (!IsEditing && !_isDragging)
            {
                // 检查是否按下了修饰键（Ctrl/Shift）用于平移画布
                if (Keyboard.Modifiers == ModifierKeys.Control || Keyboard.Modifiers == ModifierKeys.Shift)
                {
                    return;  // 不处理，让 InfiniteCanvas 处理平移
                }
                
                // 查找父画布和 ContentPresenter
                _parentCanvas = FindParentCanvas(this);
                _contentPresenter = FindParentContentPresenter(this);
                
                if (_parentCanvas == null || _contentPresenter == null)
                {
                    return;
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
                
                // 阻止事件冒泡
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
            
            // 如果正在拖拽，使用 RenderTransform 临时移动（流畅拖拽）
            if (_isDragging && _dragTransform != null)
            {
                _dragTransform.X = deltaX;
                _dragTransform.Y = deltaY;
                e.Handled = true;
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
            }
        }
        
        private void StartDragging()
        {
            if (_parentCanvas == null || _contentPresenter == null)
                return;
            
            _isDragging = true;
            Cursor = Cursors.Hand;
            
            // 初始化拖拽变换（仅用于视觉反馈，不影响数据绑定）
            var transformGroup = _contentPresenter.RenderTransform as TransformGroup;
            if (transformGroup == null)
            {
                transformGroup = new TransformGroup();
                _contentPresenter.RenderTransform = transformGroup;
            }
            
            // 添加 TranslateTransform
            _dragTransform = transformGroup.Children.OfType<TranslateTransform>().FirstOrDefault();
            if (_dragTransform == null)
            {
                _dragTransform = new TranslateTransform();
                transformGroup.Children.Add(_dragTransform);
            }
            
            // 重置变换值
            _dragTransform.X = 0;
            _dragTransform.Y = 0;
        }
        
        private void EndDragging()
        {
            if (_dragTransform != null && _parentCanvas != null && _contentPresenter != null)
            {
                // 计算最终位置（画布坐标系）
                var canvasDeltaX = _dragTransform.X / _parentCanvas.Scale;
                var canvasDeltaY = _dragTransform.Y / _parentCanvas.Scale;
                
                var finalCanvasPosition = new Point2D(
                    _dragStartNodePosition.X + canvasDeltaX,
                    _dragStartNodePosition.Y + canvasDeltaY
                );
                
                // 清除拖拽变换
                if (_contentPresenter.RenderTransform is TransformGroup transformGroup)
                {
                    transformGroup.Children.Remove(_dragTransform);
                    if (transformGroup.Children.Count == 0)
                    {
                        _contentPresenter.RenderTransform = null;
                    }
                }
                _dragTransform = null;
                
                // 更新数据模型
                UpdateNodePosition(finalCanvasPosition);
                
                // 直接设置 Canvas 附加属性，确保位置正确（即使绑定不更新）
                Canvas.SetLeft(_contentPresenter, finalCanvasPosition.X);
                Canvas.SetTop(_contentPresenter, finalCanvasPosition.Y);
            }
        }

        private void TitleTextBlock_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                // 双击进入编辑模式
                IsEditing = true;
                e.Handled = true;
            }
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
                if (!string.IsNullOrWhiteSpace(_editTextBox.Text))
                {
                    Title = _editTextBox.Text.Trim();
                }

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
    }
}
