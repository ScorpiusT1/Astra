using Astra.UI.Controls;
using Astra.UI.Converters;
using Astra.UI.ViewModels;
using System;
using System.Collections.Generic;
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
    /// MasterWorkflowView.xaml 的交互逻辑
    /// 主流程编辑界面 - 独立用户控件
    /// </summary>
    public partial class MasterWorkflowView : UserControl
    {
        public MasterWorkflowView()
        {
            InitializeComponent();
            Loaded += MasterWorkflowView_Loaded;
            DataContextChanged += MasterWorkflowView_DataContextChanged;
        }

        private void MasterWorkflowView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            // 当 DataContext 变更时，刷新数据绑定
            if (e.NewValue is MultiFlowEditorViewModel viewModel)
            {
                // 订阅 MasterWorkflowTab 属性变更
                viewModel.PropertyChanged += ViewModel_PropertyChanged;
            }

            if (e.OldValue is MultiFlowEditorViewModel oldViewModel)
            {
                oldViewModel.PropertyChanged -= ViewModel_PropertyChanged;
            }
        }

        private void ViewModel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MultiFlowEditorViewModel.MasterWorkflowTab) && MasterWorkflowEditor != null)
            {
                // 当 MasterWorkflowTab 属性变更时，强制刷新数据源
                var viewModel = DataContext as MultiFlowEditorViewModel;
                if (viewModel?.MasterWorkflowTab != null)
                {
                    Debug.WriteLine($"[MasterWorkflowView] MasterWorkflowTab 属性变更，节点数: {viewModel.MasterWorkflowTab.Nodes?.Count ?? 0}");

                    // 延迟刷新，确保数据已更新
                    MasterWorkflowEditor.Dispatcher.BeginInvoke(new System.Action(() =>
                    { 
                        // 强制刷新绑定
                        var binding = System.Windows.Data.BindingOperations.GetBinding(MasterWorkflowEditor, FlowEditor.CanvasItemsSourceProperty);
                        if (binding != null)
                        {
                            System.Windows.Data.BindingOperations.ClearBinding(MasterWorkflowEditor, FlowEditor.CanvasItemsSourceProperty);
                            System.Windows.Data.BindingOperations.SetBinding(MasterWorkflowEditor, FlowEditor.CanvasItemsSourceProperty, binding);
                            Debug.WriteLine("[MasterWorkflowView] MasterWorkflowTab 属性变更，已刷新 CanvasItemsSource 绑定");
                        }

                        // 直接设置数据源，确保更新
                        MasterWorkflowEditor.CanvasItemsSource = viewModel.MasterWorkflowTab.Nodes;
                        MasterWorkflowEditor.EdgeItemsSource = viewModel.MasterWorkflowTab.Edges;
                        Debug.WriteLine($"[MasterWorkflowView] 已直接设置数据源，节点数: {viewModel.MasterWorkflowTab.Nodes?.Count ?? 0}");
                    }), System.Windows.Threading.DispatcherPriority.Loaded);
                }
            }
        }

        private void MasterWorkflowView_Loaded(object sender, RoutedEventArgs e)
        {
            // 禁用主流程编辑界面的拖放功能，防止误将节点拖到主流程中
            if (MasterWorkflowEditor != null)
            {
                MasterWorkflowEditor.AllowDrop = false;
                System.Diagnostics.Debug.WriteLine("[MasterWorkflowView] 已禁用主流程编辑器的拖放功能");
            }
        }

        /// <summary>
        /// 主流程编辑器加载事件处理
        /// </summary>
        private void MasterWorkflowEditor_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is FlowEditor flowEditor && DataContext is MultiFlowEditorViewModel viewModel)
            {
                // 确保主流程编辑器禁用拖放
                flowEditor.AllowDrop = false;
                Debug.WriteLine("[MasterWorkflowView] 在主流程编辑器加载时禁用拖放功能");

                // 延迟执行，确保所有子元素都已加载
                flowEditor.Dispatcher.BeginInvoke(new System.Action(() =>
                {
                    // 再次确保禁用拖放（防止被其他代码重新启用）
                    flowEditor.AllowDrop = false;

                    // 使用 ViewModel 的 MasterWorkflowTab
                    var masterWorkflowTab = viewModel.MasterWorkflowTab;
                    if (masterWorkflowTab != null)
                    {
                        Debug.WriteLine($"[MasterWorkflowView] MasterWorkflowTab 节点数: {masterWorkflowTab.Nodes?.Count ?? 0}");

                        // 强制刷新数据源绑定
                        var binding = System.Windows.Data.BindingOperations.GetBinding(flowEditor, FlowEditor.CanvasItemsSourceProperty);
                        if (binding != null)
                        {
                            System.Windows.Data.BindingOperations.ClearBinding(flowEditor, FlowEditor.CanvasItemsSourceProperty);
                            System.Windows.Data.BindingOperations.SetBinding(flowEditor, FlowEditor.CanvasItemsSourceProperty, binding);
                            Debug.WriteLine("[MasterWorkflowView] 已刷新 CanvasItemsSource 绑定");
                        }

                        // 根据 WorkflowTab 的 Type 获取正确的模板选择器
                        var converter = this.TryFindResource("WorkflowTypeToTemplateSelectorConverter") as WorkflowTypeToTemplateSelectorConverter;
                        if (converter != null)
                        {
                            var selector = converter.Convert(masterWorkflowTab.Type, typeof(System.Windows.Controls.DataTemplateSelector), null, System.Globalization.CultureInfo.CurrentCulture) as System.Windows.Controls.DataTemplateSelector;
                            if (selector != null)
                            {
                                // 先清空，再设置，确保触发变更
                                flowEditor.ItemTemplateSelector = null;
                                flowEditor.Dispatcher.BeginInvoke(new System.Action(() =>
                                {
                                    flowEditor.ItemTemplateSelector = selector;
                                    Debug.WriteLine($"[MasterWorkflowView] 为主流程编辑器设置 ItemTemplateSelector: {selector.GetType().Name}");

                                    // 检查模板选择器的配置
                                    if (selector is NodeTypeToDataTemplateSelector nodeSelector)
                                    {
                                        Debug.WriteLine($"[MasterWorkflowView] 主流程编辑器 - WorkflowReferenceNodeTemplate: {nodeSelector.WorkflowReferenceNodeTemplate != null}");
                                        Debug.WriteLine($"[MasterWorkflowView] 主流程编辑器 - DefaultNodeTemplate: {nodeSelector.DefaultNodeTemplate != null}");
                                    }
                                }), System.Windows.Threading.DispatcherPriority.Loaded);
                            }
                        }
                    }
                    else
                    {
                        Debug.WriteLine("[MasterWorkflowView] 警告: MasterWorkflowTab 为 null");
                    }
                }), System.Windows.Threading.DispatcherPriority.Loaded);
            }
        }
    }
}
