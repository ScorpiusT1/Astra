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
    public class PropertyEditorControl : Control
    {
        private const string PART_SearchBox = "PART_SearchBox";

        private TextBox _searchBox;
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

            CommandBindings.Add(new CommandBinding(ResetPropertyCommand, OnResetProperty));
            CommandBindings.Add(new CommandBinding(AddCollectionItemCommand, OnAddCollectionItem));
           
        }

        #region 依赖属性

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

        public static readonly RoutedCommand ResetPropertyCommand =
            new RoutedCommand(nameof(ResetPropertyCommand), typeof(PropertyEditorControl));

        private void OnResetProperty(object sender, ExecutedRoutedEventArgs e)
        {
            if (e.Parameter is PropertyDescriptor property)
            {
                property.ResetValue();
            }
        }

        public static readonly RoutedCommand AddCollectionItemCommand =
            new RoutedCommand(nameof(AddCollectionItemCommand), typeof(PropertyEditorControl));

        private void OnAddCollectionItem(object sender, ExecutedRoutedEventArgs e)
        {
            if (e.Parameter is PropertyDescriptor property && property.IsCollection)
            {
                _viewModel.AddCollectionItem(property);
            }
        }

        #endregion

        #region 公共方法

        public void Refresh()
        {
            _viewModel.Refresh();
        }

        public void SetPropertyVisibility(string propertyName, bool visible)
        {
            _viewModel.SetPropertyVisibility(propertyName, visible);
        }

        public object GetPropertyValue(string propertyName)
        {
            return _viewModel.GetPropertyValue(propertyName);
        }

        public bool SetPropertyValue(string propertyName, object value)
        {
            return _viewModel.SetPropertyValue(propertyName, value);
        }

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

    public enum PropertySortMode
    {
        Categorized,
        Alphabetical,
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
                Properties = new ObservableCollection<PropertyDescriptor>();
                PropertiesView = null;
                return;
            }

            try
            {
                IEnumerable<PropertyDescriptor> properties;

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

                foreach (var property in Properties)
                {
                    property.PropertyChanged += OnPropertyDescriptorChanged;
                }

                if (_selectedObject is INotifyPropertyChanged notifyObject)
                {
                    _notifyObject = notifyObject;
                    notifyObject.PropertyChanged += OnSelectedObjectPropertyChanged;
                }

                UpdateCategoryGroupOrders();

                var collectionViewSource = new CollectionViewSource
                {
                    Source = Properties,
                    IsLiveFilteringRequested = true
                };

                collectionViewSource.LiveFilteringProperties.Add(nameof(PropertyDescriptor.IsBrowsable));

                PropertiesView = collectionViewSource.View;
                PropertiesView.Filter = FilterProperties;

                UpdateSorting();
                UpdateGrouping();

                UpdatePropertyVisibility();
            }
            catch (Exception ex)
            {
                // 记录错误但不抛出异常，避免影响 UI
                System.Diagnostics.Debug.WriteLine($"[PropertyEditor] 加载属性失败: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[PropertyEditor] 异常详情: {ex}");
                Properties = new ObservableCollection<PropertyDescriptor>();
            }
        }

        private void OnPropertyDescriptorChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(PropertyDescriptor.Value))
            {
                UpdatePropertyVisibility();
            }
        }

        private void OnSelectedObjectPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            UpdatePropertyVisibility();
        }

        private void UpdatePropertyVisibility()
        {
            if (_selectedObject == null || Properties == null)
                return;

            if (_selectedObject is IPropertyVisibilityProvider visibilityProvider)
            {
                foreach (var property in Properties)
                {
                    var shouldBeVisible = visibilityProvider.IsPropertyVisible(property.Name);
                    if (property.IsBrowsable != shouldBeVisible)
                    {
                        property.IsBrowsable = shouldBeVisible;
                    }
                }
                return;
            }

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
                            property.IsBrowsable = shouldBeVisible;
                        }
                    }
                    catch
                    {
                        // 忽略调用失败
                    }
                }
            }
        }

        private bool FilterProperties(object item)
        {
            var property = item as PropertyDescriptor;
            if (property == null)
                return false;

            if (!property.IsBrowsable)
                return false;

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
                    // ✅ 修复：使用 CategoryGroupOrder 而不是 GroupOrder
                    PropertiesView.SortDescriptions.Add(
                        new SortDescription(nameof(PropertyDescriptor.CategoryGroupOrder), ListSortDirection.Ascending));
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

        private void UpdateCategoryGroupOrders()
        {
            if (Properties == null) return;

            var categoryGroupOrders = Properties
                .GroupBy(p => p.Category ?? "常规")
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(p => p.GroupOrder).DefaultIfEmpty(int.MaxValue).Min());

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
                // ✅ 修复：直接使用 Category 属性，不使用转换器
                PropertiesView.GroupDescriptions.Add(
                    new PropertyGroupDescription(nameof(PropertyDescriptor.Category)));
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

        public void AddCollectionItem(PropertyDescriptor property)
        {
            if (property == null || !property.IsCollection)
                return;

            try
            {
                var collection = property.Value as System.Collections.IList;
                if (collection == null && property.Value is System.Collections.IEnumerable enumerable)
                {
                    // 如果是非 IList 的集合，尝试创建新的列表
                    var itemType = property.CollectionItemType ?? typeof(object);
                    collection = System.Collections.ArrayList.Adapter(new System.Collections.ArrayList());

                    // 对于泛型列表，使用反射创建
                    if (property.PropertyType.IsGenericType)
                    {
                        var listType = typeof(List<>).MakeGenericType(itemType);
                        collection = (System.Collections.IList)Activator.CreateInstance(listType);

                        // 将现有项复制到新列表
                        foreach (var item in enumerable)
                        {
                            collection.Add(item);
                        }
                    }
                }

                if (collection != null)
                {
                    var itemType = property.CollectionItemType ?? typeof(object);
                    object newItem = null;

                    // 尝试创建新项
                    if (itemType == typeof(string))
                    {
                        newItem = string.Empty;
                    }
                    else if (itemType.IsValueType)
                    {
                        newItem = Activator.CreateInstance(itemType);
                    }
                    // 对于引用类型，尝试使用默认构造函数创建

                    if (newItem != null || itemType.IsClass)
                    {
                        if (newItem == null && itemType.IsClass)
                        {
                            try
                            {
                                newItem = Activator.CreateInstance(itemType);
                            }
                            catch
                            {
                                // 创建失败，跳过
                                return;
                            }
                        }

                        collection.Add(newItem);
                        property.Value = collection; // 触发更新
                        PropertiesView?.Refresh();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PropertyEditor] 添加集合项失败: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[PropertyEditor] 异常详情: {ex}");
            }
        }

        public void RemoveCollectionItem(PropertyDescriptor property, int itemIndex)
        {
            if (property == null || !property.IsCollection)
                return;

            try
            {
                var collection = property.Value as System.Collections.IList;
                if (collection != null && itemIndex >= 0 && itemIndex < collection.Count)
                {
                    collection.RemoveAt(itemIndex);
                    property.Value = collection; // 触发更新
                    PropertiesView?.Refresh();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PropertyEditor] 删除集合项失败: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[PropertyEditor] 异常详情: {ex}");
            }
        }

        public void RemoveCollectionItem(PropertyDescriptor property, object item)
        {
            if (property == null || !property.IsCollection || item == null)
                return;

            try
            {
                var collection = property.Value as System.Collections.IList;
                if (collection != null)
                {
                    var index = collection.IndexOf(item);
                    if (index >= 0)
                    {
                        collection.RemoveAt(index);
                        property.Value = collection; // 触发更新
                        PropertiesView?.Refresh();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PropertyEditor] 删除集合项失败: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[PropertyEditor] 异常详情: {ex}");
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}