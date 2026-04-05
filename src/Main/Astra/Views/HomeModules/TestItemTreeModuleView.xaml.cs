using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Astra.UI.Styles.Controls.TreeViewEx;
using Astra.ViewModels.HomeModules;

namespace Astra.Views.HomeModules
{
    public partial class TestItemTreeModuleView : UserControl
    {
        public TestItemTreeModuleView()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is INotifyPropertyChanged oldNpc)
                oldNpc.PropertyChanged -= OnViewModelPropertyChanged;

            if (e.NewValue is TestItemTreeModuleViewModel vm)
            {
                vm.PropertyChanged += OnViewModelPropertyChanged;
                ApplyTestItemNameColumnWidth(vm);
            }
        }

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(TestItemTreeModuleViewModel.TestItemNameColumnWidth))
                return;
            if (sender is TestItemTreeModuleViewModel vm)
                Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() => ApplyTestItemNameColumnWidth(vm)));
        }

        private void ApplyTestItemNameColumnWidth(TestItemTreeModuleViewModel vm)
        {
            if (TestItemsTree?.Columns is not { Count: > 0 } cols)
                return;
            var w = vm.TestItemNameColumnWidth;
            if (w > 0 && Math.Abs(cols[0].Width - w) > 0.5)
                cols[0].Width = w;
            TestItemsTree.InvalidateColumnFillLayout();
        }
    }
}
