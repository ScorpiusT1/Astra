using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Astra.Services.Logging;
using System;
using System.Collections.ObjectModel;
using System.Windows;

namespace Astra.ViewModels.HomeModules
{
    public partial class RealTimeLogModuleViewModel : ObservableObject, IDisposable
    {
        private readonly IUiLogService _uiLogService;
        private bool _disposed;

        public ObservableCollection<LogEntryItem> Logs { get; } = new();

        public RealTimeLogModuleViewModel(IUiLogService uiLogService)
        {
            _uiLogService = uiLogService;
            _uiLogService.LogAdded += OnLogAdded;
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
