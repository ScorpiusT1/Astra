 using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Astra.Services.Logging;
using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Data;

namespace Astra.ViewModels.HomeModules
{
    public partial class RealTimeLogModuleViewModel : ObservableObject, IDisposable
    {
        private readonly IUiLogService _uiLogService;
        private readonly ICollectionView _filteredLogs;
        private bool _disposed;

        public ObservableCollection<LogEntryItem> Logs { get; } = new();

        /// <summary>经级别筛选后的视图，供列表绑定。</summary>
        public ICollectionView FilteredLogs => _filteredLogs;

        /// <summary>是否显示信息、调试等较低级别日志（默认关闭，仅看报警与报错）。</summary>
        [ObservableProperty]
        private bool _showInfoLogs;

        /// <summary>是否显示警告级别。</summary>
        [ObservableProperty]
        private bool _showWarnLogs = true;

        /// <summary>是否显示错误、致命等严重级别。</summary>
        [ObservableProperty]
        private bool _showErrorLogs = true;

        /// <summary>当前筛选条件下可见条数（用于计数与空状态）。</summary>
        [ObservableProperty]
        private int _visibleLogCount;

        public RealTimeLogModuleViewModel(IUiLogService uiLogService)
        {
            _uiLogService = uiLogService;
            _filteredLogs = CollectionViewSource.GetDefaultView(Logs);
            _filteredLogs.Filter = LogPassesFilter;
            Logs.CollectionChanged += OnLogsCollectionChanged;
            _uiLogService.LogAdded += OnLogAdded;
            UpdateVisibleLogCount();
        }

        partial void OnShowInfoLogsChanged(bool value) => ApplyLogFilter();

        partial void OnShowWarnLogsChanged(bool value) => ApplyLogFilter();

        partial void OnShowErrorLogsChanged(bool value) => ApplyLogFilter();

        private void ApplyLogFilter()
        {
            _filteredLogs.Refresh();
            UpdateVisibleLogCount();
        }

        private void OnLogsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            UpdateVisibleLogCount();
        }

        private void UpdateVisibleLogCount()
        {
            if (_disposed)
                return;

            var n = 0;
            foreach (var _ in _filteredLogs)
                n++;
            VisibleLogCount = n;
        }

        private bool LogPassesFilter(object obj)
        {
            if (obj is not LogEntryItem e)
                return false;

            return e.NormalizedLevel switch
            {
                "WARN" => ShowWarnLogs,
                "ERROR" or "FATAL" or "CRITICAL" => ShowErrorLogs,
                _ => ShowInfoLogs,
            };
        }

        private void OnLogAdded(object? sender, UiLogEntryEventArgs e)
        {
            if (_disposed)
                return;

            if (Application.Current?.Dispatcher == null || Application.Current.Dispatcher.CheckAccess())
            {
                AddLog(e.Level, e.Message, e.Timestamp);
                return;
            }

            Application.Current.Dispatcher.InvokeAsync(() => AddLog(e.Level, e.Message, e.Timestamp));
        }

        private void AddLog(string level, string message, DateTime? timestamp = null)
        {
            Logs.Insert(0, new LogEntryItem
            {
                Timestamp = timestamp ?? DateTime.Now,
                Level = level,
                Message = message
            });

            while (Logs.Count > 500)
            {
                Logs.RemoveAt(Logs.Count - 1);
            }
        }

        [RelayCommand]
        private void ClearLogs()
        {
            Logs.Clear();
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            Logs.CollectionChanged -= OnLogsCollectionChanged;
            _uiLogService.LogAdded -= OnLogAdded;
            _disposed = true;
        }
    }

    public partial class LogEntryItem : ObservableObject
    {
        [ObservableProperty]
        private DateTime _timestamp;

        [ObservableProperty]
        private string _level = string.Empty;

        [ObservableProperty]
        private string _message = string.Empty;

        public string NormalizedLevel
        {
            get
            {
                if (string.IsNullOrWhiteSpace(Level))
                {
                    return "INFO";
                }

                var level = Level.Trim().ToUpperInvariant();
                return level switch
                {
                    "WARNING" => "WARN",
                    "ERR" => "ERROR",
                    _ => level
                };
            }
        }

        partial void OnLevelChanged(string value)
        {
            OnPropertyChanged(nameof(NormalizedLevel));
        }
    }
}
