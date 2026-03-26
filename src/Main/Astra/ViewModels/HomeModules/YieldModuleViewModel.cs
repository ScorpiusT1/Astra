using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Astra.Services.Home;

namespace Astra.ViewModels.HomeModules
{
    public partial class YieldModuleViewModel : ObservableObject
    {
        private readonly IYieldDailyStatsService _yieldDailyStatsService;

        [ObservableProperty]
        private int _passCount;

        [ObservableProperty]
        private int _failCount;

        [ObservableProperty]
        private int _totalCount;

        public YieldModuleViewModel(IYieldDailyStatsService yieldDailyStatsService)
        {
            _yieldDailyStatsService = yieldDailyStatsService;
            ReloadFromStorage();
        }

        public double YieldPercent => TotalCount <= 0 ? 0d : (double)PassCount / TotalCount * 100d;

        partial void OnPassCountChanged(int value) => OnPropertyChanged(nameof(YieldPercent));
        partial void OnFailCountChanged(int value) => OnPropertyChanged(nameof(YieldPercent));
        partial void OnTotalCountChanged(int value) => OnPropertyChanged(nameof(YieldPercent));

        [RelayCommand]
        private void ClearYield()
        {
            _yieldDailyStatsService.ClearToday();
            ReloadFromStorage();
        }

        public void ReloadFromStorage()
        {
            var stats = _yieldDailyStatsService.GetToday();
            PassCount = stats.PassCount;
            FailCount = stats.FailCount;
            TotalCount = stats.PassCount + stats.FailCount;
        }
    }
}
