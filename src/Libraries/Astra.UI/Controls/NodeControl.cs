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
    /// 节点状态枚举
    /// </summary>
    public enum NodeStatus
    {
        Info,
        Success,
        Running,
        Pending,
        Error
    }

    /// <summary>
    /// 流程节点控件
    /// </summary>
    public class NodeControl : Control
    {
        private TextBox _editTextBox;
        private TextBlock _titleTextBlock;

        static NodeControl()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(NodeControl),
                new FrameworkPropertyMetadata(typeof(NodeControl)));
        }

        public NodeControl()
        {
            Loaded += NodeControl_Loaded;
            // 添加鼠标按下事件监听
            AddHandler(PreviewMouseDownEvent, new MouseButtonEventHandler(OnPreviewMouseDown), true);
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
        /// 节点状态
        /// </summary>
        public static readonly DependencyProperty StatusProperty =
            DependencyProperty.Register("Status", typeof(NodeStatus), typeof(NodeControl),
                new PropertyMetadata(NodeStatus.Success, OnStatusChanged));

        public NodeStatus Status
        {
            get { return (NodeStatus)GetValue(StatusProperty); }
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

        private void OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
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
        /// </summary>
        private Brush GetBrushFromResource(string resourceKey, Color fallbackColor)
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

            // 如果找不到资源，使用回退颜色
            return new SolidColorBrush(fallbackColor);
        }

        private void UpdateStatusColors()
        {
            // 如果已经手动设置了IconColor，不自动更新
            if (IconColor != null && ReadLocalValue(IconColorProperty) != DependencyProperty.UnsetValue)
            {
                return;
            }

            switch (Status)
            {
                case NodeStatus.Success:
                    IconColor = GetBrushFromResource("SuccessBrush", Color.FromRgb(76, 175, 80));
                    break;
                case NodeStatus.Running:
                    IconColor = GetBrushFromResource("RunningBrush", Color.FromRgb(33, 150, 243));
                    break;
                case NodeStatus.Pending:
                    IconColor = GetBrushFromResource("PendingBrush", Color.FromRgb(255, 152, 0));
                    break;
                case NodeStatus.Error:
                    IconColor = GetBrushFromResource("ErrorBrush", Color.FromRgb(244, 67, 54));
                    break;
                case NodeStatus.Info:
                    IconColor = GetBrushFromResource("InfoBrush", Color.FromRgb(0, 188, 212));
                    break;
                default:
                    IconColor = GetBrushFromResource("PrimaryBrush", Color.FromRgb(33, 150, 243));
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
                case NodeStatus.Success:
                    // 勾选图标
                    pathData = "M9 12L11 14L15 10M21 12C21 16.9706 16.9706 21 12 21C7.02944 21 3 16.9706 3 12C3 7.02944 7.02944 3 12 3C16.9706 3 21 7.02944 21 12Z";
                    break;
                case NodeStatus.Running:
                    // 加载/旋转图标
                    pathData = "M12 2V6M12 18V22M4.93 4.93L7.76 7.76M16.24 16.24L19.07 19.07M2 12H6M18 12H22M4.93 19.07L7.76 16.24M16.24 7.76L19.07 4.93";
                    break;
                case NodeStatus.Pending:
                    // 时钟图标
                    pathData = "M21 12C21 16.9706 16.9706 21 12 21C7.02944 21 3 16.9706 3 12C3 7.02944 7.02944 3 12 3C16.9706 3 21 7.02944 21 12Z M12 7V12L15 15";
                    break;
                case NodeStatus.Error:
                    // 警告图标
                    pathData = "M12 8V12M12 16H12.01M21 12C21 16.9706 16.9706 21 12 21C7.02944 21 3 16.9706 3 12C3 7.02944 7.02944 3 12 3C16.9706 3 21 7.02944 21 12Z";
                    break;
                case NodeStatus.Info:
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
