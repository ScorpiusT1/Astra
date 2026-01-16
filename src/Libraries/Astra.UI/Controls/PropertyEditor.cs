using Astra.UI.Abstractions.Interfaces;
using Astra.UI.Abstractions.Models;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using PropertyDescriptor = Astra.UI.Abstractions.Models.PropertyDescriptor;

namespace Astra.UI.Controls
{
    /// <summary>
    /// 企业级属性编辑器控件
    /// </summary>
    [TemplatePart(Name = PART_SearchBox, Type = typeof(TextBox))]
    [TemplatePart(Name = PART_PropertiesContainer, Type = typeof(ItemsControl))]
    public class PropertyEditorControl : Control
    {
        private const string PART_SearchBox = "PART_SearchBox";
        private const string PART_PropertiesContainer = "PART_PropertiesContainer";

        private TextBox _searchBox;
        private ItemsControl _propertiesContainer;
        private PropertyEditorViewModel _viewModel;

        static PropertyEditorControl()
        {
            DefaultStyleKeyProperty.OverrideMetadata(
                typeof(PropertyEditorControl),
                new FrameworkPropertyMetadata(typeof(PropertyEditorControl)));
        }

        public PropertyEditorControl()
        {
            _viewModel = new PropertyEditorViewModel();
            DataContext = _viewModel;

            //CommandBindings.Add(new CommandBinding(EditCollectionCommand, OnEditCollection));
            CommandBindings.Add(new CommandBinding(ResetPropertyCommand, OnResetProperty));
        }

        #region 依赖属性

        /// <summary>
        /// 当前选中的对象
        /// </summary>
        public static readonly DependencyProperty SelectedObjectProperty =
            DependencyProperty.Register(
                nameof(SelectedObject),
                typeof(object),
                typeof(PropertyEditorControl),
                new FrameworkPropertyMetadata(
                    null,
                    FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                    OnSelectedObjectChanged));

        public object SelectedObject
        {
            get => GetValue(SelectedObjectProperty);
            set => SetValue(SelectedObjectProperty, value);
        }

        private static void OnSelectedObjectChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (PropertyEditorControl)d;
            control._viewModel.SetSelectedObject(e.NewValue);
            control.RaiseEvent(new RoutedPropertyChangedEventArgs<object>(
                e.OldValue, e.NewValue, SelectedObjectChangedEvent));
        }

        /// <summary>
        /// 属性过滤文本
        /// </summary>
        public static readonly DependencyProperty FilterTextProperty =
            DependencyProperty.Register(
                nameof(FilterText),
                typeof(string),
                typeof(PropertyEditorControl),
                new PropertyMetadata(string.Empty, OnFilterTextChanged));

        public string FilterText
        {
            get => (string)GetValue(FilterTextProperty);
            set => SetValue(FilterTextProperty, value);
        }

        private static void OnFilterTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (PropertyEditorControl)d;
            control._viewModel.FilterText = e.NewValue as string;
        }

        /// <summary>
        /// 是否显示搜索框
        /// </summary>
        public static readonly DependencyProperty ShowSearchBoxProperty =
            DependencyProperty.Register(
                nameof(ShowSearchBox),
                typeof(bool),
                typeof(PropertyEditorControl),
                new PropertyMetadata(true));

        public bool ShowSearchBox
        {
            get => (bool)GetValue(ShowSearchBoxProperty);
            set => SetValue(ShowSearchBoxProperty, value);
        }

        /// <summary>
        /// 是否启用分类
        /// </summary>
        public static readonly DependencyProperty EnableCategoryProperty =
            DependencyProperty.Register(
                nameof(EnableCategory),
                typeof(bool),
                typeof(PropertyEditorControl),
                new PropertyMetadata(true, OnEnableCategoryChanged));

        public bool EnableCategory
        {
            get => (bool)GetValue(EnableCategoryProperty);
            set => SetValue(EnableCategoryProperty, value);
        }

        private static void OnEnableCategoryChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (PropertyEditorControl)d;
            control._viewModel.EnableCategory = (bool)e.NewValue;
        }

        /// <summary>
        /// 属性排序模式
        /// </summary>
        public static readonly DependencyProperty PropertySortModeProperty =
            DependencyProperty.Register(
                nameof(PropertySortMode),
                typeof(PropertySortMode),
                typeof(PropertyEditorControl),
                new PropertyMetadata(PropertySortMode.Categorized, OnPropertySortModeChanged));

        public PropertySortMode PropertySortMode
        {
            get => (PropertySortMode)GetValue(PropertySortModeProperty);
            set => SetValue(PropertySortModeProperty, value);
        }

        private static void OnPropertySortModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (PropertyEditorControl)d;
            control._viewModel.SortMode = (PropertySortMode)e.NewValue;
        }

        /// <summary>
        /// 是否只读模式
        /// </summary>
        public static readonly DependencyProperty IsReadOnlyProperty =
            DependencyProperty.Register(
                nameof(IsReadOnly),
                typeof(bool),
                typeof(PropertyEditorControl),
                new PropertyMetadata(false));

        public bool IsReadOnly
        {
            get => (bool)GetValue(IsReadOnlyProperty);
            set => SetValue(IsReadOnlyProperty, value);
        }

        /// <summary>
        /// 标签宽度比例 (0-1)
        /// </summary>
        public static readonly DependencyProperty LabelWidthRatioProperty =
            DependencyProperty.Register(
                nameof(LabelWidthRatio),
                typeof(double),
                typeof(PropertyEditorControl),
                new PropertyMetadata(0.4, null, CoerceLabelWidthRatio));

        public double LabelWidthRatio
        {
            get => (double)GetValue(LabelWidthRatioProperty);
            set => SetValue(LabelWidthRatioProperty, value);
        }

        private static object CoerceLabelWidthRatio(DependencyObject d, object baseValue)
        {
            var value = (double)baseValue;
            return Math.Max(0.2, Math.Min(0.8, value));
        }

        /// <summary>
        /// 自定义属性提供器
        /// </summary>
        public static readonly DependencyProperty PropertyProviderProperty =
            DependencyProperty.Register(
                nameof(PropertyProvider),
                typeof(IPropertyProvider),
                typeof(PropertyEditorControl),
                new PropertyMetadata(null, OnPropertyProviderChanged));

        public IPropertyProvider PropertyProvider
        {
            get => (IPropertyProvider)GetValue(PropertyProviderProperty);
            set => SetValue(PropertyProviderProperty, value);
        }

        private static void OnPropertyProviderChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (PropertyEditorControl)d;
            control._viewModel.PropertyProvider = e.NewValue as IPropertyProvider;
        }

        #endregion

        #region 路由事件

        /// <summary>
        /// 选中对象改变事件
        /// </summary>
        public static readonly RoutedEvent SelectedObjectChangedEvent =
            EventManager.RegisterRoutedEvent(
                nameof(SelectedObjectChanged),
                RoutingStrategy.Bubble,
                typeof(RoutedPropertyChangedEventHandler<object>),
                typeof(PropertyEditorControl));

        public event RoutedPropertyChangedEventHandler<object> SelectedObjectChanged
        {
            add => AddHandler(SelectedObjectChangedEvent, value);
            remove => RemoveHandler(SelectedObjectChangedEvent, value);
        }

        /// <summary>
        /// 属性值改变事件
        /// </summary>
        public static readonly RoutedEvent PropertyValueChangedEvent =
            EventManager.RegisterRoutedEvent(
                nameof(PropertyValueChanged),
                RoutingStrategy.Bubble,
                typeof(PropertyValueChangedEventHandler),
                typeof(PropertyEditorControl));

        public event PropertyValueChangedEventHandler PropertyValueChanged
        {
            add => AddHandler(PropertyValueChangedEvent, value);
            remove => RemoveHandler(PropertyValueChangedEvent, value);
        }

        internal void RaisePropertyValueChangedEvent(PropertyDescriptor property, object oldValue, object newValue)
        {
            var args = new PropertyValueChangedEventArgs(
                PropertyValueChangedEvent, property, oldValue, newValue);
            RaiseEvent(args);
        }

        #endregion

        #region 命令

        //public static readonly RoutedCommand EditCollectionCommand =
        //    new RoutedCommand(nameof(EditCollectionCommand), typeof(PropertyEditorControl));

        public static readonly RoutedCommand ResetPropertyCommand =
            new RoutedCommand(nameof(ResetPropertyCommand), typeof(PropertyEditorControl));

        private void OnEditCollection(object sender, ExecutedRoutedEventArgs e)
        {
            //if (e.Parameter is PropertyDescriptor property && property.IsCollection)
            //{
            //    var editor = new CollectionEditorWindow(
            //        property.Value as IEnumerable,
            //        property.CollectionItemType,
            //        this);

            //    if (editor.ShowDialog() == true)
            //    {
            //        var oldValue = property.Value;
            //        property.Value = editor.GetResult();
            //        RaisePropertyValueChangedEvent(property, oldValue, property.Value);
            //    }
            //}
        }

        private void OnResetProperty(object sender, ExecutedRoutedEventArgs e)
        {
            if (e.Parameter is PropertyDescriptor property)
            {
                property.ResetValue();
            }
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 刷新属性列表
        /// </summary>
        public void Refresh()
        {
            _viewModel.Refresh();
        }

        /// <summary>
        /// 设置属性可见性
        /// </summary>
        public void SetPropertyVisibility(string propertyName, bool visible)
        {
            _viewModel.SetPropertyVisibility(propertyName, visible);
        }

        /// <summary>
        /// 获取指定属性的值
        /// </summary>
        public object GetPropertyValue(string propertyName)
        {
            return _viewModel.GetPropertyValue(propertyName);
        }

        /// <summary>
        /// 设置指定属性的值
        /// </summary>
        public bool SetPropertyValue(string propertyName, object value)
        {
            return _viewModel.SetPropertyValue(propertyName, value);
        }

        /// <summary>
        /// 获取所有属性描述器
        /// </summary>
        public IEnumerable<PropertyDescriptor> GetProperties()
        {
            return _viewModel.Properties;
        }

        #endregion

        #region 模板应用

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            _searchBox = GetTemplateChild(PART_SearchBox) as TextBox;
            _propertiesContainer = GetTemplateChild(PART_PropertiesContainer) as ItemsControl;

            if (_searchBox != null)
            {
                _searchBox.SetBinding(TextBox.TextProperty,
                    new Binding(nameof(FilterText))
                    {
                        Source = this,
                        Mode = BindingMode.TwoWay,
                        UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
                    });
            }
        }

        #endregion
    }

    #region 枚举定义

    /// <summary>
    /// 属性排序模式
    /// </summary>
    public enum PropertySortMode
    {
        /// <summary>按类别分组排序</summary>
        Categorized,
        /// <summary>按字母排序</summary>
        Alphabetical,
        /// <summary>按定义顺序</summary>
        DefinitionOrder
    }

    #endregion

    #region 事件参数

    public delegate void PropertyValueChangedEventHandler(object sender, PropertyValueChangedEventArgs e);

    public class PropertyValueChangedEventArgs : RoutedEventArgs
    {
        public PropertyDescriptor Property { get; }
        public object OldValue { get; }
        public object NewValue { get; }

        public PropertyValueChangedEventArgs(
            RoutedEvent routedEvent,
            PropertyDescriptor property,
            object oldValue,
            object newValue)
            : base(routedEvent)
        {
            Property = property;
            OldValue = oldValue;
            NewValue = newValue;
        }
    }

    #endregion


    internal class PropertyEditorViewModel : INotifyPropertyChanged
    {
        private object _selectedObject;
        private INotifyPropertyChanged _notifyObject;
        private ObservableCollection<PropertyDescriptor> _properties;
        private ICollectionView _propertiesView;
        private string _filterText;
        private bool _enableCategory = true;
        private PropertySortMode _sortMode = PropertySortMode.Categorized;
        private IPropertyProvider _propertyProvider;

        public ObservableCollection<PropertyDescriptor> Properties
        {
            get => _properties;
            private set
            {
                _properties = value;
                OnPropertyChanged(nameof(Properties));
            }
        }

        public ICollectionView PropertiesView
        {
            get => _propertiesView;
            private set
            {
                _propertiesView = value;
                OnPropertyChanged(nameof(PropertiesView));
            }
        }

        public string FilterText
        {
            get => _filterText;
            set
            {
                if (_filterText != value)
                {
                    _filterText = value;
                    OnPropertyChanged(nameof(FilterText));
                    PropertiesView?.Refresh();
                }
            }
        }

        public bool EnableCategory
        {
            get => _enableCategory;
            set
            {
                if (_enableCategory != value)
                {
                    _enableCategory = value;
                    OnPropertyChanged(nameof(EnableCategory));
                    UpdateGrouping();
                }
            }
        }

        public PropertySortMode SortMode
        {
            get => _sortMode;
            set
            {
                if (_sortMode != value)
                {
                    _sortMode = value;
                    OnPropertyChanged(nameof(SortMode));
                    UpdateSorting();
                }
            }
        }

        public IPropertyProvider PropertyProvider
        {
            get => _propertyProvider;
            set
            {
                _propertyProvider = value;
                Refresh();
            }
        }

        public void SetSelectedObject(object obj)
        {
            // 取消之前对象的事件订阅
            if (_notifyObject != null)
            {
                _notifyObject.PropertyChanged -= OnSelectedObjectPropertyChanged;
                _notifyObject = null;
            }

            _selectedObject = obj;
            LoadProperties();
        }

        public void Refresh()
        {
            LoadProperties();
        }

        private void LoadProperties()
        {
            if (_selectedObject == null)
            {
                Properties = new ObservableCollection<Astra.UI.Abstractions.Models.PropertyDescriptor>();
                PropertiesView = null;
                return;
            }

            try
            {
                IEnumerable<Astra.UI.Abstractions.Models.PropertyDescriptor> properties;

                if (_propertyProvider != null)
                {
                    properties = _propertyProvider.GetProperties(_selectedObject);
                }
                else
                {
                    properties = _selectedObject.GetType()
                        .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                        .Select(p => new PropertyDescriptor(_selectedObject, p))
                        .Where(p => p.IsBrowsable);
                }

                Properties = new ObservableCollection<PropertyDescriptor>(properties);

                // 订阅属性值改变事件
                foreach (var property in Properties)
                {
                    property.PropertyChanged += OnPropertyDescriptorChanged;
                }

                // 如果对象实现了 INotifyPropertyChanged，订阅其属性变化事件
                if (_selectedObject is INotifyPropertyChanged notifyObject)
                {
                    _notifyObject = notifyObject;
                    notifyObject.PropertyChanged += OnSelectedObjectPropertyChanged;
                }

                // 计算每个分组的 GroupOrder（取该分组中所有属性的最小 GroupOrder）
                UpdateCategoryGroupOrders();

                // 创建视图
                var collectionViewSource = new CollectionViewSource
                {
                    Source = Properties,
                    IsLiveFilteringRequested = true  // 启用实时过滤功能
                };
                
                // 启用实时过滤：当 IsBrowsable 属性改变时，自动重新评估过滤器
                // 这是 WPF 的虚拟化过滤机制，不需要手动刷新整个视图
                collectionViewSource.LiveFilteringProperties.Add(nameof(PropertyDescriptor.IsBrowsable));
                
                PropertiesView = collectionViewSource.View;
                PropertiesView.Filter = FilterProperties;

                UpdateSorting();
                UpdateGrouping();
                
                // 初始检查属性可见性
                UpdatePropertyVisibility();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"加载属性失败: {ex.Message}");
                Properties = new ObservableCollection<PropertyDescriptor>();
            }
        }

        private void OnPropertyDescriptorChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(PropertyDescriptor.Value))
            {
                // 当属性值改变时，检查是否需要更新其他属性的可见性
                UpdatePropertyVisibility();              
            }          
        }

        private void OnSelectedObjectPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // 当绑定对象的属性改变时，更新属性可见性
            UpdatePropertyVisibility();
        }

        private void UpdatePropertyVisibility()
        {
            if (_selectedObject == null || Properties == null)
                return;

            // 如果对象实现了 IPropertyVisibilityProvider，使用它来控制可见性
            if (_selectedObject is IPropertyVisibilityProvider visibilityProvider)
            {
                foreach (var property in Properties)
                {
                    var shouldBeVisible = visibilityProvider.IsPropertyVisible(property.Name);
                    if (property.IsBrowsable != shouldBeVisible)
                    {
                        // 设置 IsBrowsable 会触发 PropertyChanged 事件
                        // OnPropertyDescriptorChanged 会检测到并调用 RefreshViewAsync
                        property.IsBrowsable = shouldBeVisible;
                    }
                }
                // 注意：这里不再直接调用 Refresh()，而是通过 PropertyChanged 事件触发
                return;
            }

            // 否则，尝试通过方法名约定来检查
            // 例如：如果对象有 IsAgeVisible 方法，则检查 Age 属性的可见性
            var objectType = _selectedObject.GetType();
            foreach (var property in Properties)
            {
                var methodName = $"Is{property.Name}Visible";
                var visibilityMethod = objectType.GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
                
                if (visibilityMethod != null && visibilityMethod.ReturnType == typeof(bool))
                {
                    try
                    {
                        var shouldBeVisible = (bool)visibilityMethod.Invoke(_selectedObject, null);
                        if (property.IsBrowsable != shouldBeVisible)
                        {
                            // 设置 IsBrowsable 会触发 PropertyChanged 事件                          
                            property.IsBrowsable = shouldBeVisible;
                        }
                    }
                    catch
                    {
                        // 忽略调用失败
                    }
                }
            }
            // 注意：这里不再直接调用 Refresh()，而是通过 PropertyChanged 事件触发
        }

        private bool FilterProperties(object item)
        {
            var property = item as PropertyDescriptor;
            if (property == null)
                return false;

            // 首先检查 IsBrowsable 属性
            if (!property.IsBrowsable)
                return false;

            // 然后检查搜索文本过滤
            if (string.IsNullOrWhiteSpace(FilterText))
                return true;

            return property.DisplayName?.Contains(FilterText, StringComparison.OrdinalIgnoreCase) ?? false;
        }

        private void UpdateSorting()
        {
            if (PropertiesView == null) return;

            PropertiesView.SortDescriptions.Clear();

            switch (SortMode)
            {
                case PropertySortMode.Categorized:
                    // 先按分组排序（GroupOrder），再按分组名称（Category），最后按属性排序（Order）和显示名称
                    PropertiesView.SortDescriptions.Add(
                        new SortDescription(nameof(PropertyDescriptor.GroupOrder), ListSortDirection.Ascending));
                    PropertiesView.SortDescriptions.Add(
                        new SortDescription(nameof(PropertyDescriptor.Category), ListSortDirection.Ascending));
                    PropertiesView.SortDescriptions.Add(
                        new SortDescription(nameof(PropertyDescriptor.Order), ListSortDirection.Ascending));
                    PropertiesView.SortDescriptions.Add(
                        new SortDescription(nameof(PropertyDescriptor.DisplayName), ListSortDirection.Ascending));
                    break;

                case PropertySortMode.Alphabetical:
                    PropertiesView.SortDescriptions.Add(
                        new SortDescription(nameof(PropertyDescriptor.DisplayName), ListSortDirection.Ascending));
                    break;

                case PropertySortMode.DefinitionOrder:
                    PropertiesView.SortDescriptions.Add(
                        new SortDescription(nameof(PropertyDescriptor.Order), ListSortDirection.Ascending));
                    break;
            }
        }

        /// <summary>
        /// 更新每个分组的 GroupOrder（取该分组中所有属性的最小 GroupOrder）
        /// </summary>
        private void UpdateCategoryGroupOrders()
        {
            if (Properties == null) return;

            // 按 Category 分组，计算每个分组的最小 GroupOrder
            var categoryGroupOrders = Properties
                .GroupBy(p => p.Category ?? "常规")
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(p => p.GroupOrder).DefaultIfEmpty(int.MaxValue).Min());

            // 更新每个属性的 CategoryGroupOrder（用于分组排序）
            foreach (var property in Properties)
            {
                var category = property.Category ?? "常规";
                if (categoryGroupOrders.TryGetValue(category, out var minGroupOrder))
                {
                    property.CategoryGroupOrder = minGroupOrder;
                }
            }
        }

        private void UpdateGrouping()
        {
            if (PropertiesView == null) return;

            PropertiesView.GroupDescriptions.Clear();

            if (EnableCategory && SortMode == PropertySortMode.Categorized)
            {
                // 使用自定义的 PropertyGroupDescription 来支持分组排序
                PropertiesView.GroupDescriptions.Add(
                    new PropertyGroupDescription(nameof(PropertyDescriptor.Category))
                    {
                        Converter = new CategoryGroupConverter()
                    });
            }
        }

        /// <summary>
        /// 分组转换器，用于在分组时包含 GroupOrder 信息
        /// </summary>
        private class CategoryGroupConverter : IValueConverter
        {
            public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
            {
                if (value is PropertyDescriptor property)
                {
                    // 返回包含分组名称和排序顺序的复合键
                    // 格式："{GroupOrder:0000}_{Category}"，这样分组会先按 GroupOrder 排序，再按 Category 排序
                    var category = property.Category ?? "常规";
                    var groupOrder = property.CategoryGroupOrder;
                    return $"{groupOrder:0000}_{category}";
                }
                return value;
            }

            public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
            {
                throw new NotImplementedException();
            }
        }

        public void SetPropertyVisibility(string propertyName, bool visible)
        {
            var property = Properties?.FirstOrDefault(p => p.Name == propertyName);
            if (property != null)
            {
                property.IsBrowsable = visible;
                PropertiesView?.Refresh();
            }
        }

        public object GetPropertyValue(string propertyName)
        {
            return Properties?.FirstOrDefault(p => p.Name == propertyName)?.Value;
        }

        public bool SetPropertyValue(string propertyName, object value)
        {
            var property = Properties?.FirstOrDefault(p => p.Name == propertyName);
            if (property != null)
            {
                try
                {
                    property.Value = value;
                    return true;
                }
                catch
                {
                    return false;
                }
            }
            return false;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

   
    
}
