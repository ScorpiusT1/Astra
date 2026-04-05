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
                        // 不要用 ClearBinding 刷新 CanvasItemsSource：会短暂置 null，InfiniteCanvas.ItemsSource 为空时会清空整条连线层。
                        var tab = viewModel.MasterWorkflowTab;
                        tab.Nodes ??= new System.Collections.ObjectModel.ObservableCollection<Astra.Core.Nodes.Models.Node>();
                        tab.Edges ??= new System.Collections.ObjectModel.ObservableCollection<Astra.Core.Nodes.Models.Edge>();
                        MasterWorkflowEditor.CanvasItemsSource = tab.Nodes;
                        MasterWorkflowEditor.EdgeItemsSource = tab.Edges;
                        MasterWorkflowEditor.RefreshEdgesImmediate();
                        Debug.WriteLine($"[MasterWorkflowView] MasterWorkflowTab 变更后已同步画布与连线，节点数: {tab.Nodes.Count}, 边数: {tab.Edges.Count}");
                    }), System.Windows.Threading.DispatcherPriority.Loaded);
                }
            }
        }

        /// <summary>
        /// 主流程编辑器加载事件处理
        /// </summary>
        private void MasterWorkflowEditor_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is FlowEditor flowEditor && DataContext is MultiFlowEditorViewModel viewModel)
            {
                // 延迟执行，确保所有子元素都已加载
                flowEditor.Dispatcher.BeginInvoke(new System.Action(() =>
                {
                    // 使用 ViewModel 的 MasterWorkflowTab
                    var masterWorkflowTab = viewModel.MasterWorkflowTab;
                    if (masterWorkflowTab != null)
                    {
                        Debug.WriteLine($"[MasterWorkflowView] MasterWorkflowTab 节点数: {masterWorkflowTab.Nodes?.Count ?? 0}");

                        // 不要用 ClearBinding 刷新 CanvasItemsSource（同上，会导致 ItemsSource 空窗期并清空连线）。
                        masterWorkflowTab.Nodes ??= new System.Collections.ObjectModel.ObservableCollection<Astra.Core.Nodes.Models.Node>();
                        masterWorkflowTab.Edges ??= new System.Collections.ObjectModel.ObservableCollection<Astra.Core.Nodes.Models.Edge>();
                        flowEditor.CanvasItemsSource = masterWorkflowTab.Nodes;
                        flowEditor.EdgeItemsSource = masterWorkflowTab.Edges;

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

                                    // 节点容器与端口布局就绪后再画线，避免仅出现一帧后被后续刷新清空
                                    flowEditor.RefreshEdgesImmediate();
                                }), System.Windows.Threading.DispatcherPriority.Loaded);
                            }
                        }

                        // 模板选择器缺失时也要在布局后补一次连线重绘；与上面 inner 回调中的刷新叠加也无妨
                        flowEditor.Dispatcher.BeginInvoke(new System.Action(() => flowEditor.RefreshEdgesImmediate()),
                            System.Windows.Threading.DispatcherPriority.Loaded);
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
