using Astra.ViewModels.HomeModules;
using System.Windows;
using System.Windows.Controls;

namespace Astra.Views.HomeModules
{
    public partial class IOMonitorModuleView : UserControl
    {
        public IOMonitorModuleView()
        {
            InitializeComponent();
            Loaded += OnLoaded;
            DataContextChanged += OnDataContextChanged;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            RefreshVmVisibility();
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            RefreshVmVisibility();
        }

        private void RefreshVmVisibility()
        {
            if (DataContext is IOMonitorModuleViewModel vm)
            {
                vm.RefreshVisibilityAfterLoad();
            }
        }
    }
}
