using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Astra.Services.Home;
using Astra.Core.Configuration.Abstractions;
using Astra.Core.Configuration;
using Astra.Configuration;
using Astra.Core.Nodes.Models;
using Astra.UI.Abstractions.Nodes;
using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Astra.UI.Services;
using Astra.UI.Styles.Controls.TreeViewEx;
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
        /// <summary>
        /// 多子流程并行时，各引擎在线程池上几乎同时上报 Running，若各自 InvokeAsync 会在 Dispatcher 里排队，视觉上像「先后开始」。
        /// 将 Running 先收进列表，再用 ContextIdle 上循环 drain，避免多次 InvokeAsync 排队造成「先后亮」。
        /// </summary>
        private readonly object _runningCoalesceLock = new object();
        private readonly List<WorkflowNodeExecutionChangedEventArgs> _coalescedRunningEvents = new();
        private bool _runningFlushScheduled;
        /// <summary>为 true 时 <see cref="TryUpdateNodeStatus"/> 对 Running 使用单行一次 PropertyChanged，减轻 TreeView 逐行重绘。</summary>
        private bool _applyingParallelRunningBatch;
        /// <summary>暂停期间 <see cref="UpdateSummary"/> 使用的冻结时刻，避免任意刷新把运行耗时按墙钟继续推。</summary>
        private DateTime? _summaryClockHoldTime;
        /// <summary>本次暂停开始的墙钟时刻，用于恢复时把 <see cref="_nodeStartTimes"/> / 叶子 <see cref="TestTreeNodeItem.StartedAt"/> 前移，排除暂停段。</summary>
        private DateTime? _wallPauseStartedAt;

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

        /// <summary>首页测试项树「测试项名称」列宽（DIP），由 <see cref="RecalculateTestItemNameColumnWidth"/> 按最长名称与缩进估算，默认不超过 400 DIP。</summary>
        [ObservableProperty]
        private double _testItemNameColumnWidth = 280;

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
                RecalculateTestItemNameColumnWidth();
                IsLoading = false;
            }
        }

        /// <summary>
        /// 按树中全部节点名称、层级缩进与首列模板装饰估算列宽；宽度限制在 [minW, maxW]，默认最大 400 DIP，超出部分在单元格内省略显示（见首列模板 ToolTip）。
        /// </summary>
        private void RecalculateTestItemNameColumnWidth()
        {
            const double indentSize = LevelToIndentConverter.DefaultIndentSize;
            const double expanderWidth = 20;
            const double leafDotTotal = 13;
            const double rootIconTotal = 17;
            const double badgeChrome = 38;
            const double fudge = 20;
            const double minW = 220;
            const double maxW = 400;

            if (Roots.Count == 0)
            {
                if (Math.Abs(TestItemNameColumnWidth - minW) > 0.5)
                    TestItemNameColumnWidth = minW;
                return;
            }

            var px = GetPixelsPerDipSafe();
            double max = 0;
            foreach (var root in Roots)
                AccumulateFirstNameColumnWidth(root, 0, ref max, indentSize, expanderWidth, leafDotTotal, rootIconTotal, badgeChrome, px);

            var w = Math.Clamp(max + fudge, minW, maxW);
            if (Math.Abs(TestItemNameColumnWidth - w) > 0.5)
                TestItemNameColumnWidth = w;
        }

        private static void AccumulateFirstNameColumnWidth(
            TestTreeNodeItem node,
            int level,
            ref double max,
            double indentSize,
            double expanderW,
            double leafDotTotal,
            double rootIconTotal,
            double badgeChrome,
            double pixelsPerDip)
        {
            var prefix = level * indentSize + expanderW;
            if (node.IsRoot)
            {
                prefix += rootIconTotal;
                var nameW = MeasureUiTextWidth(node.Name, 13, FontWeights.SemiBold, pixelsPerDip);
                var countW = MeasureUiTextWidth(node.Children.Count.ToString(CultureInfo.InvariantCulture), 11, FontWeights.SemiBold, pixelsPerDip);
                max = Math.Max(max, prefix + nameW + badgeChrome + countW);
            }
            else
            {
                prefix += leafDotTotal;
                var nameW = MeasureUiTextWidth(node.Name, 13, FontWeights.Normal, pixelsPerDip);
                max = Math.Max(max, prefix + nameW);
            }

            foreach (var c in node.Children)
                AccumulateFirstNameColumnWidth(c, level + 1, ref max, indentSize, expanderW, leafDotTotal, rootIconTotal, badgeChrome, pixelsPerDip);
        }

        private static double MeasureUiTextWidth(string? text, double fontSize, FontWeight weight, double pixelsPerDip)
        {
            if (string.IsNullOrEmpty(text))
                return 0;

            var typeface = new Typeface(SystemFonts.MessageFontFamily, FontStyles.Normal, weight, FontStretches.Normal);
            var ft = new FormattedText(
                text,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                typeface,
                fontSize,
                Brushes.Black,
                pixelsPerDip);
            return ft.WidthIncludingTrailingWhitespace;
        }

        private static double GetPixelsPerDipSafe()
        {
            try
            {
                var w = Application.Current?.MainWindow;
                if (w != null)
                    return VisualTreeHelper.GetDpi(w).PixelsPerDip;
            }
            catch
            {
                // ignored
            }

            return 1.0;
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

            if (e == null)
                return;

            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null)
                return;

            // 非 Running：必须先刷掉挂起的 Running，避免完成态早于「运行中」展示
            if (e.State != NodeExecutionState.Running)
            {
                void ApplyTerminal()
                {
                    FlushCoalescedRunningEvents();
                    if (string.IsNullOrWhiteSpace(e.NodeId))
                        return;
                    if (!TryUpdateNodeStatus(Roots, e.NodeId, e.WorkflowKey, e.State, e.DetailMessage, e.UiPayload))
                        return;
                    UpdateSummary(Roots.ToList());
                }

                if (dispatcher.CheckAccess())
                    ApplyTerminal();
                else
                    dispatcher.Invoke(ApplyTerminal, DispatcherPriority.Normal);
                return;
            }

            if (dispatcher.CheckAccess())
            {
                lock (_runningCoalesceLock)
                {
                    _coalescedRunningEvents.Add(e);
                }

                DrainCoalescedRunningQueue();
                return;
            }

            var shouldPump = false;
            lock (_runningCoalesceLock)
            {
                _coalescedRunningEvents.Add(e);
                if (!_runningFlushScheduled)
                {
                    _runningFlushScheduled = true;
                    shouldPump = true;
                }
            }

            if (!shouldPump)
                return;

            try
            {
                // Invoke：在引擎线程上同步等到 UI 刷完本批，避免 BeginInvoke 与其它消息交错导致「逐行亮」
                dispatcher.Invoke(ProcessCoalescedRunningBatches, DispatcherPriority.ContextIdle);
            }
            catch
            {
                lock (_runningCoalesceLock)
                {
                    _runningFlushScheduled = false;
                }

                throw;
            }
        }

        private void ProcessCoalescedRunningBatches()
        {
            if (_disposed)
            {
                lock (_runningCoalesceLock)
                {
                    _coalescedRunningEvents.Clear();
                    _runningFlushScheduled = false;
                }

                return;
            }

            try
            {
                DrainCoalescedRunningQueue();
            }
            catch
            {
                lock (_runningCoalesceLock)
                {
                    _runningFlushScheduled = false;
                }

                throw;
            }
        }

        /// <summary>在 UI 线程上把当前队列中的 Running 事件全部应用掉（可连续多轮，直到队列空）。</summary>
        private void DrainCoalescedRunningQueue()
        {
            while (true)
            {
                List<WorkflowNodeExecutionChangedEventArgs> batch;
                lock (_runningCoalesceLock)
                {
                    if (_coalescedRunningEvents.Count == 0)
                    {
                        _runningFlushScheduled = false;
                        return;
                    }

                    batch = _coalescedRunningEvents.ToList();
                    _coalescedRunningEvents.Clear();
                }

                var unifiedNow = DateTime.Now;
                var any = false;
                _applyingParallelRunningBatch = true;
                try
                {
                    foreach (var ev in batch)
                    {
                        if (ev.ParallelRunningNodeIds is { Count: > 0 } parallelIds)
                        {
                            foreach (var nid in parallelIds)
                            {
                                if (string.IsNullOrWhiteSpace(nid))
                                    continue;
                                if (TryUpdateNodeStatus(Roots, nid, ev.WorkflowKey, ev.State, ev.DetailMessage, ev.UiPayload, unifiedNow))
                                    any = true;
                            }
                        }
                        else if (!string.IsNullOrWhiteSpace(ev.NodeId))
                        {
                            if (TryUpdateNodeStatus(Roots, ev.NodeId, ev.WorkflowKey, ev.State, ev.DetailMessage, ev.UiPayload, unifiedNow))
                                any = true;
                        }
                    }
                }
                finally
                {
                    _applyingParallelRunningBatch = false;
                }

                if (any)
                    UpdateSummary(Roots.ToList());
            }
        }

        private void FlushCoalescedRunningEvents()
        {
            var d = Application.Current?.Dispatcher;
            if (d == null)
                return;

            if (d.CheckAccess())
                DrainCoalescedRunningQueue();
            else
                d.Invoke(DrainCoalescedRunningQueue, DispatcherPriority.Normal);
        }

        /// <summary>并行多子流程时节点 Id 可能重复，计时字典须带流程键区分。</summary>
        private static string NodeTimingKey(string? workflowKey, string nodeId)
        {
            return string.IsNullOrWhiteSpace(workflowKey)
                ? nodeId
                : $"{workflowKey}\u001f{nodeId}";
        }

        private bool TryUpdateNodeStatus(
            IEnumerable<TestTreeNodeItem> nodes,
            string nodeId,
            string? workflowKey,
            NodeExecutionState state,
            string? detailMessage,
            IReadOnlyDictionary<string, object>? uiPayload,
            DateTime? unifiedWallClock = null)
        {
            foreach (var node in nodes)
            {
                var idMatches = string.Equals(node.NodeId, nodeId, StringComparison.Ordinal);
                // 引擎会带 WorkFlowKey；多子流程并行时节点 Id 可能重复，必须按子流程区分。无 WorkflowKey 时退化为仅按 NodeId（兼容旧事件）。
                var workflowMatches = string.IsNullOrWhiteSpace(workflowKey)
                    || string.Equals(node.SubWorkflowId, workflowKey, StringComparison.Ordinal);

                // 叶子或「主流程插件单行根」（IsRoot 且带 NodeId）
                var isExecutableRow = !node.IsRoot || !string.IsNullOrWhiteSpace(node.NodeId);
                if (isExecutableRow && idMatches && workflowMatches)
                {
                    var now = unifiedWallClock ?? DateTime.Now;
                    var timingKey = NodeTimingKey(
                        string.IsNullOrWhiteSpace(node.SubWorkflowId) ? workflowKey : node.SubWorkflowId,
                        nodeId);

                    if (state == NodeExecutionState.Running && _applyingParallelRunningBatch)
                    {
                        var statusText = MapStatus(state);
                        var msg = BuildRunningStatusMessage(node, detailMessage);
                        var chartVis = ShowChartAction &&
                            (node.SupportsHomeChartButton || node.HasChartData);
                        node.ApplyRunningBatchVisual(now, statusText, msg, chartVis, uiPayload);
                        _nodeStartTimes[timingKey] = now;
                        if (_executionStartTime == null)
                            _executionStartTime = now;
                        _executionEndTime = null;
                        return true;
                    }

                    node.Status = MapStatus(state);
                    node.TestTime = now;
                    ApplyNodeStatusMessage(node, state, detailMessage);

                    if (uiPayload != null)
                    {
                        ApplyUiPayload(node, uiPayload);
                    }

                    node.IsChartButtonVisible = ShowChartAction &&
                        (node.SupportsHomeChartButton || node.HasChartData);

                    if (state == NodeExecutionState.Running)
                    {
                        _nodeStartTimes[timingKey] = now;
                        node.StartedAt = now;
                        node.CompletedAt = null;
                        node.TestDurationSeconds = 0d;
                        if (_executionStartTime == null)
                            _executionStartTime = now;
                        _executionEndTime = null;
                    }
                    else if (_nodeStartTimes.TryGetValue(timingKey, out var startedAt))
                    {
                        var duration = now - startedAt;
                        if (duration < TimeSpan.Zero)
                            duration = TimeSpan.Zero;
                        node.TestDurationSeconds = Math.Round(duration.TotalSeconds, 2);
                        node.CompletedAt = now;
                        _nodeStartTimes.Remove(timingKey);
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

                if (TryUpdateNodeStatus(node.Children, nodeId, workflowKey, state, detailMessage, uiPayload, unifiedWallClock))
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
            // 子流程分组根（无 NodeId）不显示消息；主流程插件单行根与叶子一样展示。
            if (node.IsRoot && string.IsNullOrWhiteSpace(node.NodeId))
            {
                return;
            }

            if (state == NodeExecutionState.Running)
            {
                node.StatusMessage = BuildRunningStatusMessage(node, detailMessage);
            }
            else
            {
                node.StatusMessage = detailMessage ?? string.Empty;
            }
        }

        private static string BuildRunningStatusMessage(TestTreeNodeItem node, string? detailMessage)
        {
            if (node.IsRoot && string.IsNullOrWhiteSpace(node.NodeId))
                return node.StatusMessage;
            return string.IsNullOrWhiteSpace(detailMessage) ? "运行中…" : detailMessage.Trim();
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
            if (!node.IsRoot || !string.IsNullOrWhiteSpace(node.NodeId))
            {
                node.IsChartButtonVisible = showChartActionPreference &&
                    (node.SupportsHomeChartButton || node.HasChartData);
            }

            foreach (var child in node.Children)
            {
                ApplyChartButtonVisibility(child, showChartActionPreference);
            }
        }

        private void UpdateSummary(IReadOnlyList<TestTreeNodeItem> roots)
        {
            // 暂停时不能用实时墙钟，否则 OnNodeExecutionChanged 等触发的汇总会把「测试项时间」和根聚合继续拉长
            var now = _summaryClockHoldTime ?? DateTime.Now;
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
            _summaryClockHoldTime = null;
            _wallPauseStartedAt = null;

            ResetNodeDurations(Roots);
        }

        public void PauseRunTimer()
        {
            if (!_isRunActive || _isRunPaused)
                return;

            var now = DateTime.Now;
            _summaryClockHoldTime = now;
            _wallPauseStartedAt = now;
            if (_runSegmentStartedAt.HasValue)
            {
                var segment = now - _runSegmentStartedAt.Value;
                if (segment > TimeSpan.Zero)
                    _accumulatedRunDuration += segment;
            }
            _runSegmentStartedAt = null;
            _isRunPaused = true;
            SummaryTime = FormatDurationWithMilliseconds(_accumulatedRunDuration);
            // 立即用冻结时刻刷新叶子/根行耗时，避免仍显示暂停前最后一次 tick 的值
            UpdateSummary(Roots.ToList());
        }

        public void ResumeRunTimer()
        {
            if (!_isRunActive || !_isRunPaused)
                return;

            if (_wallPauseStartedAt.HasValue)
            {
                var pauseWall = DateTime.Now - _wallPauseStartedAt.Value;
                if (pauseWall > TimeSpan.Zero)
                {
                    foreach (var k in _nodeStartTimes.Keys.ToList())
                    {
                        _nodeStartTimes[k] = _nodeStartTimes[k].Add(pauseWall);
                    }

                    foreach (var r in Roots)
                    {
                        ShiftRunningLeavesStartedAt(r, pauseWall);
                    }
                }

                _wallPauseStartedAt = null;
            }

            _summaryClockHoldTime = null;
            _runSegmentStartedAt = DateTime.Now;
            _isRunPaused = false;
            UpdateSummary(Roots.ToList());
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
            _summaryClockHoldTime = null;
            _wallPauseStartedAt = null;
            SummaryTime = FormatDurationWithMilliseconds(_accumulatedRunDuration);
        }

        /// <summary>
        /// 恢复执行时把运行中叶子的 StartedAt 前移，使根聚合与后续 tick 的耗时不含暂停段。
        /// </summary>
        private static void ShiftRunningLeavesStartedAt(TestTreeNodeItem node, TimeSpan pauseWall)
        {
            var timingRow = !node.IsRoot || (!string.IsNullOrWhiteSpace(node.NodeId) && node.Children.Count == 0);
            if (timingRow &&
                string.Equals(node.Status, "Running", StringComparison.OrdinalIgnoreCase) &&
                node.StartedAt.HasValue)
            {
                node.StartedAt = node.StartedAt.Value.Add(pauseWall);
            }

            foreach (var child in node.Children)
            {
                ShiftRunningLeavesStartedAt(child, pauseWall);
            }
        }

        public void BeginStandaloneExecutionEventSession()
        {
            _acceptStandaloneExecutionEvents = true;
        }

        public void EndStandaloneExecutionEventSession()
        {
            _acceptStandaloneExecutionEvents = false;
        }

        private void ResetNodeDurations(IEnumerable<TestTreeNodeItem> nodes)
        {
            foreach (var node in nodes)
            {
                node.TestDurationSeconds = 0d;
                node.StartedAt = null;
                node.CompletedAt = null;
                node.StatusMessage = string.Empty;
                if (node.IsRoot && string.IsNullOrWhiteSpace(node.NodeId))
                    node.Status = "Ready";
                else if (!node.IsRoot || !string.IsNullOrWhiteSpace(node.NodeId))
                    node.Status = "Ready";

                if (!node.IsRoot || !string.IsNullOrWhiteSpace(node.NodeId))
                {
                    node.HasChartData = false;
                    node.ChartArtifactKey = string.Empty;
                    node.IsChartButtonVisible = ShowChartAction && node.SupportsHomeChartButton;
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

                var timingKey = NodeTimingKey(node.SubWorkflowId, node.NodeId);
                if (_nodeStartTimes.TryGetValue(timingKey, out var startedAt))
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
                if (!string.IsNullOrWhiteSpace(root.NodeId))
                {
                    root.TestTime = now;
                    return;
                }

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
            if (item == null || (item.IsRoot && string.IsNullOrWhiteSpace(item.NodeId)))
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
                if (!node.IsRoot || !string.IsNullOrWhiteSpace(node.NodeId))
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

            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher != null)
            {
                try
                {
                    if (dispatcher.CheckAccess())
                        FlushCoalescedRunningEvents();
                    else
                        dispatcher.Invoke(FlushCoalescedRunningEvents, DispatcherPriority.Normal);
                }
                catch
                {
                    lock (_runningCoalesceLock)
                    {
                        _coalescedRunningEvents.Clear();
                    }
                }
            }

            _disposed = true;
        }
    }

    public partial class TestTreeNodeItem : ObservableObject
    {
        [ObservableProperty]
        private string _nodeId = string.Empty;

        /// <summary>所属子流程 Id（与引擎事件 <see cref="WorkflowNodeExecutionChangedEventArgs.WorkflowKey"/> 一致），用于并行时区分重复的节点 Id。</summary>
        [ObservableProperty]
        private string _subWorkflowId = string.Empty;

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

        /// <summary>是否存在可供图表展示的曲线数据（与总开关、<see cref="SupportsHomeChartButton"/> 共同决定图表按钮是否可见）。</summary>
        [ObservableProperty]
        private bool _hasChartData;

        /// <summary>加载树时设置：节点实现 <see cref="IHomeTestItemChartNode"/> 且开启显示按钮，或实现 <see cref="IHomeTestItemChartEligibleNode"/>；新运行开始时不清除。</summary>
        [ObservableProperty]
        private bool _supportsHomeChartButton;

        /// <summary>最近一次执行写入的 Raw 产物键（展示用）。</summary>
        [ObservableProperty]
        private string _chartArtifactKey = string.Empty;

        /// <summary>节点执行过程中的说明（错误、跳过原因、成功附加信息等）。</summary>
        [ObservableProperty]
        private string _statusMessage = string.Empty;

        public ObservableCollection<TestTreeNodeItem> Children { get; } = new();

        /// <summary>
        /// 并行批刷新：直接写字段后 <see cref="ObservableObject.OnPropertyChanged(string?)"/> 一次，
        /// 避免 Status/TestTime/StartedAt 等多属性各触发一轮 TreeView 布局（肉眼像逐行先后变色）。
        /// </summary>
        internal void ApplyRunningBatchVisual(
            DateTime unifiedNow,
            string statusDisplay,
            string statusMessage,
            bool chartButtonVisible,
            IReadOnlyDictionary<string, object>? uiPayload)
        {
            _status = statusDisplay;
            _testTime = unifiedNow;
            _statusMessage = statusMessage;
            _startedAt = unifiedNow;
            _completedAt = null;
            _testDurationSeconds = 0d;
            _isChartButtonVisible = chartButtonVisible;

            if (uiPayload != null)
                ApplyPayloadFieldsForBatch(uiPayload);

            OnPropertyChanged(string.Empty);
        }

        private void ApplyPayloadFieldsForBatch(IReadOnlyDictionary<string, object> payload)
        {
            if (TryGetDoubleFromPayload(payload, NodeUiOutputKeys.ActualValue, out var av))
                _actualValue = av;

            if (TryGetDoubleFromPayload(payload, NodeUiOutputKeys.LowerLimit, out var lo))
                _lowerLimit = lo;

            if (TryGetDoubleFromPayload(payload, NodeUiOutputKeys.UpperLimit, out var hi))
                _upperLimit = hi;

            if (payload.TryGetValue(NodeUiOutputKeys.HasChartData, out var hc) && hc is bool hasChart)
                _hasChartData = hasChart;

            if (payload.TryGetValue(NodeUiOutputKeys.ChartArtifactKey, out var ck) && ck is string key && !string.IsNullOrWhiteSpace(key))
                _chartArtifactKey = key.Trim();
        }

        private static bool TryGetDoubleFromPayload(IReadOnlyDictionary<string, object> payload, string key, out double value)
        {
            value = default;
            if (!payload.TryGetValue(key, out var o) || o == null)
                return false;

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
                    value = Convert.ToDouble(conv, CultureInfo.InvariantCulture);
                    return true;
                }
                catch
                {
                    return false;
                }
            }

            return false;
        }
    }
}
