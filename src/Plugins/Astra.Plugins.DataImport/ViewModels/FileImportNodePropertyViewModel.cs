using Astra.Core.Constants;
using Astra.Plugins.DataImport.Helpers;
using Astra.Plugins.DataImport.Import;
using Astra.Plugins.DataImport.Nodes;
using Astra.UI.Helpers;
using NVHDataBridge.Models;
using System.Windows;
using System.Windows.Threading;
using ScottPlot;
using ScottPlot.Plottables;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;

namespace Astra.Plugins.DataImport.ViewModels
{
    /// <summary>
    /// 文件导入节点自定义属性面板的 ViewModel。
    /// <list type="bullet">
    ///   <item>每条通道对应一条 ScottPlot Signal 曲线</item>
    ///   <item>勾选/取消勾选 CheckBox 即时显示/隐藏对应曲线</item>
    ///   <item>全部 I/O 在后台线程执行，UI 线程只更新 Plot</item>
    /// </list>
    /// </summary>
    public sealed class FileImportNodePropertyViewModel : INotifyPropertyChanged
    {
        private readonly Plot _plot;
        private readonly Action _refreshPlot;
        private CancellationTokenSource? _previewCts;
        private CancellationTokenSource? _discoverCts;

        // 与 ChannelItem 一致的键 → Signal（多文件时为「文件名|通道名」，单文件时为通道名）
        private readonly Dictionary<string, List<Signal>> _signalMap = new(StringComparer.Ordinal);

        private string _virtualDeviceAlias = string.Empty;
        private bool _isLoadingPreview;
        private string _previewStatus = string.Empty;
        private bool _hasPreviewData;
        private bool _hasChannels;

        // ====== 可观察集合 ======

        public ObservableCollection<string> FilePaths { get; } = new();
        public ObservableCollection<ChannelItem> ChannelItems { get; } = new();

        // ====== 绑定属性 ======

        public string VirtualDeviceAlias
        {
            get => _virtualDeviceAlias;
            set { _virtualDeviceAlias = value ?? string.Empty; Notify(); }
        }

        public bool IsLoadingPreview
        {
            get => _isLoadingPreview;
            private set
            {
                _isLoadingPreview = value;
                Notify();
                Notify(nameof(IsNotLoadingPreview));
                Notify(nameof(ShowEmptyPlaceholder));
            }
        }

        public bool IsNotLoadingPreview => !_isLoadingPreview;

        public string PreviewStatus
        {
            get => _previewStatus;
            private set { _previewStatus = value ?? string.Empty; Notify(); Notify(nameof(HasPreviewStatus)); }
        }

        public bool HasPreviewStatus => !string.IsNullOrEmpty(_previewStatus);

        public bool HasPreviewData
        {
            get => _hasPreviewData;
            private set { _hasPreviewData = value; Notify(); Notify(nameof(ShowEmptyPlaceholder)); }
        }

        public bool ShowEmptyPlaceholder => !_hasPreviewData && !_isLoadingPreview;

        public bool HasChannels
        {
            get => _hasChannels;
            private set { _hasChannels = value; Notify(); }
        }

        // ====== 构造 ======

        public FileImportNodePropertyViewModel(FileImportRawNodeBase node, Plot plot, Action refreshPlot)
        {
            _plot = plot;
            _refreshPlot = refreshPlot;

            VirtualDeviceAlias = node.VirtualDeviceAlias ?? string.Empty;

            foreach (var p in node.SourceFilePaths ?? new List<string>())
                FilePaths.Add(p);

            ApplyPlotStyle();
            _refreshPlot();

            _ = RefreshChannelItemsAsync(node.SelectedChannelNames ?? new List<string>());
        }

        // ====== 公共操作 ======

        public void AddFiles(IEnumerable<string> paths)
        {
            var changed = false;
            foreach (var p in paths)
            {
                if (!string.IsNullOrWhiteSpace(p) && !FilePaths.Contains(p))
                {
                    FilePaths.Add(p);
                    changed = true;
                }
            }
            if (!changed) return;

            _ = RefreshChannelItemsAsync(preSelected: null);
        }

        public void RemoveFile(string path)
        {
            FilePaths.Remove(path);
            _ = RefreshChannelItemsAsync(preSelected: null);
        }

        public void SelectAllChannels() => BatchSetAllChannels(true);
        public void DeselectAllChannels() => BatchSetAllChannels(false);

        public void Apply(FileImportRawNodeBase target)
        {
            target.VirtualDeviceAlias = VirtualDeviceAlias;
            target.SourceFilePaths = FilePaths.ToList();
            var multi = FilePaths.Count > 1;
            target.SelectedChannelNames = ChannelItems
                .Where(c => c.IsSelected)
                .Select(c => multi
                    ? $"{Path.GetFileName(c.SourceFilePath)}{FileImportRawNodeBase.FileChannelSelectionSeparator}{c.Name}"
                    : c.Name)
                .ToList();
        }

        // ====== 私有：通道 CheckBox → 曲线可见性 ======

        private void OnChannelItemSelectionChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(ChannelItem.IsSelected)) return;
            if (sender is not ChannelItem item) return;

            if (_signalMap.TryGetValue(item.SignalMapKey, out var signals))
            {
                foreach (var sig in signals)
                    sig.IsVisible = item.IsSelected;
                _refreshPlot();
            }
        }

        private void BatchSetAllChannels(bool selected)
        {
            // 先临时退订，批量修改后统一刷新，避免 N 次 Refresh
            foreach (var item in ChannelItems)
            {
                item.PropertyChanged -= OnChannelItemSelectionChanged;
                item.IsSelected = selected;
                item.PropertyChanged += OnChannelItemSelectionChanged;
            }

            foreach (var item in ChannelItems)
            {
                if (_signalMap.TryGetValue(item.SignalMapKey, out var signals))
                    foreach (var sig in signals)
                        sig.IsVisible = selected;
            }

            _refreshPlot();
        }

        // ====== 私有：通道刷新（后台并行发现 + UI 线程重建列表） ======

        private sealed class ChannelSelectionState
        {
            public Dictionary<string, bool>? ExplicitState { get; init; }
            public bool DefaultSelected { get; init; }
            public bool FromSerializedList { get; init; }
            public bool AnyCompositeInList { get; init; }
        }

        private static ChannelSelectionState BuildChannelSelectionState(
            IEnumerable<string>? preSelected,
            bool multi,
            IReadOnlyList<ChannelItem> currentItems)
        {
            Dictionary<string, bool>? explicitState = null;
            bool defaultSelected;
            var fromSerializedList = preSelected != null;

            if (preSelected == null)
            {
                if (currentItems.Count > 0)
                {
                    explicitState = currentItems.ToDictionary(
                        c => c.SelectionStateKey(multi), c => c.IsSelected, StringComparer.Ordinal);
                }

                defaultSelected = true;
            }
            else
            {
                var list = preSelected.ToList();
                if (list.Count == 0)
                {
                    defaultSelected = true;
                }
                else
                {
                    explicitState = list.ToDictionary(
                        n => n.Trim(), _ => true, StringComparer.Ordinal);
                    defaultSelected = false;
                }
            }

            var anyComposite = explicitState != null
                               && explicitState.Keys.Any(k =>
                                   FileImportRawNodeBase.TryParseFileChannelSelection(k, out _, out _));

            return new ChannelSelectionState
            {
                ExplicitState = explicitState,
                DefaultSelected = defaultSelected,
                FromSerializedList = fromSerializedList,
                AnyCompositeInList = anyComposite
            };
        }

        private void ApplyChannelItemsFromDiscovery(
            IReadOnlyDictionary<string, List<string>> pathToChannels,
            ChannelSelectionState state,
            bool multi)
        {
            foreach (var old in ChannelItems)
                old.PropertyChanged -= OnChannelItemSelectionChanged;
            ChannelItems.Clear();

            foreach (var p in FilePaths)
            {
                if (!File.Exists(p))
                    continue;

                if (!pathToChannels.TryGetValue(p, out var fileChannels) || fileChannels.Count == 0)
                    continue;

                var fileLabel = Path.GetFileName(p) ?? p;

                foreach (var ch in fileChannels)
                {
                    var stateKey = multi
                        ? $"{fileLabel}{FileImportRawNodeBase.FileChannelSelectionSeparator}{ch}"
                        : ch;

                    bool selected;
                    if (state.ExplicitState == null)
                        selected = state.DefaultSelected;
                    else if (!state.FromSerializedList)
                        selected = state.ExplicitState.TryGetValue(stateKey, out var prev)
                            ? prev
                            : state.DefaultSelected;
                    else if (multi && state.AnyCompositeInList)
                        selected = state.ExplicitState.ContainsKey(stateKey);
                    else
                        selected = state.ExplicitState.ContainsKey(ch);

                    var item = new ChannelItem(p, ch, fileLabel, multi, selected);
                    item.PropertyChanged += OnChannelItemSelectionChanged;
                    ChannelItems.Add(item);
                }
            }

            HasChannels = ChannelItems.Count > 0;
        }

        private async Task RefreshChannelItemsAsync(IEnumerable<string>? preSelected)
        {
            CancelDiscover();
            CancelPreview();

            var myCts = new CancellationTokenSource();
            _discoverCts = myCts;
            var ct = myCts.Token;

            var multi = FilePaths.Count > 1;
            var selection = BuildChannelSelectionState(preSelected, multi, ChannelItems.ToList());
            var paths = FilePaths.Where(File.Exists).ToList();

            List<(string Path, List<string> Channels)> discovered;
            try
            {
                discovered = await Task.Run(
                        () => FileImportChannelDiscovery.DiscoverOrdered(
                            paths,
                            FileImportIoParallelism.EffectiveDegree,
                            ct),
                        ct)
                    .ConfigureAwait(true);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (!ReferenceEquals(_discoverCts, myCts))
                return;

            var pathToChannels = discovered.ToDictionary(
                x => x.Path,
                x => x.Channels,
                StringComparer.OrdinalIgnoreCase);

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                if (!ReferenceEquals(_discoverCts, myCts))
                    return;

                ApplyChannelItemsFromDiscovery(pathToChannels, selection, multi);
                ApplyPlotStyle();
                _refreshPlot();
                _ = LoadAllPreviewsAsync();
            }, DispatcherPriority.Normal);
        }

        // ====== 私有：波形预览（多通道） ======

        private void CancelPreview()
        {
            _previewCts?.Cancel();
            _previewCts = null;
        }

        private void CancelDiscover()
        {
            _discoverCts?.Cancel();
            _discoverCts = null;
        }

        private async Task LoadAllPreviewsAsync()
        {
            CancelPreview();

            var existingFiles = FilePaths.Where(File.Exists).ToList();
            if (existingFiles.Count == 0)
            {
                _signalMap.Clear();
                _plot.Clear();
                ApplyPlotStyle();
                _refreshPlot();
                HasPreviewData = false;
                PreviewStatus = string.Empty;
                return;
            }

            var cts = new CancellationTokenSource();
            _previewCts = cts;

            IsLoadingPreview = true;
            PreviewStatus = string.Empty;

            var plotTheme = ScottPlotStyleHelper.CreateThemeStyleOptions();

            try
            {
                var allFileData = await Task.Run(() =>
                {
                    var n = existingFiles.Count;
                    var slots = new (string FileName, Dictionary<string, (double[] Samples, double Period)> Channels)?[n];
                    var options = new ParallelOptions
                    {
                        MaxDegreeOfParallelism = FileImportIoParallelism.EffectiveDegree,
                        CancellationToken = cts.Token
                    };
                    Parallel.For(0, n, options, i =>
                    {
                        cts.Token.ThrowIfCancellationRequested();
                        var file = existingFiles[i];
                        var channels = BuildAllChannelData(file, cts.Token);
                        if (channels.Count > 0)
                            slots[i] = (Path.GetFileName(file) ?? file, channels);
                    });

                    var result = new List<(string FileName, Dictionary<string, (double[] Samples, double Period)> Channels)>();
                    for (var i = 0; i < n; i++)
                    {
                        if (slots[i] is { } row)
                            result.Add(row);
                    }

                    return result;
                }, cts.Token);

                if (cts.IsCancellationRequested) return;

                _plot.Clear();
                _signalMap.Clear();

                var totalSignals = allFileData.Sum(f => f.Channels.Count);

                if (totalSignals > 0)
                {
                    var multiFile = allFileData.Count > 1;
                    var uiMulti = FilePaths.Count > 1;

                    foreach (var (fileName, channels) in allFileData)
                    {
                        foreach (var (name, (samples, period)) in channels)
                        {
                            var sig = _plot.Add.Signal(samples, period);
                            sig.LegendText = multiFile
                                ? $"{Path.GetFileNameWithoutExtension(fileName)}/{name}"
                                : name;

                            var channelItem = ChannelItems.FirstOrDefault(c =>
                                string.Equals(Path.GetFileName(c.SourceFilePath), fileName,
                                    StringComparison.OrdinalIgnoreCase)
                                && string.Equals(c.Name, name, StringComparison.Ordinal));
                            sig.IsVisible = channelItem?.IsSelected ?? true;

                            var mapKey = channelItem?.SignalMapKey
                                ?? (uiMulti ? $"{fileName}{FileImportRawNodeBase.FileChannelSelectionSeparator}{name}" : name);

                            if (!_signalMap.TryGetValue(mapKey, out var list))
                            {
                                list = new List<Signal>();
                                _signalMap[mapKey] = list;
                            }
                            list.Add(sig);
                        }
                    }

                    _plot.Axes.Bottom.Label.Text = "时间 (s)";
                    _plot.Axes.Left.Label.Text = "幅值";
                    _plot.Axes.AutoScale();

                    if (totalSignals > 1)
                        _plot.ShowLegend();

                    ScottPlotStyleHelper.Apply(_plot, plotTheme);

                    HasPreviewData = true;
                }
                else
                {
                    ApplyPlotStyle(plotTheme);
                    HasPreviewData = false;
                    PreviewStatus = "无法读取波形数据";
                }

                _refreshPlot();
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                if (!cts.IsCancellationRequested)
                {
                    _plot.Clear();
                    ApplyPlotStyle(plotTheme);
                    _refreshPlot();
                    _signalMap.Clear();
                    HasPreviewData = false;
                    PreviewStatus = $"预览失败：{ex.Message}";
                }
            }
            finally
            {
                if (!cts.IsCancellationRequested)
                    IsLoadingPreview = false;
            }
        }

        /// <summary>
        /// 在后台线程导入文件，提取所有信号通道的采样数据。
        /// 返回 channelName → (samples, periodSeconds)。
        /// </summary>
        private static Dictionary<string, (double[] Samples, double Period)> BuildAllChannelData(
            string filePath, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            var importer = NvhFormatImporterRegistry.FindForPath(filePath);
            if (importer == null) return new();

            ct.ThrowIfCancellationRequested();

            var file = importer.Import(filePath);
            ct.ThrowIfCancellationRequested();

            // 优先使用 Signal 组，找不到则用第一个组
            NvhMemoryGroup? group = null;
            file.TryGetGroup(AstraSharedConstants.DataGroups.Signal, out group);
            group ??= file.Groups.Values.FirstOrDefault();
            if (group == null) return new();

            // 直接按组内通道对象遍历，避免 TryExtractAsDoubleArray 在历史上
            // “按名未命中却回落首通道”导致多路数据完全相同、曲线重叠成一条。
            var result = new Dictionary<string, (double[], double)>(StringComparer.Ordinal);
            const double fallbackDt = 1.0 / 48000.0;

            foreach (var kvp in group.Channels)
            {
                ct.ThrowIfCancellationRequested();

                if (string.IsNullOrWhiteSpace(kvp.Key))
                    continue;

                if (!DataImportNvhSampleUtil.TryExtractFromChannel(kvp.Value, out var samples)
                    || samples.Length == 0)
                    continue;

                var dt = DataImportNvhSampleUtil.GetWaveformIncrementOrDefault(kvp.Value, fallbackDt);
                result[kvp.Key] = Downsample(samples, dt);
            }

            return result;
        }

        private static (double[] Data, double Period) Downsample(double[] samples, double dt)
        {
            // ScottPlot Signal 内置 MinMax 渲染，200k 点以内无需降采样
            const int maxPts = 200_000;
            if (samples.Length <= maxPts)
                return (samples, dt);

            var step = (int)Math.Ceiling((double)samples.Length / maxPts);
            var n = samples.Length / step;
            var data = new double[n];
            for (var i = 0; i < n; i++)
                data[i] = samples[i * step];
            return (data, dt * step);
        }

        private void ApplyPlotStyle() =>
            ApplyPlotStyle(ScottPlotStyleHelper.CreateThemeStyleOptions());

        private void ApplyPlotStyle(ScottPlotStyleOptions options) =>
            ScottPlotStyleHelper.Apply(_plot, options);

        // ====== INotifyPropertyChanged ======

        public event PropertyChangedEventHandler? PropertyChanged;

        private void Notify([CallerMemberName] string? prop = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
    }

    // ====== 通道条目 ======

    public sealed class ChannelItem : INotifyPropertyChanged
    {
        private bool _isSelected;

        /// <summary>文件完整路径。</summary>
        public string SourceFilePath { get; }

        /// <summary>文件内通道键（与 NVH 组内名称一致）。</summary>
        public string Name { get; }

        /// <summary>界面展示：多文件时为「文件名 - 通道名」。</summary>
        public string DisplayName { get; }

        /// <summary>与波形曲线字典一致的键（多文件时为 文件名|通道名）。</summary>
        public string SignalMapKey { get; }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected == value) return;
                _isSelected = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
            }
        }

        public ChannelItem(string sourceFilePath, string channelName, string fileNameLabel, bool multiFile,
            bool selected)
        {
            SourceFilePath = sourceFilePath;
            Name = channelName;
            SignalMapKey = multiFile
                ? $"{fileNameLabel}{FileImportRawNodeBase.FileChannelSelectionSeparator}{channelName}"
                : channelName;
            DisplayName = multiFile ? $"{fileNameLabel} - {channelName}" : channelName;
            _isSelected = selected;
        }

        public string SelectionStateKey(bool multiFile) =>
            multiFile
                ? $"{Path.GetFileName(SourceFilePath)}{FileImportRawNodeBase.FileChannelSelectionSeparator}{Name}"
                : Name;

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
