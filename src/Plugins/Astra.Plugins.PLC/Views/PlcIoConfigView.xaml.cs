using Astra.Plugins.PLC.Configs;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Astra.Plugins.PLC.Views
{
    public partial class PlcIoConfigView : UserControl
    {
        public PlcIoConfigView()
        {
            InitializeComponent();
        }

        private void DataGrid_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not DataGrid dataGrid)
            {
                return;
            }

            var dep = e.OriginalSource as DependencyObject;
            while (dep != null && dep is not DataGridRow)
            {
                dep = VisualTreeHelper.GetParent(dep);
            }

            if (dep is DataGridRow row)
            {
                row.IsSelected = true;
                dataGrid.SelectedItem = row.Item;
                dataGrid.Focus();
            }
        }
    }
}

