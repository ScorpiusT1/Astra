using System.Windows.Controls;
using Astra.ViewModels;

namespace Astra.Views
{
    /// <summary>
    /// DebugView.xaml 的交互逻辑
    /// </summary>
    public partial class DebugView : UserControl
    {
        public DebugView()
        {
            InitializeComponent();

            if (DataContext is DebugViewModel vm)
            {
                vm.ContentControlChanged += Vm_ContentControlChanged;
            }

            DataContextChanged += DebugView_DataContextChanged;
        }

        private void DebugView_DataContextChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is DebugViewModel oldVm)
            {
                oldVm.ContentControlChanged -= Vm_ContentControlChanged;
            }

            if (e.NewValue is DebugViewModel newVm)
            {
                newVm.ContentControlChanged += Vm_ContentControlChanged;
            }
        }

        private void Vm_ContentControlChanged(object? sender, Control? e)
        {
            DebugContentRegion.Content = e;
        }
    }
}
