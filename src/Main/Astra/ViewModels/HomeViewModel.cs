using Astra.Services.Home;
using Astra.Services.Logging;
using Astra.Core.Configuration.Abstractions;
using Astra.Configuration;
using Astra.Core.Configuration;
using Astra.UI.Abstractions.Home;
using Astra.Core.Triggers;
using Astra.UI.Helpers;
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
using System.Windows.Threading;

namespace Astra.ViewModels
{
    public partial class HomeViewModel : ObservableObject, IDisposable, IAutoTriggerHomeRunContext, IHomeDisplayedSnSink
    {
        private bool _disposed;
        private bool _hasPendingYieldRecord;
        private bool _isCurrentRunCanceled;
        private bool _wasTestRunning;
        private readonly IYieldDailyStatsService _yieldDailyStatsService;
        private readonly IConfigurationManager _configurationManager;
        private readonly IWorkflowExecutionSessionService _workflowExecutionSessionService;
        private readonly IHomeWorkflowExecutionService _homeWorkflowExecutionService;
        private readonly IManualBarcodeContext _manualBarcodeContext;
        private readonly Action<SoftwareConfig, ConfigChangeType> _softwareConfigChangedHandler;
        private readonly SequenceViewModel _sequenceViewModel;
        private readonly IUiLogService _uiLogService;
        private readonly IScanModeState _homeScanModeState;
        private readonly IAutoTriggerLifecycle _autoTriggerLifecycle;
        private CancellationTokenSource? _homeRunCts;
        private Task? _homeRunTask;

        [ObservableProperty]
        private bool _isSequenceLinkageEnabled = true;

        /// <summary>
        /// true：开始测试前弹出扫码窗并校验条码长度；false：直接开始。
        /// </summary>
        [ObservableProperty]
        private bool _isManualScanMode;

        private int _barcodeMinLength = 6;
        private int _barcodeMaxLength = 32;

        [ObservableProperty]
        private bool _isTestRunning;

        [ObservableProperty]
        private bool _isTestPaused;

        [ObservableProperty]
        private string _currentSn = "-";

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
            IManualBarcodeContext manualBarcodeContext,
            IUiLogService uiLogService,
            SequenceViewModel sequenceViewModel,
            IScanModeState homeScanModeState,
            IAutoTriggerLifecycle autoTriggerLifecycle)
        {
            _yieldDailyStatsService = yieldDailyStatsService;
            _configurationManager = configurationManager;
            _workflowExecutionSessionService = workflowExecutionSessionService;
            _homeWorkflowExecutionService = homeWorkflowExecutionService;
            _manualBarcodeContext = manualBarcodeContext ?? throw new ArgumentNullException(nameof(manualBarcodeContext));
            _sequenceViewModel = sequenceViewModel;
            _uiLogService = uiLogService;
            _homeScanModeState = homeScanModeState ?? throw new ArgumentNullException(nameof(homeScanModeState));
            _autoTriggerLifecycle = autoTriggerLifecycle ?? throw new ArgumentNullException(nameof(autoTriggerLifecycle));
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

            _homeScanModeState.IsAutoScanMode = !IsManualScanMode;

            OnIsManualScanModeChanged(false);
        }

        partial void OnIsManualScanModeChanged(bool value)
        {
            _homeScanModeState.IsAutoScanMode = !value;
            _ = _autoTriggerLifecycle.ApplyCurrentModeAsync();
            StartTestCommand.NotifyCanExecuteChanged();
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

        /// <summary>
        /// 仅用于<strong>手动</strong>运行模式：扫码（或联动序列）后启动测试。
        /// 自动模式下由 PLC 等触发器在后台轮询，满足条件即自动执行脚本，无需也不应点此按钮。
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanStartTest))]
        private Task StartTestAsync()
        {
            if (!IsManualScanMode)
            {
                return Task.CompletedTask;
            }

            string? manualSn = null;
            if (!TryGetManualBarcodeFromDialog(out manualSn))
            {
                return Task.CompletedTask;
            }

            if (IsSequenceLinkageEnabled)
                _manualBarcodeContext.PendingBarcode = manualSn;
            else
                _manualBarcodeContext.PendingBarcode = null;

            UpdateCurrentSn(manualSn);
            RealTimeLogModule.ClearLogsCommand.Execute(null);
            _hasPendingYieldRecord = true;
            _isCurrentRunCanceled = false;

            if (IsSequenceLinkageEnabled)
            {
                TestItemTreeModule.EndStandaloneExecutionEventSession();
                _sequenceViewModel.MultiFlowEditor?.PlayCommand.Execute(null);
                SyncRunningStateFromSequence();
                return Task.CompletedTask;
            }

            TestItemTreeModule.ResetForNewRun();
            TestItemTreeModule.BeginStandaloneExecutionEventSession();
            IsTestRunning = true;
            IsTestPaused = false;
            _homeRunCts?.Cancel();
            _homeRunCts?.Dispose();
            _homeRunCts = new CancellationTokenSource();
            _homeRunTask = RunStandaloneFromHomeAsync(_homeRunCts.Token, manualSn);
            return Task.CompletedTask;
        }

        [RelayCommand]
        private void SelectManualScanMode() => IsManualScanMode = true;

        [RelayCommand]
        private void SelectAutoScanMode() => IsManualScanMode = false;

        private bool TryGetManualBarcodeFromDialog(out string? barcode)
        {
            barcode = null;
            var min = Math.Min(_barcodeMinLength, _barcodeMaxLength);
            var max = Math.Max(_barcodeMinLength, _barcodeMaxLength);
            var owner = Application.Current?.MainWindow;
            if (!ScanCodeDialog.Show(owner, out var text, "扫码", "请使用扫码枪扫描，或手动输入条码后确定。", min, max))
                return false;
            barcode = text;
            return !string.IsNullOrEmpty(barcode);
        }

        private bool CanStartTest() => !IsTestRunning && IsManualScanMode;

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

        private async Task RunStandaloneFromHomeAsync(CancellationToken cancellationToken, string? manualBarcode)
        {
            try
            {
                await _homeWorkflowExecutionService.ExecuteCurrentConfiguredMasterAsync(cancellationToken, manualBarcode).ConfigureAwait(false);
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
                // 用 Background 优先级，确保先排队的 Normal 优先级节点状态回调全部执行完毕后再关闭会话；
                // 否则 Invoke 默认 Send 优先级会抢先于 InvokeAsync(Normal)，导致并行子流程的晚到事件被丢弃。
                var dispatcher = Application.Current?.Dispatcher;
                if (dispatcher != null)
                {
                    dispatcher.Invoke(() =>
                    {
                        TestItemTreeModule.EndStandaloneExecutionEventSession();
                        TestItemTreeModule.StopRunTimer();
                        IsTestRunning = false;
                        IsTestPaused = false;
                    }, System.Windows.Threading.DispatcherPriority.Background);
                }
            }
        }

        bool IAutoTriggerHomeRunContext.IsSequenceLinkageEnabled => IsSequenceLinkageEnabled;

        bool IAutoTriggerHomeRunContext.IsExecutionBusy =>
            Application.Current?.Dispatcher?.Invoke(() => IsTestRunning || _workflowExecutionSessionService.IsRunning) ?? false;

        async Task<AutoTriggerPrepareResult> IAutoTriggerHomeRunContext.TryPrepareAutoTriggerRunAsync(CancellationToken externalCancellation, string? sn)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null)
            {
                return default;
            }

            return await dispatcher.InvokeAsync(() =>
            {
                if (IsSequenceLinkageEnabled)
                {
                    return new AutoTriggerPrepareResult { Started = false };
                }

                if (IsTestRunning || _workflowExecutionSessionService.IsRunning)
                {
                    _uiLogService.Warn("已有测试在执行，自动触发已忽略。");
                    return new AutoTriggerPrepareResult { Started = false };
                }

                RealTimeLogModule.ClearLogsCommand.Execute(null);
                TestItemTreeModule.ResetForNewRun();
                _hasPendingYieldRecord = true;
                _isCurrentRunCanceled = false;
                _manualBarcodeContext.PendingBarcode = null;
                UpdateCurrentSn(sn);
                TestItemTreeModule.BeginStandaloneExecutionEventSession();
                IsTestRunning = true;
                IsTestPaused = false;
                _homeRunCts?.Cancel();
                _homeRunCts?.Dispose();
                _homeRunCts = CancellationTokenSource.CreateLinkedTokenSource(externalCancellation);
                return new AutoTriggerPrepareResult { Started = true, LinkedCancellation = _homeRunCts };
            });
        }

        private void UpdateCurrentSn(string? sn)
        {
            CurrentSn = string.IsNullOrWhiteSpace(sn) ? "-" : sn.Trim();
        }

        async Task IHomeDisplayedSnSink.SetDisplayedSnAsync(string? sn, CancellationToken cancellationToken)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null)
            {
                return;
            }

            await dispatcher
                .InvokeAsync(() => UpdateCurrentSn(sn), DispatcherPriority.Normal, cancellationToken)
                .Task.ConfigureAwait(false);
        }

        async Task IAutoTriggerHomeRunContext.CompleteAutoTriggerRunAsync()
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null)
            {
                return;
            }

            await dispatcher.InvokeAsync(() =>
            {
                TestItemTreeModule.EndStandaloneExecutionEventSession();
                TestItemTreeModule.StopRunTimer();
                IsTestRunning = false;
                IsTestPaused = false;
            }, System.Windows.Threading.DispatcherPriority.Background);
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
                        IsManualScanMode = latest.HomeStartInManualScanMode;
                        _barcodeMinLength = latest.BarcodeMinLength;
                        _barcodeMaxLength = latest.BarcodeMaxLength;
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
                IsManualScanMode = config.HomeStartInManualScanMode;
                _barcodeMinLength = config.BarcodeMinLength;
                _barcodeMaxLength = config.BarcodeMaxLength;
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

            var wasRunning = IsTestRunning;
            IsTestRunning = editor.IsRunning;
            IsTestPaused = editor.IsPaused;

            // 联动：从序列界面点「启动」时不会走主页 StartTest 的 ResetForNewRun，此处必须在 IsRunning 上升沿启动总表计时，
            // 否则 OnSummaryTimerTick 因 _isRunActive==false 不会刷新运行中叶子耗时与 SummaryTime。
            if (IsSequenceLinkageEnabled && IsTestRunning && !wasRunning)
            {
                TestItemTreeModule.ResetForNewRun();
            }

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
            IoMonitorModule?.Dispose();
            _disposed = true;
        }
    }
}
