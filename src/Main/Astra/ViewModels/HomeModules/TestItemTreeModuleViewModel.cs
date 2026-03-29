using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Astra.Services.Home;
using Astra.Core.Configuration.Abstractions;
using Astra.Core.Configuration;
using Astra.Configuration;
using Astra.Core.Nodes.Models;
using Astra.Core.Nodes.Ui;
using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Astra.UI.Services;
using Astra.Views.HomeModules;

namespace Astra.ViewModels.HomeModules
{
    public partial class TestItemTreeModuleViewModel : ObservableObject, IDisposable
    {
        private readonly ITestItemTreeDataProvider _provider;
        private readonly IConfigurationManager _configurationManager;
        private readonly Action<SoftwareConfig, ConfigChangeType> _softwareConfigChangedHandler;
        private readonly IWorkflowExecutionSessionService _workflowExecutionSessionService;
        private readonly DispatcherTimer _summaryTimer;
        private string? _lastObservedWorkflowPath;
        private bool _isSequenceHomeLinkageEnabled = true;
        private bool _acceptStandaloneExecutionEvents;
        private bool _disposed;
        private DateTime? _executionStartTime;
        private DateTime? _executionEndTime;
        private bool _isRunActive;
        private bool _isRunPaused;
        private TimeSpan _accumulatedRunDuration = TimeSpan.Zero;
        private DateTime? _runSegmentStartedAt;
        private readonly Dictionary<string, DateTime> _nodeStartTimes = new(StringComparer.Ordinal);

        public ObservableCollection<TestTreeNodeItem> Roots { get; } = new();

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private string _summaryResult = "READY";

        [ObservableProperty]
        private string _summaryTime = "00:00:00.000";

        [ObservableProperty]
        private int _totalPassCount;

        [ObservableProperty]
        private int _totalFailCount;

        [ObservableProperty]
        private bool _showChartAction = true;

        public TestItemTreeModuleViewModel(
            ITestItemTreeDataProvider provider,
            IConfigurationManager configurationManager,
            IWorkflowExecutionSessionService workflowExecutionSessionService)
        {
            _provider = provider;
            _configurationManager = configurationManager;
            _workflowExecutionSessionService = workflowExecutionSessionService;
            _softwareConfigChangedHandler = OnSoftwareConfigChanged;
            _configurationManager.Subscribe(_softwareConfigChangedHandler);
            _workflowExecutionSessionService.NodeExecutionChanged += OnNodeExecutionChanged;
            _summaryTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(100)
            };
            _summaryTimer.Tick += OnSummaryTimerTick;
            _summaryTimer.Start();
            _ = InitializeObservedWorkflowPathAsync();
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
            try
            {
                var roots = await _provider.LoadRootNodesAsync();
                Roots.Clear();
                var rootIndex = 0;
                foreach (var r in roots)
                {
                    ApplyGroupColorIndex(r, rootIndex % 4);
                    ApplyChartButtonVisibility(r, ShowChartAction);
                    Roots.Add(r);
                    rootIndex++;
                }

                UpdateSummary(roots);
            }
            catch (Exception ex)
            {
                // 错误信息写入日志，不在模块内单独展示错误提示控件
                SummaryResult = "ERROR";
                SummaryTime = "00:00:00.000";
                TotalPassCount = 0;
                TotalFailCount = 0;
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void OnSoftwareConfigChanged(SoftwareConfig config, ConfigChangeType changeType)
        {
            if (changeType != ConfigChangeType.Updated)
                return;

            _isSequenceHomeLinkageEnabled = config.EnableHomeSequenceLinkage;

            var currentWorkflowPath = NormalizePath(ResolvePreferredScriptPath(config));
            _lastObservedWorkflowPath = currentWorkflowPath;

            // 配置更新后始终刷新主页测试项树：
            // 即使脚本路径未变，脚本内容也可能已在 Sequence 保存时发生变化。
            Application.Current?.Dispatcher?.InvokeAsync(async () => await LoadTreeAsync());
        }

        private async Task InitializeObservedWorkflowPathAsync()
        {
            try
            {
                var all = await _configurationManager.GetAllAsync().ConfigureAwait(false);
                var latest = all?.Data?
                    .OfType<SoftwareConfig>()
                    .OrderByDescending(x => x.UpdatedAt ?? DateTime.MinValue)
                    .ThenByDescending(x => x.CreatedAt)
                    .FirstOrDefault();

                _lastObservedWorkflowPath = NormalizePath(ResolvePreferredScriptPath(latest));
                if (latest != null)
                {
                    _isSequenceHomeLinkageEnabled = latest.EnableHomeSequenceLinkage;
                }
            }
            catch
            {
                _lastObservedWorkflowPath = null;
                _isSequenceHomeLinkageEnabled = true;
            }
        }

        private void OnNodeExecutionChanged(object? sender, WorkflowNodeExecutionChangedEventArgs e)
        {
            if (_disposed)
                return;

            if (!_isSequenceHomeLinkageEnabled && !_acceptStandaloneExecutionEvents)
                return;

            if (e == null || string.IsNullOrWhiteSpace(e.NodeId))
                return;

            Application.Current?.Dispatcher?.InvokeAsync(() =>
            {
                if (!TryUpdateNodeStatus(Roots, e.NodeId, e.State, e.DetailMessage, e.UiPayload))
                    return;

                UpdateSummary(Roots.ToList());
            });
        }

        private bool TryUpdateNodeStatus(
            IEnumerable<TestTreeNodeItem> nodes,
            string nodeId,
            NodeExecutionState state,
            string? detailMessage,
            IReadOnlyDictionary<string, object>? uiPayload)
        {
            foreach (var node in nodes)
            {
                if (!node.IsRoot && string.Equals(node.NodeId, nodeId, StringComparison.Ordinal))
                {
                    var now = DateTime.Now;
                    node.Status = MapStatus(state);
                    node.TestTime = now;
                    ApplyNodeStatusMessage(node, state, detailMessage);

                    if (uiPayload != null)
                    {
                        ApplyUiPayload(node, uiPayload);
                    }

                    node.IsChartButtonVisible = ShowChartAction && node.HasChartData;

                    if (state == NodeExecutionState.Running)
                    {
                        _nodeStartTimes[nodeId] = now;
                        node.StartedAt = now;
                        node.CompletedAt = null;
                        node.TestDurationSeconds = 0d;
                        if (_executionStartTime == null)
                            _executionStartTime = now;
                        _executionEndTime = null;
                    }
                    else if (_nodeStartTimes.TryGetValue(nodeId, out var startedAt))
                    {
                        var duration = now - startedAt;
                        if (duration < TimeSpan.Zero)
                            duration = TimeSpan.Zero;
                        node.TestDurationSeconds = Math.Round(duration.TotalSeconds, 2);
                        node.CompletedAt = now;
                        _nodeStartTimes.Remove(nodeId);
                    }
                    else if (string.Equals(node.Status, "Ready", StringComparison.OrdinalIgnoreCase))
                    {
                        node.StartedAt = null;
                        node.CompletedAt = null;
                        node.TestDurationSeconds = 0d;
                    }
                    else
                    {
                        node.CompletedAt = now;
                    }

                    return true;
                }

                if (TryUpdateNodeStatus(node.Children, nodeId, state, detailMessage, uiPayload))
                    return true;
            }

            return false;
        }

        private static void ApplyUiPayload(TestTreeNodeItem node, IReadOnlyDictionary<string, object> payload)
        {
            if (TryGetDouble(payload, NodeUiOutputKeys.ActualValue, out var av))
            {
                node.ActualValue = av;
            }

            if (TryGetDouble(payload, NodeUiOutputKeys.LowerLimit, out var lo))
            {
                node.LowerLimit = lo;
            }

            if (TryGetDouble(payload, NodeUiOutputKeys.UpperLimit, out var hi))
            {
                node.UpperLimit = hi;
            }

            if (payload.TryGetValue(NodeUiOutputKeys.HasChartData, out var hc) && hc is bool hasChart)
            {
                node.HasChartData = hasChart;
            }

            if (payload.TryGetValue(NodeUiOutputKeys.ChartArtifactKey, out var ck) && ck is string key && !string.IsNullOrWhiteSpace(key))
            {
                node.ChartArtifactKey = key.Trim();
            }
        }

        private static bool TryGetDouble(IReadOnlyDictionary<string, object> payload, string key, out double value)
        {
            value = default;
            if (!payload.TryGetValue(key, out var o) || o == null)
            {
                return false;
            }

            var t = o.GetType();
            if (t == typeof(double))
            {
                value = (double)o;
                return true;
            }

            if (t == typeof(float))
            {
                value = (float)o;
                return true;
            }

            if (t == typeof(int))
            {
                value = (int)o;
                return true;
            }

            if (o is IConvertible conv)
            {
                try
                {
                    value = Convert.ToDouble(conv, System.Globalization.CultureInfo.InvariantCulture);
                    return true;
                }
                catch
                {
                    return false;
                }
            }

            return false;
        }

        private static void ApplyNodeStatusMessage(TestTreeNodeItem node, NodeExecutionState state, string? detailMessage)
        {
            if (node.IsRoot)
            {
                return;
            }

            if (state == NodeExecutionState.Running)
            {
                node.StatusMessage = string.IsNullOrWhiteSpace(detailMessage) ? "运行中…" : detailMessage.Trim();
            }
            else
            {
                node.StatusMessage = detailMessage ?? string.Empty;
            }
        }

        private static string MapStatus(NodeExecutionState state)
        {
            return state switch
            {
                NodeExecutionState.Success => "Pass",
                NodeExecutionState.Failed => "Fail",
                NodeExecutionState.Running => "Running",
                NodeExecutionState.Skipped => "Skip",
                NodeExecutionState.Cancelled => "Cancel",
                _ => "Ready"
            };
        }

        private static string? ResolvePreferredScriptPath(SoftwareConfig? config)
        {
            if (config == null)
                return null;

            if (!string.IsNullOrWhiteSpace(config.CurrentWorkflowId))
                return config.CurrentWorkflowId;

            return (config.Duts ?? [])
                .Select(d => d?.WorkflowId)
                .FirstOrDefault(id => !string.IsNullOrWhiteSpace(id));
        }

        private static string? NormalizePath(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return null;
            try
            {
                return System.IO.Path.GetFullPath(path.Trim());
            }
            catch
            {
                return path.Trim();
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

        private void ApplyChartButtonVisibility(TestTreeNodeItem node, bool showChartActionPreference)
        {
            if (!node.IsRoot)
            {
                node.IsChartButtonVisible = showChartActionPreference && node.HasChartData;
            }

            foreach (var child in node.Children)
            {
                ApplyChartButtonVisibility(child, showChartActionPreference);
            }
        }

        private void UpdateSummary(IReadOnlyList<TestTreeNodeItem> roots)
        {
            var now = DateTime.Now;
            foreach (var root in roots.Where(r => r.IsRoot))
            {
                UpdateRootAggregate(root, now);
            }

            var allLeaves = new List<TestTreeNodeItem>();
            foreach (var root in roots)
            {
                CollectLeaves(root, allLeaves);
            }

            TotalPassCount = allLeaves.Count(x => string.Equals(x.Status, "Pass", StringComparison.OrdinalIgnoreCase));
            TotalFailCount = allLeaves.Count(x => string.Equals(x.Status, "Fail", StringComparison.OrdinalIgnoreCase));
            UpdateRunningNodeDurations(allLeaves, now);

            if (TotalFailCount > 0)
                SummaryResult = "NG";
            else if (TotalPassCount > 0)
                SummaryResult = "OK";
            else
                SummaryResult = "READY";
        }

        private void OnSummaryTimerTick(object? sender, EventArgs e)
        {
            if (_disposed)
                return;

            var now = DateTime.Now;

            // 运行中每 100ms 刷新一次总时间与运行项耗时，保证毫秒显示实时更新。
            if (_isRunActive && !_isRunPaused)
            {
                var leaves = new List<TestTreeNodeItem>();
                foreach (var root in Roots)
                {
                    CollectLeaves(root, leaves);
                }
                UpdateRunningNodeDurations(leaves, now);

                var current = _accumulatedRunDuration;
                if (_runSegmentStartedAt.HasValue)
                {
                    var segment = now - _runSegmentStartedAt.Value;
                    if (segment > TimeSpan.Zero)
                        current += segment;
                }
                SummaryTime = FormatDurationWithMilliseconds(current);
            }
        }

        public void ResetForNewRun()
        {
            _executionStartTime = DateTime.Now;
            _executionEndTime = null;
            _nodeStartTimes.Clear();
            SummaryTime = "00:00:00.000";
            _accumulatedRunDuration = TimeSpan.Zero;
            _runSegmentStartedAt = DateTime.Now;
            _isRunActive = true;
            _isRunPaused = false;

            ResetNodeDurations(Roots);
        }

        public void PauseRunTimer()
        {
            if (!_isRunActive || _isRunPaused)
                return;

            var now = DateTime.Now;
            if (_runSegmentStartedAt.HasValue)
            {
                var segment = now - _runSegmentStartedAt.Value;
                if (segment > TimeSpan.Zero)
                    _accumulatedRunDuration += segment;
            }
            _runSegmentStartedAt = null;
            _isRunPaused = true;
            SummaryTime = FormatDurationWithMilliseconds(_accumulatedRunDuration);
        }

        public void ResumeRunTimer()
        {
            if (!_isRunActive || !_isRunPaused)
                return;

            _runSegmentStartedAt = DateTime.Now;
            _isRunPaused = false;
        }

        public void StopRunTimer()
        {
            if (!_isRunActive)
                return;

            var now = DateTime.Now;
            if (!_isRunPaused && _runSegmentStartedAt.HasValue)
            {
                var segment = now - _runSegmentStartedAt.Value;
                if (segment > TimeSpan.Zero)
                    _accumulatedRunDuration += segment;
            }
            _runSegmentStartedAt = null;
            _isRunPaused = false;
            _isRunActive = false;
            SummaryTime = FormatDurationWithMilliseconds(_accumulatedRunDuration);
        }

        public void BeginStandaloneExecutionEventSession()
        {
            _acceptStandaloneExecutionEvents = true;
        }

        public void EndStandaloneExecutionEventSession()
        {
            _acceptStandaloneExecutionEvents = false;
        }

        private static void ResetNodeDurations(IEnumerable<TestTreeNodeItem> nodes)
        {
            foreach (var node in nodes)
            {
                node.TestDurationSeconds = 0d;
                node.StartedAt = null;
                node.CompletedAt = null;
                node.StatusMessage = string.Empty;
                if (node.IsRoot)
                    node.Status = "Ready";

                if (!node.IsRoot)
                {
                    node.HasChartData = false;
                    node.ChartArtifactKey = string.Empty;
                    node.IsChartButtonVisible = false;
                }

                if (node.Children.Count > 0)
                    ResetNodeDurations(node.Children);
            }
        }

        private void UpdateRunningNodeDurations(IEnumerable<TestTreeNodeItem> leaves, DateTime now)
        {
            foreach (var node in leaves)
            {
                if (!string.Equals(node.Status, "Running", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (_nodeStartTimes.TryGetValue(node.NodeId, out var startedAt))
                {
                    var duration = now - startedAt;
                    if (duration < TimeSpan.Zero)
                        duration = TimeSpan.Zero;
                    node.TestDurationSeconds = Math.Round(duration.TotalSeconds, 2);
                }
            }
        }

        private static string FormatDuration(TimeSpan duration)
            => $"{(int)duration.TotalHours:00}:{duration.Minutes:00}:{duration.Seconds:00}";

        private static string FormatDurationWithMilliseconds(TimeSpan duration)
            => $"{(int)duration.TotalHours:00}:{duration.Minutes:00}:{duration.Seconds:00}.{duration.Milliseconds:000}";

        private static void UpdateRootAggregate(TestTreeNodeItem root, DateTime now)
        {
            if (!root.IsRoot)
                return;

            root.StatusMessage = string.Empty;

            var leaves = new List<TestTreeNodeItem>();
            CollectLeaves(root, leaves);
            if (leaves.Count == 0)
            {
                root.Status = "Ready";
                root.TestDurationSeconds = 0d;
                root.TestTime = now;
                return;
            }

            if (leaves.Any(x => string.Equals(x.Status, "Running", StringComparison.OrdinalIgnoreCase)))
                root.Status = "Running";
            else if (leaves.Any(x => string.Equals(x.Status, "Fail", StringComparison.OrdinalIgnoreCase)))
                root.Status = "Fail";
            else if (leaves.All(x => string.Equals(x.Status, "Pass", StringComparison.OrdinalIgnoreCase)))
                root.Status = "Pass";
            else
                root.Status = "Ready";

            var startedAtCandidates = leaves
                .Where(x => x.StartedAt.HasValue)
                .Select(x => x.StartedAt!.Value)
                .ToList();
            if (startedAtCandidates.Count == 0)
            {
                root.TestDurationSeconds = 0d;
                root.TestTime = now;
                return;
            }

            var startedAt = startedAtCandidates.Min();
            DateTime endedAt;
            if (string.Equals(root.Status, "Running", StringComparison.OrdinalIgnoreCase))
            {
                endedAt = now;
            }
            else
            {
                var endCandidates = leaves
                    .Where(x => x.CompletedAt.HasValue)
                    .Select(x => x.CompletedAt!.Value)
                    .ToList();
                endedAt = endCandidates.Count > 0 ? endCandidates.Max() : now;
            }

            var duration = endedAt - startedAt;
            if (duration < TimeSpan.Zero)
                duration = TimeSpan.Zero;

            root.TestDurationSeconds = Math.Round(duration.TotalSeconds, 2);
            root.TestTime = endedAt;
        }

        partial void OnShowChartActionChanged(bool value)
        {
            foreach (var root in Roots)
            {
                ApplyChartButtonVisibility(root, value);
            }
        }

        [RelayCommand]
        private void OpenChart(TestTreeNodeItem? item)
        {
            if (item == null || item.IsRoot)
                return;

            var chartWindow = new TestItemChartWindow
            {
                Owner = Application.Current?.MainWindow,
                DataContext = item
            };
            chartWindow.Show();
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

        public void Dispose()
        {
            if (_disposed)
                return;

            _configurationManager.Unsubscribe(_softwareConfigChangedHandler);
            _workflowExecutionSessionService.NodeExecutionChanged -= OnNodeExecutionChanged;
            _summaryTimer.Stop();
            _summaryTimer.Tick -= OnSummaryTimerTick;
            _disposed = true;
        }
    }

    public partial class TestTreeNodeItem : ObservableObject
    {
        [ObservableProperty]
        private string _nodeId = string.Empty;

        [ObservableProperty]
        private string _name = string.Empty;

        [ObservableProperty]
        private string _status = string.Empty;

        [ObservableProperty]
        private DateTime _testTime = DateTime.Now;

        [ObservableProperty]
        private double _testDurationSeconds;

        [ObservableProperty]
        private DateTime? _startedAt;

        [ObservableProperty]
        private DateTime? _completedAt;

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

        [ObservableProperty]
        private bool _isChartButtonVisible;

        /// <summary>是否存在可供图表展示的曲线数据（与总开关共同决定图表按钮是否可见）。</summary>
        [ObservableProperty]
        private bool _hasChartData;

        /// <summary>最近一次执行写入的 Raw 产物键（展示用）。</summary>
        [ObservableProperty]
        private string _chartArtifactKey = string.Empty;

        /// <summary>节点执行过程中的说明（错误、跳过原因、成功附加信息等）。</summary>
        [ObservableProperty]
        private string _statusMessage = string.Empty;

        public ObservableCollection<TestTreeNodeItem> Children { get; } = new();
    }
}
