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

        private async void ConfigView_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            if (DataContext is ConfigViewModel viewModel)
            {
                viewModel.ContentControlChanged -= ViewModel_ContentControlChanged;
                viewModel.ContentControlChanged += ViewModel_ContentControlChanged;
                // 若程序加载后树为空（例如插件尚未注册配置类型），在视图显示时再刷新一次
                if (viewModel.TreeNodes.Count == 0)
                {
                    await viewModel.RefreshTreeAsync();
                }
                else if (viewModel.SelectedNode != null)
                {
                    // 切换导航后再次进入配置页时，恢复右侧之前选中的配置界面
                    viewModel.RestoreSelectedConfigContent();
                }
            }
        }

        private void ViewModel_ContentControlChanged(object? sender, Control? e)
        {           
            ConfigContentRegion.Content = e;
        }

    }
}
