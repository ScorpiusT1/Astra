using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Astra.Services.Home;
using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
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

        [ObservableProperty]
        private string _summaryResult = "READY";

        [ObservableProperty]
        private string _summaryTime = "--:--:--";

        [ObservableProperty]
        private int _totalPassCount;

        [ObservableProperty]
        private int _totalFailCount;

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
                var rootIndex = 0;
                foreach (var r in roots)
                {
                    ApplyGroupColorIndex(r, rootIndex % 4);
                    Roots.Add(r);
                    rootIndex++;
                }

                UpdateSummary(roots);
            }
            catch (Exception ex)
            {
                LoadError = ex.Message;
                SummaryResult = "ERROR";
                SummaryTime = "--:--:--";
                TotalPassCount = 0;
                TotalFailCount = 0;
            }
            finally
            {
                IsLoading = false;
            }
        }

        private static void ApplyGroupColorIndex(TestTreeNodeItem node, int groupColorIndex)
        {
            node.GroupColorIndex = groupColorIndex;
            foreach (var child in node.Children)
            {
                ApplyGroupColorIndex(child, groupColorIndex);
            }
        }

        private void UpdateSummary(IReadOnlyList<TestTreeNodeItem> roots)
        {
            var allLeaves = new List<TestTreeNodeItem>();
            foreach (var root in roots)
            {
                CollectLeaves(root, allLeaves);
            }

            TotalPassCount = allLeaves.Count(x => string.Equals(x.Status, "Pass", StringComparison.OrdinalIgnoreCase));
            TotalFailCount = allLeaves.Count(x => string.Equals(x.Status, "Fail", StringComparison.OrdinalIgnoreCase));

            var latest = allLeaves.OrderByDescending(x => x.TestTime).FirstOrDefault();
            SummaryTime = latest?.TestTime.ToString("HH:mm:ss") ?? "--:--:--";

            if (TotalFailCount > 0)
                SummaryResult = "NG";
            else if (TotalPassCount > 0)
                SummaryResult = "OK";
            else
                SummaryResult = "READY";
        }

        private static void CollectLeaves(TestTreeNodeItem node, List<TestTreeNodeItem> result)
        {
            if (node.Children.Count == 0)
            {
                if (!node.IsRoot)
                    result.Add(node);
                return;
            }

            foreach (var child in node.Children)
            {
                CollectLeaves(child, result);
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

        [ObservableProperty]
        private int _groupColorIndex;

        public ObservableCollection<TestTreeNodeItem> Children { get; } = new();
    }
}
