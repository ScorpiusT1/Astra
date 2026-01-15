using Astra.UI.Controls;
using Astra.UI.Models;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Astra.UI.Styles.Controls
{
    /// <summary>
    /// SequenceView.xaml 的交互逻辑
    /// </summary>
    public partial class MultiFlowEditor : UserControl
    {
        private ViewModels.MultiFlowEditorViewModel _currentViewModel;
        private System.Collections.ObjectModel.ObservableCollection<WorkflowTab> _subscribedTabs;

        public MultiFlowEditor()
        {
            InitializeComponent();
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
            DataContextChanged += OnDataContextChanged;
            
            // 订阅 PreviewMouseDown 事件，用于检测点击外部区域取消重命名
            PreviewMouseDown += OnPreviewMouseDown;
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            Debug.WriteLine($"[SequenceView] DataContext 变更: {e.NewValue != null}");

            // 取消订阅旧的 ViewModel
            if (_currentViewModel != null && _subscribedTabs != null)
            {
                _subscribedTabs.CollectionChanged -= OnSubWorkflowTabsCollectionChanged;
                UnsubscribeFromTabs(_subscribedTabs);
                _subscribedTabs = null;
            }

            if (e.NewValue is ViewModels.MultiFlowEditorViewModel viewModel)
            {
                _currentViewModel = viewModel;

                // 订阅 SubWorkflowTabs 集合变化
                if (viewModel.SubWorkflowTabs != null)
                {
                    _subscribedTabs = viewModel.SubWorkflowTabs;
                    _subscribedTabs.CollectionChanged += OnSubWorkflowTabsCollectionChanged;
                    SubscribeToTabs(_subscribedTabs);
                }
            }
            else
            {
                _currentViewModel = null;
            }
        }

        private void OnSubWorkflowTabsCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                foreach (WorkflowTab tab in e.NewItems)
                {
                    SubscribeToTab(tab);
                }
            }

            if (e.OldItems != null)
            {
                foreach (WorkflowTab tab in e.OldItems)
                {
                    UnsubscribeFromTab(tab);
                }
            }
        }

        private void SubscribeToTabs(System.Collections.ObjectModel.ObservableCollection<WorkflowTab> tabs)
        {
            if (tabs == null) return;
            foreach (var tab in tabs)
            {
                SubscribeToTab(tab);
            }
        }

        private void UnsubscribeFromTabs(System.Collections.ObjectModel.ObservableCollection<WorkflowTab> tabs)
        {
            if (tabs == null) return;
            foreach (var tab in tabs)
            {
                UnsubscribeFromTab(tab);
            }
        }

        private void SubscribeToTab(WorkflowTab tab)
        {
            if (tab == null) return;
            tab.PropertyChanged += OnWorkflowTabPropertyChanged;
        }

        private void UnsubscribeFromTab(WorkflowTab tab)
        {
            if (tab == null) return;
            tab.PropertyChanged -= OnWorkflowTabPropertyChanged;
        }

        private void OnWorkflowTabPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // 标准 TabControl 不需要手动刷新绑定，数据绑定会自动更新
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            // 标准 TabControl 不需要特殊事件订阅，关闭按钮通过命令绑定处理
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            // 清理编辑状态
            _editingTextBox = null;
            _editingTab = null;
        }



        /// <summary>
        /// TabControl 选择变更事件处理（支持 WorkflowTabControl 和标准 TabControl）
        /// </summary>
        private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Models.WorkflowTab selectedTab = null;

            // 支持 WorkflowTabControl 和标准 TabControl
            if (sender is UI.Controls.WorkflowTabControl workflowTabControl)
            {
                // WorkflowTabControl 的 SelectedItem 直接是 WorkflowTab
                selectedTab = workflowTabControl.SelectedItem as Models.WorkflowTab;
            }
            else if (sender is System.Windows.Controls.TabControl tabControl)
            {
                // 标准 TabControl 的 SelectedItem 可能是 TabItem 或 WorkflowTab
                if (tabControl.SelectedItem is Models.WorkflowTab tab)
                {
                    selectedTab = tab;
                }
                else if (tabControl.SelectedItem is System.Windows.Controls.TabItem tabItem && tabItem.Content is Models.WorkflowTab contentTab)
                {
                    selectedTab = contentTab;
                }
            }

            if (selectedTab != null && DataContext is ViewModels.MultiFlowEditorViewModel viewModel)
            {
                // 只有当切换的标签页不是当前活动标签页时才执行切换命令
                if (selectedTab != viewModel.CurrentTab)
                {
                    viewModel.SwitchWorkflowCommand.Execute(selectedTab);
                }
            }
        }

        /// <summary>
        /// WorkflowTabControl 添加按钮点击事件处理
        /// </summary>
        private void WorkflowTabControl_AddButtonClick(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.MultiFlowEditorViewModel viewModel)
            {
                // 执行添加新流程命令
                if (viewModel.AddNewWorkflowCommand?.CanExecute(null) == true)
                {
                    viewModel.AddNewWorkflowCommand.Execute(null);
                }
            }
        }

        /// <summary>
        /// WorkflowTabControl 主流程按钮点击事件处理
        /// </summary>
        private void WorkflowTabControl_MasterWorkflowButtonClick(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.MultiFlowEditorViewModel viewModel)
            {
                // 执行打开主流程命令
                if (viewModel.OpenMasterWorkflowCommand?.CanExecute(null) == true)
                {
                    viewModel.OpenMasterWorkflowCommand.Execute(null);
                }
            }
        }

        /// <summary>
        /// WorkflowTabControl 标题列表项选择事件处理
        /// </summary>
        private void WorkflowTabControl_TabListItemSelected(object sender, UI.Controls.TabListItemSelectedEventArgs e)
        {
            Models.WorkflowTab selectedTab = null;

            // SelectedTabItem 可能是 TabItem 或直接是 WorkflowTab
            if (e.SelectedTabItem?.Content is Models.WorkflowTab contentTab)
            {
                selectedTab = contentTab;
            }
            else if (e.SelectedTabItem?.DataContext is Models.WorkflowTab dataContextTab)
            {
                selectedTab = dataContextTab;
            }
            else if (e.SelectedTabItem.DataContext is WorkflowTab directTab)
            {
                selectedTab = directTab;
            }

            if (selectedTab != null && DataContext is ViewModels.MultiFlowEditorViewModel viewModel)
            {
                // 切换到选中的标签页
                if (viewModel.SwitchWorkflowCommand?.CanExecute(selectedTab) == true)
                {
                    viewModel.SwitchWorkflowCommand.Execute(selectedTab);
                }
            }
        }

        private static WorkflowTab GetWorkflowTabFromMenuItem(object sender)
        {
            if (sender is MenuItem menuItem && menuItem.CommandParameter is WorkflowTab commandTab)
            {
                return commandTab;
            }

            if (sender is MenuItem mi && mi.Parent is ContextMenu contextMenu)
            {
                if (contextMenu.PlacementTarget is FrameworkElement placementElement)
                {
                    return placementElement.DataContext as WorkflowTab;
                }
            }
            return null;
        }

        //private void RenameMenuItem_Click(object sender, RoutedEventArgs e)
        //{
        //    var tab = GetWorkflowTabFromMenuItem(sender);
        //    if (tab == null || DataContext is not ViewModels.SequenceViewModel viewModel)
        //        return;

        //    if (viewModel.BeginEditWorkflowNameCommand.CanExecute(tab))
        //    {
        //        viewModel.BeginEditWorkflowNameCommand.Execute(tab);
        //        FocusTabHeaderTextBox(tab);
        //    }
        //}

        private void ExportMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var tab = GetWorkflowTabFromMenuItem(sender);
            if (tab == null || DataContext is not ViewModels.MultiFlowEditorViewModel viewModel)
                return;

            if (viewModel.ExportWorkflowCommand.CanExecute(tab))
            {
                viewModel.ExportWorkflowCommand.Execute(tab);
            }
        }

        private void CloseMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var tab = GetWorkflowTabFromMenuItem(sender);
            if (tab == null || DataContext is not ViewModels.MultiFlowEditorViewModel viewModel)
                return;

            if (viewModel.CloseWorkflowCommand.CanExecute(tab))
            {
                viewModel.CloseWorkflowCommand.Execute(tab);
            }
        }

        private void CloseOtherMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var tab = GetWorkflowTabFromMenuItem(sender);
            if (tab == null || DataContext is not ViewModels.MultiFlowEditorViewModel viewModel)
                return;

            if (viewModel.CloseOtherWorkflowsCommand.CanExecute(tab))
            {
                viewModel.CloseOtherWorkflowsCommand.Execute(tab);
            }
        }

        private void CloseAllMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not ViewModels.MultiFlowEditorViewModel viewModel)
                return;

            if (viewModel.CloseAllWorkflowsCommand.CanExecute(null))
            {
                viewModel.CloseAllWorkflowsCommand.Execute(null);
            }
        }

        private void FocusTabHeaderTextBox(WorkflowTab tab)
        {
            if (tab == null)
                return;

            var container = SubWorkflowTabControl.ItemContainerGenerator.ContainerFromItem(tab) as DependencyObject;
            if (container == null)
                return;

            var textBox = FindVisualChild<TextBox>(container, tb => ReferenceEquals(tb.DataContext, tab));
            if (textBox == null)
                return;

            textBox.Dispatcher.BeginInvoke(new Action(() =>
            {
                textBox.Focus();
                textBox.SelectAll();
            }), System.Windows.Threading.DispatcherPriority.Input);
        }


        /// <summary>
        /// 关闭标签页按钮点击事件处理（备用方法，如果 ClosingTabItem 不工作）
        /// </summary>
        private void CloseTabButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button button &&
                button.DataContext is Models.WorkflowTab tab)
            {
                if (DataContext is ViewModels.MultiFlowEditorViewModel viewModel)
                {
                    viewModel.CloseWorkflowCommand.Execute(tab);
                }
            }
            // 阻止事件冒泡，避免触发标签页选择
            e.Handled = true;
        }

        /// <summary>
        /// 标签头部点击事件处理
        /// </summary>
        private void TabHeader_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is Models.WorkflowTab tab)
            {
                if (DataContext is ViewModels.MultiFlowEditorViewModel viewModel)
                {
                    viewModel.SwitchWorkflowCommand.Execute(tab);
                }
            }
        }

        /// <summary>
        /// FlowEditor 加载事件处理
        /// 确保 ItemTemplateSelector 正确应用（作为备用机制）
        /// </summary>
        private void FlowEditor_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is FlowEditor flowEditor)
            {
                // 延迟执行，确保所有子元素都已加载
                flowEditor.Dispatcher.BeginInvoke(new System.Action(() =>
                {
                    // 尝试从 FlowEditor 的父级（TabItem 的 Content）获取 WorkflowTab
                    var workflowTab = flowEditor.DataContext as Models.WorkflowTab;
                    Debug.WriteLine($"[FlowEditor_Loaded] FlowEditor 加载, DataContext: {workflowTab?.Name ?? "null"}");
                    Debug.WriteLine($"[FlowEditor_Loaded] FlowEditor.DataContext 类型: {flowEditor.DataContext?.GetType().Name ?? "null"}");
                    Debug.WriteLine($"[FlowEditor_Loaded] CanvasItemsSource: {flowEditor.CanvasItemsSource?.GetHashCode()}, Count: {(flowEditor.CanvasItemsSource as System.Collections.IList)?.Count}");
                    Debug.WriteLine($"[FlowEditor_Loaded] WorkflowTab.Nodes: {workflowTab?.Nodes?.GetHashCode()}, Count: {workflowTab?.Nodes?.Count}");
                    Debug.WriteLine($"[FlowEditor_Loaded] 集合是否相同: {ReferenceEquals(flowEditor.CanvasItemsSource, workflowTab?.Nodes)}");
                    
                    if (workflowTab == null)
                    {
                        // 如果 DataContext 不是 WorkflowTab，尝试从父级查找
                        var parent = VisualTreeHelper.GetParent(flowEditor);
                        while (parent != null)
                        {
                            if (parent is FrameworkElement fe && fe.DataContext is Models.WorkflowTab tab)
                            {
                                workflowTab = tab;
                                Debug.WriteLine($"[FlowEditor_Loaded] 从父级找到 WorkflowTab: {workflowTab.Name}");
                                break;
                            }
                            parent = VisualTreeHelper.GetParent(parent);
                        }
                    }

                    if (workflowTab != null)
                    {
                        // 将 FlowEditor 的 CommandManager 设置到 WorkflowTab
                        // 这样每个流程都有独立的命令历史
                        // 使用全局的 CommandManager，而不是 FlowEditor 自己的
                        if (DataContext is ViewModels.MultiFlowEditorViewModel viewModel)
                        {
                            var globalCommandManager = viewModel.GetGlobalCommandManager();
                            if (globalCommandManager != null)
                            {
                                flowEditor.CommandManager = globalCommandManager;
                                workflowTab.CommandManager = globalCommandManager;
                                Debug.WriteLine($"[SequenceView] 为 {workflowTab.Name} 设置全局 CommandManager");
                            }
                        }
                        else if (flowEditor.CommandManager != null && workflowTab.CommandManager == null)
                        {
                            // 回退：如果没有全局 CommandManager，使用 FlowEditor 自己的
                            workflowTab.CommandManager = flowEditor.CommandManager;
                            Debug.WriteLine($"[SequenceView] 为 {workflowTab.Name} 设置 FlowEditor CommandManager（回退）");
                        }
                        
                        // 根据 WorkflowTab 的 Type 获取正确的模板选择器
                        var converter = this.TryFindResource("WorkflowTypeToTemplateSelectorConverter") as Converters.WorkflowTypeToTemplateSelectorConverter;
                        if (converter != null)
                        {
                            var selector = converter.Convert(workflowTab.Type, typeof(DataTemplateSelector), null, System.Globalization.CultureInfo.CurrentCulture) as DataTemplateSelector;
                            if (selector != null)
                            {
                                // 先清空，再设置，确保触发变更
                                flowEditor.ItemTemplateSelector = null;
                                flowEditor.Dispatcher.BeginInvoke(new System.Action(() =>
                                {
                                    flowEditor.ItemTemplateSelector = selector;
                                    Debug.WriteLine($"[SequenceView] 为 {workflowTab.Name} (Type: {workflowTab.Type}) 设置 ItemTemplateSelector: {selector.GetType().Name}");

                                    // 检查模板选择器的配置
                                    if (selector is Converters.NodeTypeToDataTemplateSelector nodeSelector)
                                    {
                                        Debug.WriteLine($"[SequenceView] WorkflowReferenceNodeTemplate: {nodeSelector.WorkflowReferenceNodeTemplate != null}");
                                        Debug.WriteLine($"[SequenceView] DefaultNodeTemplate: {nodeSelector.DefaultNodeTemplate != null}");

                                        // 检查主流程中的节点类型
                                        if (workflowTab.Nodes != null && workflowTab.Nodes.Count > 0)
                                        {
                                            foreach (var node in workflowTab.Nodes.Take(3)) // 只检查前3个节点
                                            {
                                                Debug.WriteLine($"[SequenceView] 节点: {node.Name}, NodeType: {node.NodeType}, 类型: {node.GetType().Name}");
                                            }
                                        }
                                    }
                                }), System.Windows.Threading.DispatcherPriority.Loaded);
                            }
                        }
                    }
                    else
                    {
                        // 如果找不到 WorkflowTab，尝试使用当前的 ItemTemplateSelector（从绑定获取）
                        var currentSelector = flowEditor.ItemTemplateSelector;
                        if (currentSelector != null)
                        {
                            // 临时清空再设置，强制触发更新
                            flowEditor.ItemTemplateSelector = null;
                            flowEditor.ItemTemplateSelector = currentSelector;
                            Debug.WriteLine($"[SequenceView] 强制刷新 ItemTemplateSelector: {currentSelector.GetType().Name}");
                        }
                    }
                }), System.Windows.Threading.DispatcherPriority.Loaded);
            }
        }

        /// <summary>
        /// 在可视化树中查找指定类型的子元素
        /// </summary>
        private static T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T result)
                {
                    return result;
                }
                var childOfChild = FindVisualChild<T>(child);
                if (childOfChild != null)
                {
                    return childOfChild;
                }
            }
            return null;
        }

        private static T FindVisualChild<T>(DependencyObject parent, Func<T, bool> predicate) where T : DependencyObject
        {
            if (predicate == null)
            {
                return FindVisualChild<T>(parent);
            }

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T result)
                {
                    if (predicate(result))
                    {
                        return result;
                    }
                }
                var childOfChild = FindVisualChild(child, predicate);
                if (childOfChild != null)
                {
                    return childOfChild;
                }
            }
            return null;
        }



        /// <summary>
        /// 编辑名称 TextBox 可见性变化时自动全选文本并设置焦点
        /// </summary>
        private void EditNameTextBox_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                // 只有在变为可见时才处理
                if (!(bool)e.NewValue)
                {
                    return;
                }

                var tab = textBox.DataContext as Models.WorkflowTab;
                if (tab == null || !tab.IsInEditMode)
                {
                    return;
                }

                System.Diagnostics.Debug.WriteLine($"[SequenceView] TextBox IsVisibleChanged 事件触发: Text={textBox.Text}, IsInEditMode={tab.IsInEditMode}, NewValue={e.NewValue}");

                // 确保 TextBox 可以输入
                textBox.IsReadOnly = false;
                textBox.IsEnabled = true;
                textBox.Focusable = true;

                // 使用最高优先级的 Dispatcher 延迟执行，确保文本已加载并且UI已更新
                textBox.Dispatcher.BeginInvoke(new Action(() =>
                {
                    // 再次确保可以输入
                    textBox.IsReadOnly = false;
                    textBox.IsEnabled = true;
                    textBox.Focusable = true;

                    // 设置焦点并全选文本
                    if (textBox.Focus())
                    {
                        textBox.SelectAll();
                        System.Diagnostics.Debug.WriteLine($"[SequenceView] TextBox 变为可见并全选文本: Text={textBox.Text}, IsFocused={textBox.IsFocused}, SelectionLength={textBox.SelectionLength}");
                    }
                    else
                    {
                        // 如果焦点设置失败，再次尝试
                        textBox.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            textBox.Focus();
                            textBox.SelectAll();
                            System.Diagnostics.Debug.WriteLine($"[SequenceView] TextBox 延迟设置焦点并全选: SelectionLength={textBox.SelectionLength}");
                        }), System.Windows.Threading.DispatcherPriority.Input);
                    }
                }), System.Windows.Threading.DispatcherPriority.Loaded);
            }
        }

        /// <summary>
        /// 编辑名称 TextBox 获得焦点时自动全选文本
        /// </summary>
        private void EditNameTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                System.Diagnostics.Debug.WriteLine($"[SequenceView] TextBox GotFocus 事件触发: Text={textBox.Text}, IsFocused={textBox.IsFocused}");

                // 确保 TextBox 可以输入
                textBox.IsReadOnly = false;
                textBox.IsEnabled = true;
                textBox.Focusable = true;

                // 立即全选文本
                textBox.SelectAll();
                
                System.Diagnostics.Debug.WriteLine($"[SequenceView] TextBox 获得焦点并全选文本: Text={textBox.Text}, SelectionLength={textBox.SelectionLength}");

                // 使用 Dispatcher 延迟执行，确保全选生效
                textBox.Dispatcher.BeginInvoke(new Action(() =>
                {
                    textBox.SelectAll();
                    System.Diagnostics.Debug.WriteLine($"[SequenceView] TextBox 延迟全选: SelectionLength={textBox.SelectionLength}");
                }), System.Windows.Threading.DispatcherPriority.Input);
            }
        }

        // 当前正在编辑的 TextBox（用于检测点击外部区域）
        private TextBox _editingTextBox;
        private Models.WorkflowTab _editingTab;

        /// <summary>
        /// 编辑名称 TextBox 失去焦点事件处理
        /// 不在这里取消编辑，而是等待用户点击外部区域
        /// </summary>
        private void EditNameTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                var tab = textBox.DataContext as Models.WorkflowTab;
                if (tab == null || !tab.IsInEditMode)
                {
                    _editingTextBox = null;
                    _editingTab = null;
                    return;
                }

                // 保存当前编辑的 TextBox 和 Tab，等待用户点击外部区域
                _editingTextBox = textBox;
                _editingTab = tab;
                
                System.Diagnostics.Debug.WriteLine($"[SequenceView] EditNameTextBox_LostFocus: 保存编辑状态，等待用户点击外部区域");
                // 不在这里取消编辑，等待 OnPreviewMouseDown 事件处理
            }
        }

        /// <summary>
        /// UserControl 级别的预览鼠标按下事件处理（用于检测点击编辑框外部）
        /// </summary>
        private void OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"[SequenceView] OnPreviewMouseDown: 鼠标按下事件触发, Source={e.Source?.GetType().Name}, OriginalSource={e.OriginalSource?.GetType().Name}");
            
            if (_editingTextBox == null || _editingTab == null || !_editingTab.IsInEditMode)
            {
                // 清空编辑状态
                _editingTextBox = null;
                _editingTab = null;
                return;
            }

            System.Diagnostics.Debug.WriteLine($"[SequenceView] OnPreviewMouseDown: 检测到正在编辑，检查点击位置");

            // 获取点击的元素
            var hitElement = e.OriginalSource as DependencyObject;
            if (hitElement == null)
            {
                System.Diagnostics.Debug.WriteLine($"[SequenceView] OnPreviewMouseDown: hitElement 为 null");
                return;
            }

            // 检查点击的元素是否是 TextBox 本身或其子元素
            var isClickOnTextBox = hitElement == _editingTextBox || 
                                   FindAncestor<TextBox>(hitElement) == _editingTextBox;
            
            if (isClickOnTextBox)
            {
                // 点击在 TextBox 内，重新获得焦点并全选文本
                System.Diagnostics.Debug.WriteLine($"[SequenceView] OnPreviewMouseDown: 点击在 TextBox 内，重新获得焦点");
                _editingTextBox.Focus();
                _editingTextBox.SelectAll();
                return;
            }

            // 检查点击的元素是否在同一个 TabItem 内
            var textBoxTabItem = FindAncestor<TabItem>(_editingTextBox);
            var hitTabItem = FindAncestor<TabItem>(hitElement);
            
            // 如果点击在同一个 TabItem 内，不取消编辑（可能是点击了关闭按钮等）
            if (hitTabItem != null && hitTabItem == textBoxTabItem)
            {
                System.Diagnostics.Debug.WriteLine($"[SequenceView] OnPreviewMouseDown: 点击在同一个 TabItem 内，不取消编辑");
                return;
            }

            // 用户点击了外部区域，提交编辑（保存新名称）
            System.Diagnostics.Debug.WriteLine($"[SequenceView] OnPreviewMouseDown: 用户点击了外部区域，提交编辑");
            
            // 先更新绑定值（确保 EditingName 已更新）
            var bindingExpression = _editingTextBox.GetBindingExpression(TextBox.TextProperty);
            bindingExpression?.UpdateSource();
            
            // 执行提交编辑命令（保存新名称）
            var sequenceViewModel = FindSequenceViewModel(_editingTextBox);
            if (sequenceViewModel != null)
            {
                sequenceViewModel.CommitEditWorkflowNameCommand.Execute(_editingTab);
            }
            
            // 清空编辑状态
            _editingTextBox = null;
            _editingTab = null;
        }

        /// <summary>
        /// 在视觉树中向上查找指定类型的祖先元素
        /// </summary>
        private static T FindAncestor<T>(DependencyObject element) where T : DependencyObject
        {
            var current = element;
            while (current != null)
            {
                if (current is T result)
                {
                    return result;
                }
                current = System.Windows.Media.VisualTreeHelper.GetParent(current);
            }
            return null;
        }

        /// <summary>
        /// 编辑名称 TextBox 预览按键事件处理（阻止事件冒泡，防止触发TabItem的关闭等操作）
        /// </summary>
        private void EditNameTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                // 如果是回车键或ESC键，阻止事件冒泡到父控件（防止触发TabItem的关闭等操作）
                if (e.Key == Key.Enter || e.Key == Key.Escape)
                {
                    e.Handled = true;
                    System.Diagnostics.Debug.WriteLine($"[SequenceView] EditNameTextBox_PreviewKeyDown: 阻止 {e.Key} 键事件冒泡");

                    var tab = textBox.DataContext as Models.WorkflowTab;
                    var sequenceViewModel = FindSequenceViewModel(textBox);
                    if (tab == null || sequenceViewModel == null)
                    {
                        return;
                    }

                    if (e.Key == Key.Enter)
                    {
                        System.Diagnostics.Debug.WriteLine($"[SequenceView] EditNameTextBox_PreviewKeyDown: 回车键触发重命名确认");
                        
                        // 先更新 EditingName（确保绑定值已更新）
                        var bindingExpression = textBox.GetBindingExpression(TextBox.TextProperty);
                        bindingExpression?.UpdateSource();
                        
                        // 执行提交命令
                        sequenceViewModel.CommitEditWorkflowNameCommand.Execute(tab);
                        
                        // 清空编辑状态（在命令执行后）
                        _editingTextBox = null;
                        _editingTab = null;
                        
                        // 让 TextBox 失去焦点，以便 UI 能够正确更新（退出编辑模式）
                        // 使用 Dispatcher 延迟执行，确保命令已处理完成
                        textBox.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            // 检查是否仍在编辑模式（如果重命名失败，可能仍在编辑模式）
                            if (tab.IsInEditMode)
                            {
                                // 如果仍在编辑模式，说明重命名可能失败，不失去焦点
                                System.Diagnostics.Debug.WriteLine($"[SequenceView] 重命名可能失败，保持焦点");
                                return;
                            }
                            
                            // 将焦点移动到父控件，使 TextBox 失去焦点
                            var parent = System.Windows.Media.VisualTreeHelper.GetParent(textBox) as FrameworkElement;
                            if (parent != null)
                            {
                                parent.Focusable = true;
                                parent.Focus();
                            }
                            else
                            {
                                // 如果无法找到父控件，使用 Keyboard.ClearFocus()
                                Keyboard.ClearFocus();
                            }
                            System.Diagnostics.Debug.WriteLine($"[SequenceView] TextBox 已失去焦点，重命名完成");
                        }), System.Windows.Threading.DispatcherPriority.Loaded);
                    }
                    else if (e.Key == Key.Escape)
                    {
                        System.Diagnostics.Debug.WriteLine($"[SequenceView] EditNameTextBox_PreviewKeyDown: 手动触发 CancelEditWorkflowNameCommand");
                        
                        // 清空编辑状态
                        _editingTextBox = null;
                        _editingTab = null;
                        
                        sequenceViewModel.CancelEditWorkflowNameCommand.Execute(tab);
                        
                        // 让 TextBox 失去焦点
                        textBox.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            var parent = System.Windows.Media.VisualTreeHelper.GetParent(textBox) as FrameworkElement;
                            if (parent != null)
                            {
                                parent.Focusable = true;
                                parent.Focus();
                            }
                            else
                            {
                                Keyboard.ClearFocus();
                            }
                        }), System.Windows.Threading.DispatcherPriority.Input);
                    }
                }
            }
        }

        private static ViewModels.MultiFlowEditorViewModel FindSequenceViewModel(DependencyObject startingElement)
        {
            var current = startingElement;
            while (current != null)
            {
                if (current is FrameworkElement fe && fe.DataContext is ViewModels.MultiFlowEditorViewModel viewModel)
                {
                    return viewModel;
                }
                // 也检查 Tag 属性（向后兼容）
                if (current is FrameworkElement fe2 && fe2.Tag is ViewModels.MultiFlowEditorViewModel viewModel2)
                {
                    return viewModel2;
                }
                current = VisualTreeHelper.GetParent(current);
            }
            return null;
        }


    }
}
