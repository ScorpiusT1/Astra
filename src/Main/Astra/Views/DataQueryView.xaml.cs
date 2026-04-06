using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Astra.Reporting;
using Astra.ViewModels;

namespace Astra.Views
{
    public partial class DataQueryView : UserControl
    {
        public DataQueryView()
        {
            InitializeComponent();
        }

        private async void DataQueryView_OnLoaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is DataQueryViewModel vm)
                await vm.PrepareArchiveRootAsync().ConfigureAwait(true);
        }

        private void ResultGrid_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is DataQueryViewModel vm)
                vm.OpenSelectedFileCommand.Execute(null);
        }

        /// <summary>右键按下时选中所在行，便于上下文菜单针对当前行。</summary>
        private void ResultGrid_OnPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not DataGrid dg)
                return;

            DependencyObject? dep = e.OriginalSource as DependencyObject;
            while (dep != null && dep is not DataGridRow)
                dep = VisualTreeHelper.GetParent(dep);

            if (dep is DataGridRow row && row.Item is ArchivedDataFileRow item)
            {
                if (!row.IsSelected)
                {
                    row.IsSelected = true;
                    dg.SelectedItem = item;
                }

                if (dg.DataContext is DataQueryViewModel vm)
                    vm.SelectedRow = item;
            }
        }
    }
}
