using System.Threading.Tasks;
using System.Windows.Controls;
using Astra.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace Astra.Views
{
    /// <summary>
    /// ConfigView.xaml 的交互逻辑
    /// </summary>
    public partial class ConfigView : UserControl
    {
        public ConfigView()
        {
            InitializeComponent();

            Loaded -= ConfigView_Loaded;
            Loaded += ConfigView_Loaded;
        }

        private void ConfigView_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            if(DataContext is ConfigViewModel viewModel)
            {
                viewModel.ContentControlChanged -= ViewModel_ContentControlChanged;
                viewModel.ContentControlChanged += ViewModel_ContentControlChanged;
            }
        }

        private void ViewModel_ContentControlChanged(object? sender, Control? e)
        {           
            ConfigContentRegion.Content = e;
        }

    }
}
