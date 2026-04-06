using Astra.Core.Constants;
using Astra.Core.Data;
using Astra.Core.Nodes.Models;
using Astra.Core.Reporting;
using Astra.Plugins.DataImport.Helpers;
using Astra.Plugins.DataImport.Import;
using Astra.Plugins.DataImport.Views;
using Astra.UI.Abstractions.Attributes;
using Astra.UI.Abstractions.Nodes;
using Astra.UI.PropertyEditors;
using Newtonsoft.Json;
using NVHDataBridge.Models;
using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace Astra.Plugins.DataImport.Nodes
{
    /// <summary>
    /// 从文件加载 <see cref="NvhMemoryFile"/> 并发布与多采集相同键规则的 Raw（虚拟设备「文件导入」）。
    /// 支持多文件导入：每个文件作为独立虚拟设备发布 Raw，下游节点可分别选择。
    /// </summary>
    [NodePropertyEditor(typeof(FileImportNodePropertyView))]
    public abstract class FileImportRawNodeBase : Node, IRawDataPipelineNode, IMultiRawDataPipelineNode, IDesignTimeDataSourceInfo, IHomeTestItemChartEligibleNode, IReportWhitelistChartProducerNode
    {
        private string _virtualDeviceAlias = string.Empty;
        [JsonIgnore]
        private List<string> _discoveredChannels = new();

        protected FileImportRawNodeBase(string nodeTypeKey, string defaultName)
        {
            NodeType = nodeTypeKey;
            Name = defaultName;
        }

        /// <inheritdoc />
        public override void OnPlacedFromToolbox()
        {
            base.OnPlacedFromToolbox();
            if (string.IsNullOrWhiteSpace(_virtualDeviceAlias))
                VirtualDeviceAlias = CreateDefaultVirtualDeviceAliasForId(Id);
        }

        private static string CreateDefaultVirtualDeviceAliasForId(string? nodeId)
        {
            var compact = (nodeId ?? string.Empty).Replace("-", "");
            if (compact.Length < 8)
                compact = Guid.NewGuid().ToString("N");
            var suffix = compact[..8];
            return $"{AstraSharedConstants.VirtualImportDevices.DisplayName}-{suffix}";
        }

        [Display(Name = "虚拟设备别名", GroupName = "输入", Order = -1,
            Description = "新建节点默认自动生成唯一别名；可修改。清空则下游显示为默认「文件导入」")]
        public string VirtualDeviceAlias
        {
            get => _virtualDeviceAlias;
            set
            {
                var v = value?.Trim() ?? string.Empty;
                if (string.Equals(_virtualDeviceAlias, v, StringComparison.Ordinal))
                    return;

                if (!string.IsNullOrEmpty(_virtualDeviceAlias))
                {
                    VirtualDeviceChannelRegistry.RemoveAlias(_virtualDeviceAlias);
                    VirtualDeviceChannelRegistry.Clear(_virtualDeviceAlias);
                }

                _virtualDeviceAlias = v;

                if (!string.IsNullOrEmpty(_virtualDeviceAlias))
                {
                    VirtualDeviceChannelRegistry.RegisterAlias(
                        _virtualDeviceAlias,
                        AstraSharedConstants.VirtualImportDevices.DeviceId);
                }

                OnPropertyChanged();
                OnPropertyChanged(nameof(DataAcquisitionDeviceDisplayName));
                DesignTimeUpstreamRegistry.NotifyUpstreamChannelOptionsChanged(Id);
            }
        }

        public string DataAcquisitionDeviceDisplayName =>
            string.IsNullOrEmpty(_virtualDeviceAlias)
                ? AstraSharedConstants.VirtualImportDevices.DisplayName
                : _virtualDeviceAlias;

        IEnumerable<string> IMultiRawDataPipelineNode.DataAcquisitionDeviceDisplayNames =>
            GetPerFileDeviceDisplayNames();

        // ===== 文件选择 =====

        private List<string> _sourceFilePaths = new();

        [Display(Name = "导入文件", GroupName = "输入", Order = 0,
            Description = "选择一个或多个数据文件")]
        [Editor(typeof(MultiFilePickerPropertyEditor))]
        [FilePicker(FilePickerMode.Open,
            "NVH 数据文件 (*.tdms;*.wav)|*.tdms;*.wav|TDMS 文件 (*.tdms)|*.tdms|WAV 文件 (*.wav)|*.wav|所有文件 (*.*)|*.*",
            Title = "选择数据文件", Multiselect = true)]
        public List<string> SourceFilePaths
        {
            get => _sourceFilePaths;
            set
            {
                _sourceFilePaths = value ?? new();
                OnPropertyChanged();
                RefreshDiscoveredChannels();
            }
        }

        public string SourceFilePath { get; set; } = string.Empty;

        // ===== 通道多选 =====

        [JsonIgnore]
        public IEnumerable<string> DiscoveredChannelOptions
        {
            get
            {
                if (_discoveredChannels.Count == 0)
                    RefreshDiscoveredChannels();
                return _discoveredChannels;
            }
        }

        [Display(Name = "通道", GroupName = "输入", Order = 1,
            Description = "选择要传递给下游的通道；不选则传递全部通道")]
        [Editor(typeof(CheckComboBoxPropertyEditor))]
        [ItemsSource(nameof(DiscoveredChannelOptions), DisplayMemberPath = ".")]
        public List<string> SelectedChannelNames
        {
            get => _selectedChannelNames;
            set
            {
                value ??= new();
                if (_selectedChannelNames.SequenceEqual(value, StringComparer.Ordinal))
                    return;
                _selectedChannelNames = value;
                OnPropertyChanged();
                DesignTimeUpstreamRegistry.NotifyUpstreamChannelOptionsChanged(Id);
            }
        }

        private List<string> _selectedChannelNames = new();

        /// <summary>多文件时勾选项持久化为「文件名|通道键」；单文件仍为纯通道键。文件名取自 <see cref="Path.GetFileName(string)"/>。</summary>
        internal const char FileChannelSelectionSeparator = '|';

        internal static bool TryParseFileChannelSelection(string? raw, out string fileNamePart, out string channelPart)
        {
            fileNamePart = string.Empty;
            channelPart = string.Empty;
            var s = raw?.Trim();
            if (string.IsNullOrEmpty(s))
                return false;
            var i = s.IndexOf(FileChannelSelectionSeparator);
            if (i <= 0 || i >= s.Length - 1)
                return false;
            fileNamePart = s[..i].Trim();
            channelPart = s[(i + 1)..].Trim();
            return fileNamePart.Length > 0 && channelPart.Length > 0;
        }

        /// <summary>
        /// 主页预览：本文件 Signal 组内应绘制的通道键列表（顺序与勾选列表一致，去重）。
        /// 未勾选时返回单元素 null（与 <see cref="DataImportNvhSampleUtil.TryExtractAsDoubleArray"/> 一致，回落为组内首通道）。
        /// </summary>
        private List<string?> ResolvePreviewChannelKeysForFile(string sourcePath, NvhMemoryFile file)
        {
            if (!file.TryGetGroup(AstraSharedConstants.DataGroups.Signal, out var g) || g == null)
                g = file.Groups.Values.FirstOrDefault();
            if (g == null || g.Channels.Count == 0)
                return new List<string?>();

            var thisFileName = Path.GetFileName(sourcePath);
            var selected = SelectedChannelNames?
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s.Trim())
                .ToList();
            if (selected == null || selected.Count == 0)
                return new List<string?> { null };

            var added = new HashSet<string>(StringComparer.Ordinal);
            var keys = new List<string?>();
            foreach (var name in selected)
            {
                string? chKey = null;
                if (TryParseFileChannelSelection(name, out var fn, out var ch))
                {
                    if (!string.Equals(fn, thisFileName, StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (g.Channels.ContainsKey(ch))
                        chKey = ch;
                }
                else if (g.Channels.ContainsKey(name))
                    chKey = name;

                if (chKey != null && added.Add(chKey))
                    keys.Add(chKey);
            }

            // 与旧版 ResolvePreviewChannelKeyForFile 一致：勾选了项但本文件无匹配时仍预览首通道
            if (keys.Count == 0)
                keys.Add(null);

            return keys;
        }

        private static string BuildPreviewSeriesName(
            IReadOnlyList<string> allFilePaths,
            string sourcePath,
            string displayName,
            string? channelKeyOrNull,
            NvhMemoryFile file)
        {
            string? labelKey = channelKeyOrNull;
            if (labelKey == null)
            {
                if (!file.TryGetGroup(AstraSharedConstants.DataGroups.Signal, out var g) || g == null)
                    g = file.Groups.Values.FirstOrDefault();
                labelKey = g?.Channels.Keys.FirstOrDefault();
            }

            var stem = Path.GetFileNameWithoutExtension(sourcePath);
            if (allFilePaths.Count > 1)
                return string.IsNullOrEmpty(labelKey) ? displayName : $"{stem}/{labelKey}";
            return string.IsNullOrEmpty(labelKey) ? displayName : labelKey;
        }

        private static bool ChannelMatchesSavedSelection(string? selectionEntry, string channelKey, string currentFileName)
        {
            var s = selectionEntry?.Trim();
            if (string.IsNullOrEmpty(s))
                return false;
            if (TryParseFileChannelSelection(s, out var fn, out var ch))
            {
                return string.Equals(fn, currentFileName, StringComparison.OrdinalIgnoreCase)
                       && string.Equals(ch, channelKey, StringComparison.Ordinal);
            }
            return string.Equals(s, channelKey, StringComparison.Ordinal);
        }

        // ===== 通道发现 =====

        private void RefreshDiscoveredChannels()
        {
            var files = GetEffectiveFilePaths().ToList();
            if (files.Count == 0)
            {
                _discoveredChannels = new List<string>();
            }
            else
            {
                try
                {
                    var ordered = FileImportChannelDiscovery.DiscoverOrdered(
                        files,
                        FileImportIoParallelism.EffectiveDegree,
                        CancellationToken.None);
                    _discoveredChannels = ordered
                        .SelectMany(t => t.Channels)
                        .Distinct()
                        .ToList();
                }
                catch
                {
                    _discoveredChannels = new List<string>();
                }
            }

            OnPropertyChanged(nameof(DiscoveredChannelOptions));
            DesignTimeUpstreamRegistry.NotifyUpstreamChannelOptionsChanged(Id);
        }

        // ===== 子类扩展点 =====

        protected abstract INvhFormatImporter? GetImporter();
        protected abstract string ChartArtifactName { get; }
        protected abstract string ResultTag { get; }

        // ===== 内部辅助 =====

        private List<string> GetEffectiveFilePaths()
        {
            var list = SourceFilePaths?
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Select(p => p.Trim())
                .ToList() ?? new List<string>();

            if (list.Count == 0 && !string.IsNullOrWhiteSpace(SourceFilePath))
                list.Add(SourceFilePath.Trim());

            return list;
        }

        private string GetBaseDeviceName()
        {
            return string.IsNullOrEmpty(_virtualDeviceAlias)
                ? AstraSharedConstants.VirtualImportDevices.DisplayName
                : _virtualDeviceAlias;
        }

        private List<string> GetPerFileDeviceDisplayNames()
        {
            var files = GetEffectiveFilePaths();
            var baseName = GetBaseDeviceName();

            if (files.Count <= 1)
                return new List<string> { baseName };

            return files.Select(f =>
                $"{baseName} [{Path.GetFileNameWithoutExtension(f)}]"
            ).ToList();
        }

        private string GetDeviceIdForFile(int index)
        {
            var baseId = AstraSharedConstants.VirtualImportDevices.DeviceId;
            if (!string.IsNullOrEmpty(_virtualDeviceAlias) &&
                VirtualDeviceChannelRegistry.TryResolveDeviceId(_virtualDeviceAlias, out var aliasId))
                baseId = aliasId;

            if (GetEffectiveFilePaths().Count <= 1)
                return baseId;

            return $"{baseId}-{index}";
        }

        private void RegisterChannelsFromFile(NvhMemoryFile file, string displayName)
        {
            try
            {
                if (!file.TryGetGroup(AstraSharedConstants.DataGroups.Signal, out var group) || group == null)
                    group = file.Groups.Values.FirstOrDefault();
                if (group == null)
                    return;

                var channelNames = group.Channels.Keys.ToList();

                if (channelNames.Count == 0)
                {
                    var count = group.Channels.Count;
                    channelNames = Enumerable.Range(1, count > 0 ? count : 1)
                        .Select(i => $"Ch{i}")
                        .ToList();
                }

                VirtualDeviceChannelRegistry.Register(displayName, channelNames);
            }
            catch
            {
            }
        }

        private void RegisterMultiFileAliases(List<string> displayNames)
        {
            var baseDeviceId = AstraSharedConstants.VirtualImportDevices.DeviceId;
            if (!string.IsNullOrEmpty(_virtualDeviceAlias) &&
                VirtualDeviceChannelRegistry.TryResolveDeviceId(_virtualDeviceAlias, out var aliasId))
                baseDeviceId = aliasId;

            for (var i = 0; i < displayNames.Count; i++)
            {
                var dn = displayNames[i];
                var deviceId = displayNames.Count <= 1 ? baseDeviceId : $"{baseDeviceId}-{i}";
                VirtualDeviceChannelRegistry.RegisterAlias(dn, deviceId);
            }
        }

        // ===== IDesignTimeDataSourceInfo =====

        public IEnumerable<string> GetAvailableDeviceDisplayNames()
        {
            var names = GetPerFileDeviceDisplayNames();
            return names.Count > 0 ? names : new List<string> { DataAcquisitionDeviceDisplayName };
        }

        public IEnumerable<string> GetAvailableChannelNames(string deviceDisplayName)
        {
            if (string.IsNullOrWhiteSpace(deviceDisplayName))
                return Enumerable.Empty<string>();

            // 仅当请求的设备名属于本节点时才返回通道，避免"串台"
            var myDevices = GetAvailableDeviceDisplayNames().ToList();
            if (!myDevices.Any(d => string.Equals(d, deviceDisplayName, StringComparison.Ordinal)))
                return Enumerable.Empty<string>();

            var files = GetEffectiveFilePaths();
            var perFileDevices = GetPerFileDeviceDisplayNames().ToList();

            // 多文件时每个虚拟设备只应暴露对应文件的通道；若用全量 _discoveredChannels（多文件并集），
            // DesignTimeUpstreamRegistry 会对「设备 × 通道」组合，造成笛卡尔积式重复项。
            List<string> baseChannels;
            var resolvedFileIndex = -1;
            if (files.Count > 1 && perFileDevices.Count == files.Count)
            {
                resolvedFileIndex = perFileDevices.FindIndex(d =>
                    string.Equals(d, deviceDisplayName, StringComparison.Ordinal));
                baseChannels = resolvedFileIndex >= 0 && resolvedFileIndex < files.Count
                    ? ResolveChannelsForFileAtIndex(files[resolvedFileIndex], deviceDisplayName)
                    : new List<string>();
            }
            else
            {
                baseChannels = VirtualDeviceChannelRegistry.GetChannels(deviceDisplayName)
                    .Where(ch => !string.IsNullOrEmpty(ch))
                    .Distinct(StringComparer.Ordinal)
                    .ToList();

                if (baseChannels.Count == 0)
                {
                    if (_discoveredChannels.Count > 0)
                        baseChannels = _discoveredChannels.ToList();
                    else if (files.Count == 1 && File.Exists(files[0]))
                        baseChannels = FileImportChannelDiscovery.DiscoverChannelNames(files[0]);
                }
            }

            var selected = SelectedChannelNames?
                .Where(ch => !string.IsNullOrEmpty(ch))
                .ToList();
            if (selected != null && selected.Count > 0)
            {
                var currentFileName = resolvedFileIndex >= 0 && resolvedFileIndex < files.Count
                    ? Path.GetFileName(files[resolvedFileIndex])
                    : (files.Count == 1 ? Path.GetFileName(files[0]) : string.Empty);

                return baseChannels.Where(ch =>
                        selected.Any(sel => ChannelMatchesSavedSelection(sel, ch, currentFileName)))
                    .ToList();
            }

            return baseChannels;
        }

        /// <summary>优先使用执行后写入注册表的通道，否则对该文件做轻量发现。</summary>
        private List<string> ResolveChannelsForFileAtIndex(string path, string deviceDisplayName)
        {
            var fromReg = VirtualDeviceChannelRegistry.GetChannels(deviceDisplayName)
                .Where(ch => !string.IsNullOrEmpty(ch))
                .Distinct(StringComparer.Ordinal)
                .ToList();
            if (fromReg.Count > 0)
                return fromReg;

            return File.Exists(path)
                ? FileImportChannelDiscovery.DiscoverChannelNames(path)
                : new List<string>();
        }

        // ===== 生命周期 =====

        public override void OnRemovedFromWorkflow()
        {
            base.OnRemovedFromWorkflow();
            if (!string.IsNullOrEmpty(_virtualDeviceAlias))
            {
                VirtualDeviceChannelRegistry.RemoveAlias(_virtualDeviceAlias);
                VirtualDeviceChannelRegistry.Clear(_virtualDeviceAlias);
            }
        }

        // ===== 执行 =====

        protected override Task<ExecutionResult> ExecuteCoreAsync(NodeContext context, CancellationToken cancellationToken)
        {
            var files = GetEffectiveFilePaths();
            if (files.Count == 0)
                return Task.FromResult(ExecutionResult.Failed("请指定至少一个有效的文件路径。"));

            var missing = files.Where(f => !File.Exists(f)).ToList();
            if (missing.Count > 0)
                return Task.FromResult(ExecutionResult.Failed(
                    $"以下文件不存在：{string.Join("、", missing.Select(Path.GetFileName))}"));

            var bus = context.GetDataBus();
            if (bus == null)
                return Task.FromResult(ExecutionResult.Failed("测试数据总线不可用。"));

            var deviceDisplayNames = GetPerFileDeviceDisplayNames();
            RegisterMultiFileAliases(deviceDisplayNames);

            var subclassImporter = GetImporter();
            var importedForPreview = new List<(string DisplayName, NvhMemoryFile File)>();
            int importedCount = 0;

            for (var i = 0; i < files.Count; i++)
            {
                var path = files[i];
                var displayName = deviceDisplayNames[i];
                var deviceId = GetDeviceIdForFile(i);

                var importer = (subclassImporter?.CanImport(path) == true)
                    ? subclassImporter
                    : NvhFormatImporterRegistry.FindForPath(path);

                if (importer == null)
                    return Task.FromResult(ExecutionResult.Failed(
                        $"文件 \"{Path.GetFileName(path)}\" 格式不受支持。"));

                NvhMemoryFile file;
                try
                {
                    file = importer.Import(path);
                }
                catch (Exception ex)
                {
                    return Task.FromResult(ExecutionResult.Failed(
                        $"导入 \"{Path.GetFileName(path)}\" 失败: {ex.Message}", ex));
                }

                importedForPreview.Add((displayName, file));

                RegisterChannelsFromFile(file, displayName);

                bus.PublishRawData(
                    producerNodeId: Id,
                    artifactName: $"{deviceId}:raw",
                    rawData: file,
                    displayName: $"{displayName}-Raw",
                    deviceId: deviceId,
                    includeInTestReport: IncludeInTestReport);

                importedCount++;
            }

            // 执行后刷新一次通道发现（确保与实际导入一致）
            RefreshDiscoveredChannels();

            var previewSeries = new List<(string SeriesName, ChartDisplayPayload Chart)>();
            for (var pi = 0; pi < importedForPreview.Count; pi++)
            {
                var (displayName, file) = importedForPreview[pi];
                var sourcePath = files[pi];
                foreach (var chKey in ResolvePreviewChannelKeysForFile(sourcePath, file))
                {
                    if (!DataImportNvhSampleUtil.TryExtractAsDoubleArray(
                            file, AstraSharedConstants.DataGroups.Signal, chKey, out var samples) ||
                        samples.Length == 0)
                        continue;

                    var dt = 1.0 / 48000.0;
                    if (DataImportNvhSampleUtil.TryGetWaveformIncrement(
                            file, AstraSharedConstants.DataGroups.Signal, chKey, out var inc) && inc > 0)
                        dt = inc;

                    var period = dt > 0 ? dt : 1.0 / 48000.0;
                    var chart = ChartDisplayPayloadFactory.Signal1D(
                        samples,
                        samplePeriod: period,
                        bottomAxisLabel: "时间 (s)",
                        leftAxisLabel: "幅值");
                    var seriesName = BuildPreviewSeriesName(files, sourcePath, displayName, chKey, file);
                    previewSeries.Add((seriesName, chart));
                }
            }

            if (previewSeries.Count > 0)
            {
                var msg = importedCount > 1
                    ? $"已导入 {importedCount} 个文件并发布 Raw"
                    : "完成";
                return Task.FromResult(DataImportResultPublisher.SuccessWithMultiChart(
                    context, Id, ChartArtifactName, previewSeries, tag: ResultTag, message: msg, includeInTestReport: IncludeInTestReport));
            }

            return Task.FromResult(ExecutionResult.Successful(
                importedCount > 1
                    ? $"已导入 {importedCount} 个文件并发布 Raw（未生成预览图表）。"
                    : "已发布 Raw（未生成预览图表）。"));
        }
    }
}
