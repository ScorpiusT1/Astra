using Astra.UI.Controls;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;

namespace Astra.Views
{
    /// <summary>
    /// SequenceView.xaml 的交互逻辑
    /// </summary>
    public partial class SequenceView : UserControl
    {
        public SequenceView()
        {
            InitializeComponent();
            Loaded += OnLoaded;
            DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            Debug.WriteLine($"[SequenceView] DataContext 变更: {e.NewValue != null}");
            if (e.NewValue is ViewModels.SequenceViewModel viewModel)
            {
                Debug.WriteLine($"[SequenceView] ToolBoxItemsSource: {viewModel.ToolBoxItemsSource?.Count ?? 0} 个类别");
                Debug.WriteLine($"[SequenceView] CanvasItemsSource: {viewModel.CanvasItemsSource?.Count ?? 0} 个节点");
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("[SequenceView] Loaded 事件触发");
            
            // 在代码后台设置 NodeFactory，因为 Func 类型无法在 XAML 中直接绑定
            if (DataContext is ViewModels.SequenceViewModel viewModel && FlowEditor != null)
            {
                FlowEditor.NodeFactory = viewModel.NodeFactory;
                Debug.WriteLine("[SequenceView] NodeFactory 已设置");
                
                // 验证数据绑定
                Debug.WriteLine($"[SequenceView] FlowEditor.ToolBoxItemsSource: {FlowEditor.ToolBoxItemsSource != null}, Count: {(FlowEditor.ToolBoxItemsSource as System.Collections.ICollection)?.Count ?? 0}");
                Debug.WriteLine($"[SequenceView] FlowEditor.CanvasItemsSource: {FlowEditor.CanvasItemsSource != null}, Count: {(FlowEditor.CanvasItemsSource as System.Collections.ICollection)?.Count ?? 0}");
            }
        }
    }
}
