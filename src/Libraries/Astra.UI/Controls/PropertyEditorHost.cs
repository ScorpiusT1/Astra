using Astra.UI.Abstractions.Models;
using Astra.UI.Logging;
using Astra.UI.PropertyEditors;
using System;
using System.Windows;
using System.Windows.Controls;

namespace Astra.UI.Controls
{
    /// <summary>
    /// 属性编辑器宿主控件
    /// 用于在运行时创建 PropertyEditorBase 实例并显示其创建的元素
    /// </summary>
    public class PropertyEditorHost : ContentControl
    {
        private PropertyEditorBase _editor;
        private PropertyDescriptor _propertyDescriptor;
        private static IPropertyEditorLogger _logger = new DefaultPropertyEditorLogger();

        static PropertyEditorHost()
        {
            DefaultStyleKeyProperty.OverrideMetadata(
                typeof(PropertyEditorHost),
                new FrameworkPropertyMetadata(typeof(PropertyEditorHost)));
        }

        public PropertyEditorHost()
        {
            DataContextChanged += PropertyEditorHost_DataContextChanged;
            Loaded += PropertyEditorHost_Loaded;
        }

        private void PropertyEditorHost_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            // 当 DataContext 变化时，如果 PropertyDescriptor 还没有设置，尝试从 DataContext 获取
            if (PropertyDescriptor == null && DataContext is PropertyDescriptor propertyDescriptor)
            {
                PropertyDescriptor = propertyDescriptor;
            }
        }

        private void PropertyEditorHost_Loaded(object sender, RoutedEventArgs e)
        {
            // 在 Loaded 时，如果 PropertyDescriptor 还没有设置，尝试从 DataContext 获取
            if (PropertyDescriptor == null && DataContext is PropertyDescriptor propertyDescriptor)
            {
                PropertyDescriptor = propertyDescriptor;
            }
            
            // 在 Loaded 时尝试初始化，确保所有绑定都已完成
            TryInitializeEditor();
        }

        #region 依赖属性

        public static readonly DependencyProperty EditorTypeProperty =
            DependencyProperty.Register(
                nameof(EditorType),
                typeof(Type),
                typeof(PropertyEditorHost),
                new PropertyMetadata(null, OnEditorTypeChanged));

        public Type EditorType
        {
            get => (Type)GetValue(EditorTypeProperty);
            set => SetValue(EditorTypeProperty, value);
        }

        public static readonly DependencyProperty PropertyDescriptorProperty =
            DependencyProperty.Register(
                nameof(PropertyDescriptor),
                typeof(PropertyDescriptor),
                typeof(PropertyEditorHost),
                new PropertyMetadata(null, OnPropertyDescriptorChanged));

        public PropertyDescriptor PropertyDescriptor
        {
            get => (PropertyDescriptor)GetValue(PropertyDescriptorProperty);
            set => SetValue(PropertyDescriptorProperty, value);
        }

        /// <summary>
        /// 设置日志记录器（用于替换默认日志实现）
        /// </summary>
        public static void SetLogger(IPropertyEditorLogger logger)
        {
            _logger = logger ?? new DefaultPropertyEditorLogger();
        }

        /// <summary>
        /// 获取当前日志记录器
        /// </summary>
        public static IPropertyEditorLogger GetLogger() => _logger;

        private static void OnEditorTypeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var host = (PropertyEditorHost)d;
            // 只有在 EditorType 和 PropertyDescriptor 都已设置时才初始化
            host.TryInitializeEditor();
        }

        private static void OnPropertyDescriptorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var host = (PropertyEditorHost)d;
            // 只有在 EditorType 和 PropertyDescriptor 都已设置时才初始化
            host.TryInitializeEditor();
        }

        #endregion

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            // 只有在 EditorType 和 PropertyDescriptor 都已设置时才初始化
            TryInitializeEditor();
        }

        /// <summary>
        /// 尝试初始化编辑器（只有在 EditorType 和 PropertyDescriptor 都已设置时才初始化）
        /// </summary>
        private void TryInitializeEditor()
        {
            // 如果两个属性都已设置，才执行初始化
            if (EditorType != null && PropertyDescriptor != null)
            {
                InitializeEditor();
            }
        }

        private void InitializeEditor()
        {
            if (EditorType == null || PropertyDescriptor == null)
            {
                Content = null;
                return;
            }

            // 如果已经初始化且属性未变化，跳过
            if (_editor != null && _propertyDescriptor == PropertyDescriptor)
            {
                return;
            }

            try
            {
                // 创建编辑器实例（每个 PropertyDescriptor 创建新实例）
                _editor = Activator.CreateInstance(EditorType) as PropertyEditorBase;
                _propertyDescriptor = PropertyDescriptor;

                if (_editor == null)
                {
                    var errorMessage = $"无法创建编辑器实例：类型 {EditorType?.Name} 不是 PropertyEditorBase 的派生类";
                    _logger.Error(errorMessage);
                    ShowErrorContent(errorMessage);
                    return;
                }

                // 创建元素
                var element = _editor.CreateElement(PropertyDescriptor);
                if (element != null)
                {
                    // 创建绑定
                    _editor.CreateBinding(PropertyDescriptor, element);

                    // 设置内容（布局属性应该由样式和父容器控制，不在这里设置）
                    Content = element;
                    _logger.Debug($"成功初始化属性编辑器: {EditorType.Name}, 属性: {PropertyDescriptor?.Name}");
                }
                else
                {
                    var errorMessage = $"编辑器 {EditorType.Name} 创建的元素为 null";
                    _logger.Warn(errorMessage);
                    Content = null;
                }
            }
            catch (Exception ex)
            {
                var errorMessage = $"初始化属性编辑器失败: {EditorType?.Name}";
                _logger.Error(errorMessage, ex);
                
                var friendlyMessage = CreateFriendlyErrorMessage(ex);
                ShowErrorContent(friendlyMessage);
            }
        }

        /// <summary>
        /// 显示错误内容
        /// </summary>
        private void ShowErrorContent(string message)
        {
            Content = new Border
            {
                Background = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromArgb(20, 255, 0, 0)),
                BorderBrush = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromArgb(200, 255, 0, 0)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(8, 4, 8, 4),
                Child = new TextBlock
                {
                    Text = message,
                    Foreground = new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromRgb(200, 0, 0)),
                    FontSize = 12,
                    TextWrapping = TextWrapping.Wrap,
                    ToolTip = "点击查看详细信息（可在日志中查看完整异常信息）"
                }
            };
        }

        /// <summary>
        /// 创建友好的错误消息
        /// </summary>
        private string CreateFriendlyErrorMessage(Exception ex)
        {
            var baseMessage = $"编辑器初始化失败";
            
            // 根据异常类型提供更友好的提示
            if (ex is MissingMethodException)
            {
                return $"{baseMessage}：编辑器类型 {EditorType?.Name} 缺少无参构造函数";
            }
            else if (ex is InvalidCastException)
            {
                return $"{baseMessage}：编辑器类型 {EditorType?.Name} 无法转换为 PropertyEditorBase";
            }
            else if (ex is TypeLoadException)
            {
                return $"{baseMessage}：无法加载编辑器类型 {EditorType?.Name}";
            }
            
            // 对于其他异常，显示简化消息
            return $"{baseMessage}\n{ex.GetType().Name}: {ex.Message}";
        }
    }
}

