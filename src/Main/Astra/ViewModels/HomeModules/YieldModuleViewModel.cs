using CommunityToolkit.Mvvm.ComponentModel;

namespace Astra.ViewModels.HomeModules
{
    public partial class YieldModuleViewModel : ObservableObject
    {
        [ObservableProperty]
        private int _passCount = 980;

        [ObservableProperty]
        private int _failCount = 20;

        [ObservableProperty]
        private int _totalCount = 1000;

        public double YieldPercent => TotalCount <= 0 ? 0d : (double)PassCount / TotalCount * 100d;

        partial void OnPassCountChanged(int value) => OnPropertyChanged(nameof(YieldPercent));
        partial void OnFailCountChanged(int value) => OnPropertyChanged(nameof(YieldPercent));
        partial void OnTotalCountChanged(int value) => OnPropertyChanged(nameof(YieldPercent));
    }
}
