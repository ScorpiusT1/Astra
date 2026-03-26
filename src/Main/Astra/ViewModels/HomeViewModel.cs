using Astra.Services.Home;
using Astra.Services.Logging;
using Astra.Core.Configuration.Abstractions;
using Astra.Configuration;
using Astra.Core.Configuration;
using Astra.UI.Services;
using Astra.ViewModels.HomeModules;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace Astra.ViewModels
{
    public partial class HomeViewModel : ObservableObject, IDisposable
    {
        private bool _disposed;
        private bool _hasPendingYieldRecord;
        private bool _isCurrentRunCanceled;
        private bool _wasTestRunning;
        private readonly IYieldDailyStatsService _yieldDailyStatsService;
        private readonly IConfigurationManager _configurationManager;
        private readonly IWorkflowExecutionSessionService _workflowExecutionSessionService;
        private readonly IHomeWorkflowExecutionService _homeWorkflowExecutionService;
        private readonly Action<SoftwareConfig, ConfigChangeType> _softwareConfigChangedHandler;
        private readonly SequenceViewModel _sequenceViewModel;
        private readonly IUiLogService _uiLogService;
        private CancellationTokenSource? _homeRunCts;
        private Task? _homeRunTask;

        [ObservableProperty]
        private bool _isSequenceLinkageEnabled = true;

        [ObservableProperty]
        private bool _isTestRunning;

        [ObservableProperty]
        private bool _isTestPaused;

        [ObservableProperty]
        private YieldModuleViewModel _yieldModule;

        [ObservableProperty]
        private RealTimeLogModuleViewModel _realTimeLogModule;

        [ObservableProperty]
        private TestItemTreeModuleViewModel _testItemTreeModule;

        [ObservableProperty]
        private IOMonitorModuleViewModel _ioMonitorModule;

        public HomeViewModel(
            ITestItemTreeDataProvider testItemTreeDataProvider,
            IYieldDailyStatsService yieldDailyStatsService,
            IConfigurationManager configurationManager,
            IWorkflowExecutionSessionService workflowExecutionSessionService,
            IHomeWorkflowExecutionService homeWorkflowExecutionService,
            IUiLogService uiLogService,
            SequenceViewModel sequenceViewModel)
        {
            _yieldDailyStatsService = yieldDailyStatsService;
            _configurationManager = configurationManager;
            _workflowExecutionSessionService = workflowExecutionSessionService;
            _homeWorkflowExecutionService = homeWorkflowExecutionService;
            _sequenceViewModel = sequenceViewModel;
            _uiLogService = uiLogService;
            YieldModule = new YieldModuleViewModel(_yieldDailyStatsService);
            RealTimeLogModule = new RealTimeLogModuleViewModel(uiLogService);
            TestItemTreeModule = new TestItemTreeModuleViewModel(testItemTreeDataProvider, configurationManager, workflowExecutionSessionService);
            IoMonitorModule = new IOMonitorModuleViewModel();

            _softwareConfigChangedHandler = OnSoftwareConfigChanged;
            _configurationManager.Subscribe(_softwareConfigChangedHandler);
            _ = LoadLinkageConfigAsync();
            if (_sequenceViewModel.MultiFlowEditor != null)
            {
                _sequenceViewModel.MultiFlowEditor.PropertyChanged += OnSequenceEditorPropertyChanged;
                _sequenceViewModel.MultiFlowEditor.SequenceFileSaved += OnSequenceFileSaved;
            }

        }

        partial void OnIsSequenceLinkageEnabledChanged(bool value)
        {
            StartTestCommand.NotifyCanExecuteChanged();
            PauseTestCommand.NotifyCanExecuteChanged();
            ResumeTestCommand.NotifyCanExecuteChanged();
            CancelTestCommand.NotifyCanExecuteChanged();
        }

        partial void OnIsTestRunningChanged(bool value)
        {
            if (!value && _wasTestRunning)
            {
                FinalizeYieldForCurrentRunIfNeeded();
            }
            _wasTestRunning = value;
            StartTestCommand.NotifyCanExecuteChanged();
            PauseTestCommand.NotifyCanExecuteChanged();
            ResumeTestCommand.NotifyCanExecuteChanged();
            CancelTestCommand.NotifyCanExecuteChanged();
        }

        partial void OnIsTestPausedChanged(bool value)
        {
            PauseTestCommand.NotifyCanExecuteChanged();
            ResumeTestCommand.NotifyCanExecuteChanged();
        }

        private void FinalizeYieldForCurrentRunIfNeeded()
        {
            if (!_hasPendingYieldRecord || _isCurrentRunCanceled)
                return;

            var summary = TestItemTreeModule.SummaryResult;
            if (string.Equals(summary, "OK", StringComparison.OrdinalIgnoreCase))
            {
                _yieldDailyStatsService.AddToday(1, 0);
            }
            else if (string.Equals(summary, "NG", StringComparison.OrdinalIgnoreCase))
            {
                _yieldDailyStatsService.AddToday(0, 1);
            }

            _hasPendingYieldRecord = false;
            YieldModule.ReloadFromStorage();
        }

        [RelayCommand(CanExecute = nameof(CanStartTest))]
        private Task StartTest()
        {
            RealTimeLogModule.ClearLogsCommand.Execute(null);
            TestItemTreeModule.ResetForNewRun();
            _hasPendingYieldRecord = true;
            _isCurrentRunCanceled = false;

            if (IsSequenceLinkageEnabled)
            {
                TestItemTreeModule.EndStandaloneExecutionEventSession();
                _sequenceViewModel.MultiFlowEditor?.PlayCommand.Execute(null);
                SyncRunningStateFromSequence();
                return Task.CompletedTask;
            }

            TestItemTreeModule.BeginStandaloneExecutionEventSession();
            IsTestRunning = true;
            IsTestPaused = false;
            _homeRunCts?.Cancel();
            _homeRunCts?.Dispose();
            _homeRunCts = new CancellationTokenSource();
            _homeRunTask = RunStandaloneFromHomeAsync(_homeRunCts.Token);
            return Task.CompletedTask;
        }

        private bool CanStartTest() => !IsTestRunning;

        [RelayCommand(CanExecute = nameof(CanPauseTest))]
        private void PauseTest()
        {
            if (!IsTestRunning || IsTestPaused)
                return;

            if (IsSequenceLinkageEnabled)
            {
                _sequenceViewModel.MultiFlowEditor?.TogglePauseResumeCommand.Execute(null);
                SyncRunningStateFromSequence();
            }
            else
            {
                var pauseResult = _workflowExecutionSessionService.Pause();
                if (!pauseResult.Success)
                {
                    _uiLogService.Warn($"Home 独立执行暂停失败：{pauseResult.Message}");
                    return;
                }
            }
            TestItemTreeModule.PauseRunTimer();
            IsTestPaused = true;
        }

        private bool CanPauseTest() => IsTestRunning && !IsTestPaused;

        [RelayCommand(CanExecute = nameof(CanResumeTest))]
        private void ResumeTest()
        {
            if (!IsTestRunning || !IsTestPaused)
                return;

            if (IsSequenceLinkageEnabled)
            {
                _sequenceViewModel.MultiFlowEditor?.TogglePauseResumeCommand.Execute(null);
                SyncRunningStateFromSequence();
            }
            else
            {
                var resumeResult = _workflowExecutionSessionService.Resume();
                if (!resumeResult.Success)
                {
                    _uiLogService.Warn($"Home 独立执行继续失败：{resumeResult.Message}");
                    return;
                }
            }
            TestItemTreeModule.ResumeRunTimer();
            IsTestPaused = false;
        }

        private bool CanResumeTest() => IsTestRunning && IsTestPaused;

        [RelayCommand(CanExecute = nameof(CanCancelTest))]
        private void CancelTest()
        {
            if (!IsTestRunning)
                return;

            _isCurrentRunCanceled = true;
            _hasPendingYieldRecord = false;
            if (IsSequenceLinkageEnabled)
            {
                _sequenceViewModel.MultiFlowEditor?.StopCommand.Execute(null);
                SyncRunningStateFromSequence();
            }
            else
            {
                _homeRunCts?.Cancel();
                var stopResult = _workflowExecutionSessionService.Stop();
                if (!stopResult.Success)
                {
                    _uiLogService.Warn($"Home 独立执行取消失败：{stopResult.Message}");
                }
                TestItemTreeModule.EndStandaloneExecutionEventSession();
            }
            TestItemTreeModule.StopRunTimer();
            IsTestRunning = false;
            IsTestPaused = false;
        }

        private bool CanCancelTest() => IsTestRunning;

        private async Task RunStandaloneFromHomeAsync(CancellationToken cancellationToken)
        {
            try
            {
                await _homeWorkflowExecutionService.ExecuteCurrentConfiguredMasterAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                _uiLogService.Warn("Home 独立执行已取消。");
            }
            catch (Exception ex)
            {
                _uiLogService.Error($"Home 独立执行异常：{ex.Message}");
            }
            finally
            {
                Application.Current?.Dispatcher?.Invoke(() =>
                {
                    TestItemTreeModule.EndStandaloneExecutionEventSession();
                    TestItemTreeModule.StopRunTimer();
                    IsTestRunning = false;
                    IsTestPaused = false;
                });
            }
        }

        private async Task LoadLinkageConfigAsync()
        {
            try
            {
                var all = await _configurationManager.GetAllAsync().ConfigureAwait(false);
                if (all?.Success != true || all.Data == null)
                    return;

                var latest = all.Data
                    .OfType<SoftwareConfig>()
                    .OrderByDescending(x => x.UpdatedAt ?? DateTime.MinValue)
                    .ThenByDescending(x => x.CreatedAt)
                    .FirstOrDefault();

                if (latest != null)
                {
                    Application.Current?.Dispatcher?.Invoke(() =>
                    {
                        IsSequenceLinkageEnabled = latest.EnableHomeSequenceLinkage;
                    });
                }
            }
            catch
            {
                Application.Current?.Dispatcher?.Invoke(() => IsSequenceLinkageEnabled = true);
            }
        }

        private void OnSoftwareConfigChanged(SoftwareConfig config, ConfigChangeType changeType)
        {
            if (changeType != ConfigChangeType.Updated || config == null)
                return;

            Application.Current?.Dispatcher?.Invoke(() =>
            {
                IsSequenceLinkageEnabled = config.EnableHomeSequenceLinkage;
            });
        }

        private void OnSequenceEditorPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != "IsRunning" && e.PropertyName != "IsPaused")
                return;

            if (!IsSequenceLinkageEnabled)
                return;

            Application.Current?.Dispatcher?.Invoke(SyncRunningStateFromSequence);
        }

        private void OnSequenceFileSaved(object? sender, string filePath)
        {
            if (_disposed)
                return;

            Application.Current?.Dispatcher?.InvokeAsync(async () =>
            {
                await TestItemTreeModule.RefreshCommand.ExecuteAsync(null);
            });
        }

        private void SyncRunningStateFromSequence()
        {
            var editor = _sequenceViewModel.MultiFlowEditor;
            if (editor == null)
            {
                TestItemTreeModule.StopRunTimer();
                IsTestRunning = false;
                IsTestPaused = false;
                return;
            }

            IsTestRunning = editor.IsRunning;
            IsTestPaused = editor.IsPaused;
            if (!IsTestRunning)
            {
                TestItemTreeModule.StopRunTimer();
            }
            else if (IsTestPaused)
            {
                TestItemTreeModule.PauseRunTimer();
            }
            else
            {
                TestItemTreeModule.ResumeRunTimer();
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _configurationManager.Unsubscribe(_softwareConfigChangedHandler);
            if (_sequenceViewModel.MultiFlowEditor != null)
            {
                _sequenceViewModel.MultiFlowEditor.PropertyChanged -= OnSequenceEditorPropertyChanged;
                _sequenceViewModel.MultiFlowEditor.SequenceFileSaved -= OnSequenceFileSaved;
            }
            _homeRunCts?.Cancel();
            _homeRunCts?.Dispose();
            RealTimeLogModule?.Dispose();
            TestItemTreeModule?.Dispose();
            _disposed = true;
        }
    }
}
