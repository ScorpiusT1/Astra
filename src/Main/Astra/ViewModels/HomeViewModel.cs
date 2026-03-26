using Astra.Services.Home;
using Astra.ViewModels.HomeModules;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Astra.ViewModels
{
    public partial class HomeViewModel : ObservableObject
    {
        [ObservableProperty]
        private YieldModuleViewModel _yieldModule;

        [ObservableProperty]
        private RealTimeLogModuleViewModel _realTimeLogModule;

        [ObservableProperty]
        private TestItemTreeModuleViewModel _testItemTreeModule;

        [ObservableProperty]
        private IOMonitorModuleViewModel _ioMonitorModule;

        public HomeViewModel(ITestItemTreeDataProvider testItemTreeDataProvider)
        {
            YieldModule = new YieldModuleViewModel();
            RealTimeLogModule = new RealTimeLogModuleViewModel();
            TestItemTreeModule = new TestItemTreeModuleViewModel(testItemTreeDataProvider);
            IoMonitorModule = new IOMonitorModuleViewModel();
        }
    }
}
