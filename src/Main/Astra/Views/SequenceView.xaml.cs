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
            
        }
    }
}
