using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.ObjectModel;
using System.Windows.Threading;

namespace Astra.ViewModels.HomeModules
{
    public partial class RealTimeLogModuleViewModel : ObservableObject
    {
        private readonly DispatcherTimer _timer;
        private int _sequence;

        public ObservableCollection<LogEntryItem> Logs { get; } = new();

        public RealTimeLogModuleViewModel()
        {
            InitializeSeedLogs();

            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2)
            };
            _timer.Tick += OnTick;
            _timer.Start();
        }

        private void InitializeSeedLogs()
        {
            AddLog("INFO", "系统启动完成，等待测试任务...");
            AddLog("INFO", "工作流引擎就绪");
            AddLog("INFO", "设备通信链路正常");
        }

        private void OnTick(object? sender, EventArgs e)
        {
            _sequence++;
            var level = _sequence % 5 == 0 ? "WARN" : "INFO";
            AddLog(level, $"实时记录：第 {_sequence} 次状态轮询完成");
        }

        private void AddLog(string level, string message)
        {
            Logs.Insert(0, new LogEntryItem
            {
                Timestamp = DateTime.Now,
                Level = level,
                Message = message
            });

            while (Logs.Count > 200)
            {
                Logs.RemoveAt(Logs.Count - 1);
            }
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
