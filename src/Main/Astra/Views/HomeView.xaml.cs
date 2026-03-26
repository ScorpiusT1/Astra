using Astra.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace Astra.Views
{
    public partial class HomeView : UserControl
    {
        public HomeView()
        {
            InitializeComponent();
            Unloaded += OnUnloaded;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            // 仅在应用退出阶段释放，避免导航切页时误销毁。
            if (Application.Current?.Dispatcher?.HasShutdownStarted != true)
                return;

            if (DataContext is HomeViewModel vm)
            {
                vm.Dispose();
            }
        }
    }
}
