using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace Astra.UI.Controls
{
    /// <summary>
    /// 工作流标签页控件 - 支持自定义标题、标题列表下拉、添加标签、主流程切换
    /// </summary>
    [TemplatePart(Name = PART_TabListButton, Type = typeof(Button))]
    [TemplatePart(Name = PART_AddButton, Type = typeof(Button))]
    [TemplatePart(Name = PART_MasterWorkflowButton, Type = typeof(Button))]
    [TemplatePart(Name = PART_TabListPopup, Type = typeof(Popup))]
    [TemplatePart(Name = PART_TabListItemsControl, Type = typeof(ItemsControl))]
    public class WorkflowTabControl : TabControl
    {
        #region 模板部件名称常量

        private const string PART_TabListButton = "PART_TabListButton";
        private const string PART_AddButton = "PART_AddButton";
        private const string PART_MasterWorkflowButton = "PART_MasterWorkflowButton";
        private const string PART_TabListPopup = "PART_TabListPopup";
        private const string PART_TabListItemsControl = "PART_TabListItemsControl";
        private const string PART_EmptyPlaceholder = "EmptyPlaceholder";

        #endregion

        #region 依赖属性

        /// <summary>
        /// 是否显示标题列表按钮
        /// </summary>
        public static readonly DependencyProperty ShowTabListButtonProperty =
            DependencyProperty.Register(
                nameof(ShowTabListButton),
                typeof(bool),
                typeof(WorkflowTabControl),
                new PropertyMetadata(true));

        public bool ShowTabListButton
        {
            get => (bool)GetValue(ShowTabListButtonProperty);
            set => SetValue(ShowTabListButtonProperty, value);
        }

        /// <summary>
        /// 是否显示添加按钮
        /// </summary>
        public static readonly DependencyProperty ShowAddButtonProperty =
            DependencyProperty.Register(
                nameof(ShowAddButton),
                typeof(bool),
                typeof(WorkflowTabControl),
                new PropertyMetadata(true));

        public bool ShowAddButton
        {
            get => (bool)GetValue(ShowAddButtonProperty);
            set => SetValue(ShowAddButtonProperty, value);
        }

        /// <summary>
        /// 是否显示主流程按钮
        /// </summary>
        public static readonly DependencyProperty ShowMasterWorkflowButtonProperty =
            DependencyProperty.Register(
                nameof(ShowMasterWorkflowButton),
                typeof(bool),
                typeof(WorkflowTabControl),
                new PropertyMetadata(true));

        public bool ShowMasterWorkflowButton
        {
            get => (bool)GetValue(ShowMasterWorkflowButtonProperty);
            set => SetValue(ShowMasterWorkflowButtonProperty, value);
        }

        /// <summary>
        /// 是否显示标签页列表（TabPanel）
        /// </summary>
        public static readonly DependencyProperty ShowTabPanelProperty =
            DependencyProperty.Register(
                nameof(ShowTabPanel),
                typeof(bool),
                typeof(WorkflowTabControl),
                new PropertyMetadata(true));

        public bool ShowTabPanel
        {
            get => (bool)GetValue(ShowTabPanelProperty);
            set => SetValue(ShowTabPanelProperty, value);
        }

        /// <summary>
        /// 是否显示内容区域
        /// </summary>
        public static readonly DependencyProperty ShowContentProperty =
            DependencyProperty.Register(
                nameof(ShowContent),
                typeof(bool),
                typeof(WorkflowTabControl),
                new PropertyMetadata(true));

        public bool ShowContent
        {
            get => (bool)GetValue(ShowContentProperty);
            set => SetValue(ShowContentProperty, value);
        }

        /// <summary>
        /// 主流程按钮内容（图标或文本）
        /// </summary>
        public static readonly DependencyProperty MasterWorkflowButtonContentProperty =
            DependencyProperty.Register(
                nameof(MasterWorkflowButtonContent),
                typeof(object),
                typeof(WorkflowTabControl),
                new PropertyMetadata("📋"));

        public object MasterWorkflowButtonContent
        {
            get => GetValue(MasterWorkflowButtonContentProperty);
            set => SetValue(MasterWorkflowButtonContentProperty, value);
        }

        /// <summary>
        /// 主流程按钮提示文本
        /// </summary>
        public static readonly DependencyProperty MasterWorkflowButtonToolTipProperty =
            DependencyProperty.Register(
                nameof(MasterWorkflowButtonToolTip),
                typeof(string),
                typeof(WorkflowTabControl),
                new PropertyMetadata("主流程编辑"));

        public string MasterWorkflowButtonToolTip
        {
            get => (string)GetValue(MasterWorkflowButtonToolTipProperty);
            set => SetValue(MasterWorkflowButtonToolTipProperty, value);
        }

        /// <summary>
        /// 添加按钮内容（图标或文本）
        /// </summary>
        public static readonly DependencyProperty AddButtonContentProperty =
            DependencyProperty.Register(
                nameof(AddButtonContent),
                typeof(object),
                typeof(WorkflowTabControl),
                new PropertyMetadata("+"));

        public object AddButtonContent
        {
            get => GetValue(AddButtonContentProperty);
            set => SetValue(AddButtonContentProperty, value);
        }

        /// <summary>
        /// 添加按钮提示文本
        /// </summary>
        public static readonly DependencyProperty AddButtonToolTipProperty =
            DependencyProperty.Register(
                nameof(AddButtonToolTip),
                typeof(string),
                typeof(WorkflowTabControl),
                new PropertyMetadata("添加新流程"));

        public string AddButtonToolTip
        {
            get => (string)GetValue(AddButtonToolTipProperty);
            set => SetValue(AddButtonToolTipProperty, value);
        }

        /// <summary>
        /// 标题列表按钮内容（图标或文本）
        /// </summary>
        public static readonly DependencyProperty TabListButtonContentProperty =
            DependencyProperty.Register(
                nameof(TabListButtonContent),
                typeof(object),
                typeof(WorkflowTabControl),
                new PropertyMetadata("☰"));

        public object TabListButtonContent
        {
            get => GetValue(TabListButtonContentProperty);
            set => SetValue(TabListButtonContentProperty, value);
        }

        /// <summary>
        /// 标题列表按钮提示文本
        /// </summary>
        public static readonly DependencyProperty TabListButtonToolTipProperty =
            DependencyProperty.Register(
                nameof(TabListButtonToolTip),
                typeof(string),
                typeof(WorkflowTabControl),
                new PropertyMetadata("显示所有标签页"));

        public string TabListButtonToolTip
        {
            get => (string)GetValue(TabListButtonToolTipProperty);
            set => SetValue(TabListButtonToolTipProperty, value);
        }

        #endregion

        #region 路由事件

        /// <summary>
        /// 添加按钮点击事件
        /// </summary>
        public static readonly RoutedEvent AddButtonClickEvent =
            EventManager.RegisterRoutedEvent(
                nameof(AddButtonClick),
                RoutingStrategy.Bubble,
                typeof(RoutedEventHandler),
                typeof(WorkflowTabControl));

        public event RoutedEventHandler AddButtonClick
        {
            add => AddHandler(AddButtonClickEvent, value);
            remove => RemoveHandler(AddButtonClickEvent, value);
        }

        /// <summary>
        /// 主流程按钮点击事件
        /// </summary>
        public static readonly RoutedEvent MasterWorkflowButtonClickEvent =
            EventManager.RegisterRoutedEvent(
                nameof(MasterWorkflowButtonClick),
                RoutingStrategy.Bubble,
                typeof(RoutedEventHandler),
                typeof(WorkflowTabControl));

        public event RoutedEventHandler MasterWorkflowButtonClick
        {
            add => AddHandler(MasterWorkflowButtonClickEvent, value);
            remove => RemoveHandler(MasterWorkflowButtonClickEvent, value);
        }

        /// <summary>
        /// 标题列表项选择事件（当用户从下拉列表中选择某个标签页时触发）
        /// </summary>
        public static readonly RoutedEvent TabListItemSelectedEvent =
            EventManager.RegisterRoutedEvent(
                nameof(TabListItemSelected),
                RoutingStrategy.Bubble,
                typeof(TabListItemSelectedEventHandler),
                typeof(WorkflowTabControl));

        public event TabListItemSelectedEventHandler TabListItemSelected
        {
            add => AddHandler(TabListItemSelectedEvent, value);
            remove => RemoveHandler(TabListItemSelectedEvent, value);
        }

        #endregion

        #region 静态构造函数

        static WorkflowTabControl()
        {
            DefaultStyleKeyProperty.OverrideMetadata(
                typeof(WorkflowTabControl),
                new FrameworkPropertyMetadata(typeof(WorkflowTabControl)));
        }

        #endregion

        #region 实例字段

        private Button _tabListButton;
        private Button _addButton;
        private Button _masterWorkflowButton;
        private Popup _tabListPopup;
        private ItemsControl _tabListItemsControl;
        private Border _emptyPlaceholder;

        #endregion

        #region 构造函数

        public WorkflowTabControl()
        {
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
            
            // 监听 SelectedItem 变化，更新下拉框中的选中状态
            SelectionChanged += OnSelectionChanged;
        }

        private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // 当 SelectedItem 变化时，更新下拉框中的按钮样式
            UpdateTabListItemsSelection();
        }

        private void UpdateTabListItemsSelection()
        {
            if (_tabListItemsControl == null)
                return;

            // 获取当前选中的 WorkflowTab 对象
            object selectedWorkflowTab = SelectedItem;

            // 遍历所有下拉框按钮，更新选中状态
            if (ItemsSource != null)
            {
                foreach (var dataItem in ItemsSource)
                {
                    var container = _tabListItemsControl.ItemContainerGenerator.ContainerFromItem(dataItem);
                    if (container != null)
                    {
                        var button = FindVisualChild<Button>(container);
                        if (button != null)
                        {
                            // 查找对应的 TabItem
                            TabItem correspondingTabItem = null;
                            foreach (var item in Items)
                            {
                                if (item is TabItem tabItem && (tabItem.DataContext == dataItem || tabItem.Content == dataItem))
                                {
                                    correspondingTabItem = tabItem;
                                    break;
                                }
                            }

                            // 设置 Tag 包含选中状态和 TabItem 引用
                            bool isSelected = dataItem == selectedWorkflowTab;
                            var tagDict = new Dictionary<string, object>
                            {
                                ["IsSelected"] = isSelected,
                                ["DataItem"] = dataItem
                            };
                            if (correspondingTabItem != null)
                            {
                                tagDict["TabItem"] = correspondingTabItem;
                            }
                            button.Tag = tagDict;

                            // 设置附加属性来标记选中状态
                            bool oldValue = GetIsTabListItemSelected(button);
                            if (oldValue != isSelected)
                            {
                                SetIsTabListItemSelected(button, isSelected);
                                // 强制刷新绑定和视觉
                                button.InvalidateProperty(IsTabListItemSelectedProperty);
                                button.InvalidateVisual();
                            }
                        }
                    }
                }
            }
            else
            {
                // 如果没有 ItemsSource，从 Items 中获取
                foreach (var item in Items)
                {
                    if (item is TabItem tabItem)
                    {
                        object dataItem = tabItem.DataContext ?? tabItem.Content;
                        if (dataItem != null)
                        {
                            var container = _tabListItemsControl.ItemContainerGenerator.ContainerFromItem(dataItem);
                            if (container != null)
                            {
                                var button = FindVisualChild<Button>(container);
                                if (button != null)
                                {
                                    bool isSelected = dataItem == selectedWorkflowTab;
                                    var tagDict = new Dictionary<string, object>
                                    {
                                        ["IsSelected"] = isSelected,
                                        ["DataItem"] = dataItem,
                                        ["TabItem"] = tabItem
                                    };
                                    button.Tag = tagDict;

                                    // 设置附加属性来标记选中状态
                                    bool oldValue = GetIsTabListItemSelected(button);
                                    if (oldValue != isSelected)
                                    {
                                        SetIsTabListItemSelected(button, isSelected);
                                        // 强制刷新绑定
                                        button.InvalidateProperty(IsTabListItemSelectedProperty);
                                        button.InvalidateVisual();
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 附加属性：用于标记下拉框按钮是否被选中
        /// </summary>
        public static readonly DependencyProperty IsTabListItemSelectedProperty =
            DependencyProperty.RegisterAttached(
                "IsTabListItemSelected",
                typeof(bool),
                typeof(WorkflowTabControl),
                new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

        public static bool GetIsTabListItemSelected(DependencyObject obj)
        {
            return (bool)obj.GetValue(IsTabListItemSelectedProperty);
        }

        public static void SetIsTabListItemSelected(DependencyObject obj, bool value)
        {
            obj.SetValue(IsTabListItemSelectedProperty, value);
        }

        #endregion

        #region 重写方法

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            // 解绑旧的事件处理器
            UnhookTemplateParts();

            // 获取模板部件
            _tabListButton = GetTemplateChild(PART_TabListButton) as Button;
            _addButton = GetTemplateChild(PART_AddButton) as Button;
            _masterWorkflowButton = GetTemplateChild(PART_MasterWorkflowButton) as Button;
            _tabListPopup = GetTemplateChild(PART_TabListPopup) as Popup;
            _tabListItemsControl = GetTemplateChild(PART_TabListItemsControl) as ItemsControl;

            // 绑定新的事件处理器
            HookTemplateParts();
        }

        #endregion

        #region 私有方法

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            // 监听 ItemsSource 变化，更新标题列表
            if (ItemsSource != null && ItemsSource is INotifyCollectionChanged notifyCollection)
            {
                notifyCollection.CollectionChanged += OnItemsSourceCollectionChanged;
            }

            // 监听 Items 集合变化（当直接操作 Items 时）
            if (Items is INotifyCollectionChanged itemsNotify)
            {
                itemsNotify.CollectionChanged += OnItemsCollectionChanged;
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            // 取消监听
            if (ItemsSource != null && ItemsSource is INotifyCollectionChanged notifyCollection)
            {
                notifyCollection.CollectionChanged -= OnItemsSourceCollectionChanged;
            }

            if (Items is INotifyCollectionChanged itemsNotify)
            {
                itemsNotify.CollectionChanged -= OnItemsCollectionChanged;
            }
        }

        private void OnItemsSourceCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            // 当 ItemsSource 变化时，更新标题列表
            UpdateTabListItems();
        }

        private void OnItemsCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            // 当 Items 集合变化时，更新标题列表
            UpdateTabListItems();
        }

        private void UnhookTemplateParts()
        {
            if (_tabListButton != null)
            {
                _tabListButton.Click -= OnTabListButtonClick;
            }

            if (_addButton != null)
            {
                _addButton.Click -= OnAddButtonClick;
            }

            if (_masterWorkflowButton != null)
            {
                _masterWorkflowButton.Click -= OnMasterWorkflowButtonClick;
            }

            if (_tabListPopup != null)
            {
                _tabListPopup.Opened -= OnTabListPopupOpened;
            }
        }

        private void HookTemplateParts()
        {
            if (_tabListButton != null)
            {
                _tabListButton.Click += OnTabListButtonClick;
            }

            if (_addButton != null)
            {
                _addButton.Click += OnAddButtonClick;
            }

            if (_masterWorkflowButton != null)
            {
                _masterWorkflowButton.Click += OnMasterWorkflowButtonClick;
            }

            if (_tabListPopup != null)
            {
                _tabListPopup.Opened += OnTabListPopupOpened;
            }
        }

        private void OnTabListButtonClick(object sender, RoutedEventArgs e)
        {
            if (_tabListPopup != null)
            {
                _tabListPopup.IsOpen = !_tabListPopup.IsOpen;
            }
        }

        private void OnAddButtonClick(object sender, RoutedEventArgs e)
        {
            var args = new RoutedEventArgs(AddButtonClickEvent, this);
            RaiseEvent(args);
        }

        private void OnMasterWorkflowButtonClick(object sender, RoutedEventArgs e)
        {
            var args = new RoutedEventArgs(MasterWorkflowButtonClickEvent, this);
            RaiseEvent(args);
        }

        private void OnTabListPopupOpened(object sender, EventArgs e)
        {
            // 获取占位符（首次打开时）
            if (_emptyPlaceholder == null && _tabListPopup != null)
            {
                _emptyPlaceholder = FindVisualChild<Border>(_tabListPopup.Child, PART_EmptyPlaceholder);
            }

            UpdateTabListItems();
            // 延迟更新选中状态，确保容器已生成
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (_tabListItemsControl.ItemContainerGenerator.Status == GeneratorStatus.ContainersGenerated)
                {
                    UpdateTabListItemsSelection();
                }
                else
                {
                    // 如果容器还没生成，等待生成完成后再更新
                    _tabListItemsControl.ItemContainerGenerator.StatusChanged += OnItemContainerGeneratorStatusChangedForSelection;
                }
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        private void OnItemContainerGeneratorStatusChangedForSelection(object sender, EventArgs e)
        {
            if (_tabListItemsControl.ItemContainerGenerator.Status == GeneratorStatus.ContainersGenerated)
            {
                _tabListItemsControl.ItemContainerGenerator.StatusChanged -= OnItemContainerGeneratorStatusChangedForSelection;
                UpdateTabListItemsSelection();
            }
        }

        private void UpdateTabListItems()
        {
            if (_tabListItemsControl == null)
                return;

            // 更新标题列表的 ItemsSource（使用 ItemsSource 或从 Items 中提取 DataContext）
            IEnumerable itemsSource = null;
            if (ItemsSource != null)
            {
                // 如果使用 ItemsSource，直接使用它
                itemsSource = ItemsSource;
            }
            else
            {
                // 如果没有 ItemsSource，从 Items 中提取 DataContext（WorkflowTab 对象）
                var dataItems = new System.Collections.Generic.List<object>();
                foreach (var item in Items)
                {
                    if (item is TabItem tabItem && tabItem.DataContext != null)
                    {
                        dataItems.Add(tabItem.DataContext);
                    }
                    else if (item is TabItem tabItem2 && tabItem2.Content != null)
                    {
                        dataItems.Add(tabItem2.Content);
                    }
                }
                itemsSource = dataItems;
            }

            _tabListItemsControl.ItemsSource = itemsSource;

            // 更新占位符的可见性
            UpdateEmptyPlaceholderVisibility(itemsSource);

            // 延迟执行，确保 ItemsControl 已渲染
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (_tabListItemsControl.ItemContainerGenerator.Status == GeneratorStatus.ContainersGenerated)
                {
                    BindTabListItemButtons();
                }
                else
                {
                    // 如果容器还没生成，等待生成完成后再绑定
                    _tabListItemsControl.ItemContainerGenerator.StatusChanged += OnItemContainerGeneratorStatusChanged;
                }
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        private void OnItemContainerGeneratorStatusChanged(object sender, EventArgs e)
        {
            if (_tabListItemsControl.ItemContainerGenerator.Status == GeneratorStatus.ContainersGenerated)
            {
                _tabListItemsControl.ItemContainerGenerator.StatusChanged -= OnItemContainerGeneratorStatusChanged;
                BindTabListItemButtons();
                // 绑定完成后更新选中状态
                UpdateTabListItemsSelection();
            }
        }

        private void BindTabListItemButtons()
        {
            if (_tabListItemsControl == null)
                return;

            // 为每个列表项按钮绑定点击事件
            // 从 ItemsSource 或 Items 中获取数据项，然后找到对应的 TabItem
            IEnumerable dataItems = ItemsSource as IEnumerable ?? Items;
            
            foreach (var dataItem in dataItems)
            {
                // 查找对应的 TabItem（通过 DataContext 或 Content 匹配）
                TabItem correspondingTabItem = null;
                foreach (var item in Items)
                {
                    if (item is TabItem tabItem)
                    {
                        if (tabItem.DataContext == dataItem || tabItem.Content == dataItem)
                        {
                            correspondingTabItem = tabItem;
                            break;
                        }
                    }
                }

                // 查找对应的容器
                var container = _tabListItemsControl.ItemContainerGenerator.ContainerFromItem(dataItem);
                if (container != null)
                {
                    var button = FindVisualChild<Button>(container);
                    if (button != null)
                    {
                        // 移除旧的事件处理器（如果存在）
                        button.Click -= OnTabListItemButtonClick;
                        
                        // 设置 Tag 包含 TabItem 和数据项信息
                        var tagDict = new Dictionary<string, object>
                        {
                            ["DataItem"] = dataItem
                        };
                        if (correspondingTabItem != null)
                        {
                            tagDict["TabItem"] = correspondingTabItem;
                        }
                        button.Tag = tagDict;
                        
                        button.Click += OnTabListItemButtonClick;
                    }
                }
            }

            // 更新选中状态
            UpdateTabListItemsSelection();
        }

        private void OnTabListItemButtonClick(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                // 从按钮的 Tag 中获取信息
                // Tag 可能包含 TabItem 或包含 IsSelected 和 DataItem 的对象
                object workflowTab = null;
                TabItem tabItem = null;

                if (button.Tag is TabItem directTabItem)
                {
                    tabItem = directTabItem;
                    workflowTab = tabItem.DataContext ?? tabItem.Content;
                }
                else if (button.Tag is System.Collections.Generic.Dictionary<string, object> tagDict)
                {
                    // 从字典中获取 DataItem
                    if (tagDict.ContainsKey("DataItem"))
                    {
                        workflowTab = tagDict["DataItem"];
                    }
                    if (tagDict.ContainsKey("TabItem"))
                    {
                        tabItem = tagDict["TabItem"] as TabItem;
                    }
                }
                else
                {
                    // 尝试从按钮的 DataContext 获取（如果按钮直接绑定到 WorkflowTab）
                    workflowTab = button.DataContext;
                }

                // 如果还没有找到 TabItem，尝试通过 workflowTab 查找
                if (tabItem == null && workflowTab != null)
                {
                    foreach (var item in Items)
                    {
                        if (item is TabItem ti)
                        {
                            if (ti.DataContext == workflowTab || ti.Content == workflowTab)
                            {
                                tabItem = ti;
                                break;
                            }
                        }
                    }
                }

                // 设置 SelectedItem 为 WorkflowTab 对象
                if (workflowTab != null)
                {
                    // 先触发事件，再设置 SelectedItem（确保事件处理器能获取到正确的 TabItem）
                    if (tabItem != null)
                    {
                        var args = new TabListItemSelectedEventArgs(TabListItemSelectedEvent, tabItem);
                        RaiseEvent(args);
                    }
                    
                    // 设置 SelectedItem，这会触发双向绑定更新 CurrentTab
                    SelectedItem = workflowTab;
                }
                else if (tabItem != null)
                {
                    // 如果找不到 WorkflowTab，尝试使用 TabItem
                    var args = new TabListItemSelectedEventArgs(TabListItemSelectedEvent, tabItem);
                    RaiseEvent(args);
                    
                    SelectedItem = tabItem.DataContext ?? tabItem.Content ?? tabItem;
                }
                else if (button.DataContext != null)
                {
                    // 如果按钮的 DataContext 是 WorkflowTab，直接使用
                    workflowTab = button.DataContext;
                    SelectedItem = workflowTab;
                    
                    // 尝试找到对应的 TabItem 并触发事件
                    foreach (var item in Items)
                    {
                        if (item is TabItem ti && (ti.DataContext == workflowTab || ti.Content == workflowTab))
                        {
                            var args = new TabListItemSelectedEventArgs(TabListItemSelectedEvent, ti);
                            RaiseEvent(args);
                            break;
                        }
                    }
                }

                // 关闭下拉菜单
                if (_tabListPopup != null)
                {
                    _tabListPopup.IsOpen = false;
                }

                // 更新选中状态（延迟执行，确保 SelectedItem 已更新）
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    UpdateTabListItemsSelection();
                }), System.Windows.Threading.DispatcherPriority.Loaded);
            }
        }

        private static T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null)
                return null;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T result)
                    return result;

                var childOfChild = FindVisualChild<T>(child);
                if (childOfChild != null)
                    return childOfChild;
            }

            return null;
        }

        private static T FindVisualChild<T>(DependencyObject parent, string name) where T : FrameworkElement
        {
            if (parent == null)
                return null;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T typedChild && typedChild.Name == name)
                    return typedChild;

                var childOfChild = FindVisualChild<T>(child, name);
                if (childOfChild != null)
                    return childOfChild;
            }

            return null;
        }

        private void UpdateEmptyPlaceholderVisibility(IEnumerable itemsSource)
        {
            if (_emptyPlaceholder == null)
                return;

            // 检查是否有元素
            bool hasItems = false;
            if (itemsSource != null)
            {
                var enumerator = itemsSource.GetEnumerator();
                hasItems = enumerator.MoveNext();
            }

            // 如果没有元素，显示占位符；如果有元素，隐藏占位符
            _emptyPlaceholder.Visibility = hasItems ? Visibility.Collapsed : Visibility.Visible;
        }

        #endregion
    }

    #region 事件参数类

    /// <summary>
    /// 标题列表项选择事件参数
    /// </summary>
    public class TabListItemSelectedEventArgs : RoutedEventArgs
    {
        public TabItem SelectedTabItem { get; }

        public TabListItemSelectedEventArgs(RoutedEvent routedEvent, TabItem selectedTabItem)
            : base(routedEvent)
        {
            SelectedTabItem = selectedTabItem;
        }
    }

    /// <summary>
    /// 标题列表项选择事件处理器
    /// </summary>
    public delegate void TabListItemSelectedEventHandler(object sender, TabListItemSelectedEventArgs e);

    #endregion
}

