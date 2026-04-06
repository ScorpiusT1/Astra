using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Astra.Core.Configuration.Abstractions;
using Astra.Reporting;
using Astra.Services.UI;
using Astra.Services.WorkflowArchive;
using Astra.UI.Abstractions.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace Astra.ViewModels
{
    /// <summary>
    /// 测试归档数据查询：按 SN、日期、类型检索「测试数据」目录下的原始/音频/算法/报告/运行日志文件。
    /// </summary>
    public partial class DataQueryViewModel : ObservableObject
    {
        private readonly IConfigurationManager _configurationManager;
        private readonly WorkflowArchiveOptions _archiveOptions;
        private readonly ArchivedTestDataQueryService _queryService;
        private readonly IBusyService _busyService;
        private readonly ILogger<DataQueryViewModel>? _logger;

        private readonly List<ArchivedDataFileRow> _allRows = new();

        private string _lastQueryArchiveRoot = string.Empty;

        [ObservableProperty]
        private string _archiveRootDisplay = string.Empty;

        [ObservableProperty]
        private string _snFilter = string.Empty;

        [ObservableProperty]
        private DateTime? _fromDate;

        [ObservableProperty]
        private DateTime? _toDate;

        /// <summary>0=全部，1=原始，2=音频，3=算法，4=报告，5=运行日志</summary>
        [ObservableProperty]
        private int _dataTypeFilterIndex;

        [ObservableProperty]
        private ObservableCollection<ArchivedDataFileRow> _results = new();

        [ObservableProperty]
        private ArchivedDataFileRow? _selectedRow;

        /// <summary>与主窗口 <see cref="IBusyService"/> 同步，用于禁用查询按钮并与全局忙碌遮罩一致。</summary>
        public bool IsBusy => _busyService.IsBusy;

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        [ObservableProperty]
        private string? _lastError;

        [ObservableProperty]
        private int _totalItemCount;

        [ObservableProperty]
        private int _totalPages = 1;

        [ObservableProperty]
        private int _pageSize = 50;

        private int _currentPage = 1;

        /// <summary>当前页码（从 1 开始）。由分页命令与查询显式更新，并配合 <see cref="ApplyPageSlice"/>。</summary>
        public int CurrentPage
        {
            get => _currentPage;
            private set
            {
                SetProperty(ref _currentPage, value);
            }
        }

        /// <summary>每页条数下拉选项。</summary>
        public ObservableCollection<int> PageSizeOptions { get; } = new(new[] { 25, 50, 100, 200 });

        public DataQueryViewModel(
            IConfigurationManager configurationManager,
            WorkflowArchiveOptions archiveOptions,
            ArchivedTestDataQueryService queryService,
            IBusyService busyService,
            ILogger<DataQueryViewModel>? logger = null)
        {
            _configurationManager = configurationManager ?? throw new ArgumentNullException(nameof(configurationManager));
            _archiveOptions = archiveOptions ?? throw new ArgumentNullException(nameof(archiveOptions));
            _queryService = queryService ?? throw new ArgumentNullException(nameof(queryService));
            _busyService = busyService ?? throw new ArgumentNullException(nameof(busyService));
            _logger = logger;
            _busyService.PropertyChanged += OnBusyServicePropertyChanged;
        }

        private void OnBusyServicePropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is null or nameof(IBusyService.IsBusy))
            {
                OnPropertyChanged(nameof(IsBusy));
                SearchCommand.NotifyCanExecuteChanged();
            }
        }

        partial void OnSelectedRowChanged(ArchivedDataFileRow? value)
        {
            OpenRowContainingFolderCommand.NotifyCanExecuteChanged();
        }

        partial void OnPageSizeChanged(int value)
        {
            if (value < 1) return;
            CurrentPage = 1;
            ApplyPageSlice();
        }

        /// <summary>供视图 Loaded 调用，异步解析归档根目录（不在 UI 线程阻塞）。</summary>
        public Task PrepareArchiveRootAsync() => RefreshArchiveRootCoreAsync();

        private async Task RefreshArchiveRootCoreAsync()
        {
            try
            {
                ArchiveRootDisplay = await ReportArchivePath.ResolveRootAsync(
                    _configurationManager,
                    _archiveOptions,
                    _logger).ConfigureAwait(true);
                LastError = null;
            }
            catch (Exception ex)
            {
                ArchiveRootDisplay = string.Empty;
                LastError = ex.Message;
                _logger?.LogWarning(ex, "解析归档根目录失败");
            }
        }

        [RelayCommand]
        private Task RefreshArchiveRootDisplay() => RefreshArchiveRootCoreAsync();

        [RelayCommand(CanExecute = nameof(CanSearch))]
        private async Task SearchAsync()
        {
            if (_busyService.IsBusy) return;

            await RefreshArchiveRootCoreAsync().ConfigureAwait(true);
            if (string.IsNullOrWhiteSpace(ArchiveRootDisplay))
            {
                StatusMessage = "无法确定归档根目录。";
                return;
            }

            LastError = null;

            var root = ArchiveRootDisplay;
            var sn = SnFilter?.Trim();
            var from = FromDate?.Date;
            var to = ToDate?.Date;
            var typeIndex = DataTypeFilterIndex;

            await BusyUiHelper.RunWithNavigationBusyAsync(
                _busyService,
                "正在扫描测试数据…",
                async () =>
                {
                    try
                    {
                        StatusMessage = "正在扫描…";
                        var criteria = new ArchivedTestDataQueryCriteria
                        {
                            SnContains = string.IsNullOrEmpty(sn) ? null : sn,
                            FromLocalDateInclusive = from,
                            ToLocalDateInclusive = to,
                            Categories = MapDataTypeIndex(typeIndex)
                        };

                        var result = await Task.Run(() => _queryService.Query(root, criteria)).ConfigureAwait(true);

                        _allRows.Clear();
                        Results.Clear();
                        if (!string.IsNullOrEmpty(result.ErrorMessage))
                        {
                            LastError = result.ErrorMessage;
                            StatusMessage = result.ErrorMessage;
                            TotalItemCount = 0;
                            TotalPages = 1;
                            CurrentPage = 1;
                            _lastQueryArchiveRoot = string.Empty;
                        }
                        else
                        {
                            _allRows.AddRange(result.Items);
                            _lastQueryArchiveRoot = result.ArchiveRoot;
                            CurrentPage = 1;
                            ApplyPageSlice();
                        }
                    }
                    catch (Exception ex)
                    {
                        LastError = ex.Message;
                        StatusMessage = "查询失败: " + ex.Message;
                        _logger?.LogWarning(ex, "归档数据查询失败");
                    }
                });
        }

        private bool CanSearch() => !_busyService.IsBusy;

        private void ApplyPageSlice()
        {
            TotalItemCount = _allRows.Count;
            if (TotalItemCount == 0)
            {
                TotalPages = 1;
                if (CurrentPage != 1)
                    CurrentPage = 1;
                Results.Clear();
                RefreshPagingCommands();
                UpdatePagingStatusMessage();
                return;
            }

            TotalPages = Math.Max(1, (TotalItemCount + PageSize - 1) / PageSize);
            var clamped = Math.Clamp(CurrentPage, 1, TotalPages);
            if (clamped != CurrentPage)
                CurrentPage = clamped;

            Results.Clear();
            var skip = (CurrentPage - 1) * PageSize;
            var indexOnPage = 0;
            foreach (var row in _allRows.Skip(skip).Take(PageSize))
            {
                row.TableSequence = ++indexOnPage;
                Results.Add(row);
            }

            RefreshPagingCommands();
            UpdatePagingStatusMessage();
        }

        private void UpdatePagingStatusMessage()
        {
            if (!string.IsNullOrEmpty(LastError) && TotalItemCount == 0)
                return;

            if (TotalItemCount == 0)
            {
                StatusMessage = string.IsNullOrEmpty(_lastQueryArchiveRoot)
                    ? "无匹配文件。"
                    : $"无匹配文件。根: {_lastQueryArchiveRoot}";
                return;
            }

            StatusMessage =
                $"共 {TotalItemCount} 条，第 {CurrentPage} / {TotalPages} 页（每页 {PageSize} 条）";
            if (!string.IsNullOrEmpty(_lastQueryArchiveRoot))
                StatusMessage += $"  |  根: {_lastQueryArchiveRoot}";
        }

        private void RefreshPagingCommands()
        {
            GoToPreviousPageCommand.NotifyCanExecuteChanged();
            GoToNextPageCommand.NotifyCanExecuteChanged();
            OnPropertyChanged(nameof(CurrentPage));
            OnPropertyChanged(nameof(PagingLabel));
        }

        /// <summary>底栏「第 x / y 页」文案。</summary>
        public string PagingLabel => $"第 {CurrentPage} / {TotalPages} 页";

        [RelayCommand(CanExecute = nameof(CanGoToPreviousPage))]
        private void GoToPreviousPage()
        {
            if (CurrentPage <= 1) return;
            CurrentPage--;
            ApplyPageSlice();
        }

        private bool CanGoToPreviousPage() => CurrentPage > 1 && TotalItemCount > 0;

        [RelayCommand(CanExecute = nameof(CanGoToNextPage))]
        private void GoToNextPage()
        {
            if (CurrentPage >= TotalPages) return;
            CurrentPage++;
            ApplyPageSlice();
        }

        private bool CanGoToNextPage() => CurrentPage < TotalPages && TotalPages > 1;

        private static ArchivedDataCategoryFlags MapDataTypeIndex(int index) =>
            index switch
            {
                1 => ArchivedDataCategoryFlags.Raw,
                2 => ArchivedDataCategoryFlags.Audio,
                3 => ArchivedDataCategoryFlags.Algorithm,
                4 => ArchivedDataCategoryFlags.Report,
                5 => ArchivedDataCategoryFlags.RunLog,
                _ => ArchivedDataCategoryFlags.All
            };

        [RelayCommand]
        private void OpenSelectedFile()
        {
            var path = SelectedRow?.FullPath;
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                MessageBox.Show("请先选择存在的文件。", "数据查询", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show("无法打开文件: " + ex.Message, "数据查询", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        /// <summary>打开当前选中文件所在文件夹（右键「打开所在文件夹」）。</summary>
        [RelayCommand(CanExecute = nameof(CanOpenRowContainingFolder))]
        private void OpenRowContainingFolder()
        {
            var path = SelectedRow?.FullPath;
            if (string.IsNullOrWhiteSpace(path))
            {
                MessageBox.Show("请先右键选中一行。", "数据查询", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dir = Path.GetDirectoryName(path);
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
            {
                MessageBox.Show("目录不存在。", "数据查询", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = dir,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show("无法打开文件夹: " + ex.Message, "数据查询", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private bool CanOpenRowContainingFolder()
        {
            if (SelectedRow == null || string.IsNullOrWhiteSpace(SelectedRow.FullPath))
                return false;
            var dir = Path.GetDirectoryName(SelectedRow.FullPath);
            return !string.IsNullOrEmpty(dir) && Directory.Exists(dir);
        }
    }
}
