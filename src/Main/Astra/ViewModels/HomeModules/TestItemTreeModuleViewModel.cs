using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Astra.Services.Home;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace Astra.ViewModels.HomeModules
{
    public partial class TestItemTreeModuleViewModel : ObservableObject
    {
        private readonly ITestItemTreeDataProvider _provider;

        public ObservableCollection<TestTreeNodeItem> Roots { get; } = new();

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private string? _loadError;

        public TestItemTreeModuleViewModel(ITestItemTreeDataProvider provider)
        {
            _provider = provider;
            _ = LoadTreeAsync();
        }

        [RelayCommand]
        private async Task Refresh()
        {
            await LoadTreeAsync();
        }

        private async Task LoadTreeAsync()
        {
            IsLoading = true;
            LoadError = null;
            try
            {
                var roots = await _provider.LoadRootNodesAsync();
                Roots.Clear();
                foreach (var r in roots)
                    Roots.Add(r);
            }
            catch (Exception ex)
            {
                LoadError = ex.Message;
            }
            finally
            {
                IsLoading = false;
            }
        }
    }

    public partial class TestTreeNodeItem : ObservableObject
    {
        [ObservableProperty]
        private string _name = string.Empty;

        [ObservableProperty]
        private string _status = string.Empty;

        [ObservableProperty]
        private DateTime _testTime = DateTime.Now;

        [ObservableProperty]
        private double _actualValue;

        [ObservableProperty]
        private double _lowerLimit;

        [ObservableProperty]
        private double _upperLimit;

        [ObservableProperty]
        private bool _isRoot;

        public ObservableCollection<TestTreeNodeItem> Children { get; } = new();
    }
}
