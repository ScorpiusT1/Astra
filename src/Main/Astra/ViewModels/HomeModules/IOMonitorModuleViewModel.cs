using Astra.UI.Abstractions.Home;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Threading;

namespace Astra.ViewModels.HomeModules
{
    public partial class IOMonitorModuleViewModel : ObservableObject, IDisposable
    {
        private bool _disposed;

        public ObservableCollection<IoMonitorPointItem> Points { get; } = new();

        /// <summary>是否有首页监控点位（用于在无 IO 时隐藏整模块）。与 Points 同步，避免仅依赖计算属性时早于界面绑定丢失通知。</summary>
        [ObservableProperty]
        private bool _hasMonitorPoints;

        /// <summary>监控点位数量（供标题绑定；ObservableCollection 变更时由 CollectionChanged 通知）。</summary>
        public int MonitorPointCount => Points.Count;

        public IOMonitorModuleViewModel()
        {
            Points.CollectionChanged += OnPointsCollectionChanged;
            IoMonitorRuntimeRegistry.RuntimeRegistered += OnIoMonitorRuntimeRegistered;

            if (IoMonitorRuntimeRegistry.TryGet() != null)
            {
                TryAttach();
            }
            else
            {
                Application.Current?.Dispatcher.BeginInvoke(TryAttach, DispatcherPriority.ApplicationIdle);
            }

            SyncHasMonitorPoints();
        }

        private void OnIoMonitorRuntimeRegistered(object? sender, EventArgs e)
        {
            Application.Current?.Dispatcher.BeginInvoke(TryAttach, DispatcherPriority.Normal);
        }

        private void OnPointsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            SyncHasMonitorPoints();
            OnPropertyChanged(nameof(MonitorPointCount));
        }

        private void TryAttach()
        {
            if (_disposed)
            {
                return;
            }

            IoMonitorRuntimeRegistry.TryGet()?.Attach(Points);
            SyncHasMonitorPoints();
        }

        private void SyncHasMonitorPoints()
        {
            HasMonitorPoints = Points.Count > 0;
        }

        /// <summary>视图 Loaded 或 DataContext 就绪后调用，与早于界面创建的 VM、Idle 上完成的 Attach 对齐可见性绑定。</summary>
        public void RefreshVisibilityAfterLoad()
        {
            SyncHasMonitorPoints();
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            IoMonitorRuntimeRegistry.RuntimeRegistered -= OnIoMonitorRuntimeRegistered;
            Points.CollectionChanged -= OnPointsCollectionChanged;
            IoMonitorRuntimeRegistry.TryGet()?.Detach();
        }
    }
}
