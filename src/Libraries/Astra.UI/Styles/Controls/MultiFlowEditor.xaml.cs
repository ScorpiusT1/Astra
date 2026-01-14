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
            DataContextChanged += OnDataContextChanged;
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
                    if (workflowTab == null)
                    {
                        // 如果 DataContext 不是 WorkflowTab，尝试从父级查找
                        var parent = VisualTreeHelper.GetParent(flowEditor);
                        while (parent != null)
                        {
                            if (parent is FrameworkElement fe && fe.DataContext is Models.WorkflowTab tab)
                            {
                                workflowTab = tab;
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
        /// 编辑名称 TextBox 获得焦点时自动全选文本
        /// </summary>
        private void EditNameTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                System.Diagnostics.Debug.WriteLine($"[SequenceView] TextBox GotFocus 事件触发: IsReadOnly={textBox.IsReadOnly}, IsEnabled={textBox.IsEnabled}, IsFocused={textBox.IsFocused}");

                // 确保 TextBox 可以输入
                textBox.IsReadOnly = false;
                textBox.IsEnabled = true;
                textBox.Focusable = true;

                // 立即全选并设置焦点
                textBox.SelectAll();
                textBox.Focus();

                // 使用 Dispatcher 延迟执行，确保文本已加载并且UI已更新
                textBox.Dispatcher.BeginInvoke(new Action(() =>
                {
                    // 再次确保可以输入
                    textBox.IsReadOnly = false;
                    textBox.IsEnabled = true;
                    textBox.Focusable = true;

                    textBox.SelectAll();
                    textBox.Focus();
                    System.Diagnostics.Debug.WriteLine($"[SequenceView] TextBox 获得焦点并全选文本: {textBox.Text}, IsFocused={textBox.IsFocused}, IsReadOnly={textBox.IsReadOnly}");

                    // 再次确认焦点
                    if (!textBox.IsFocused)
                    {
                        textBox.Focus();
                        System.Diagnostics.Debug.WriteLine($"[SequenceView] 重新设置焦点: IsFocused={textBox.IsFocused}");
                    }
                }), System.Windows.Threading.DispatcherPriority.Input);
            }
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

                    var tab = textBox.Tag as Models.WorkflowTab;
                    var sequenceViewModel = FindSequenceViewModel(textBox);
                    if (tab == null || sequenceViewModel == null)
                    {
                        return;
                    }

                    if (e.Key == Key.Enter)
                    {
                        System.Diagnostics.Debug.WriteLine($"[SequenceView] EditNameTextBox_PreviewKeyDown: 手动触发 CommitEditWorkflowNameCommand");
                        sequenceViewModel.CommitEditWorkflowNameCommand.Execute(tab);
                    }
                    else if (e.Key == Key.Escape)
                    {
                        System.Diagnostics.Debug.WriteLine($"[SequenceView] EditNameTextBox_PreviewKeyDown: 手动触发 CancelEditWorkflowNameCommand");
                        sequenceViewModel.CancelEditWorkflowNameCommand.Execute(tab);
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
