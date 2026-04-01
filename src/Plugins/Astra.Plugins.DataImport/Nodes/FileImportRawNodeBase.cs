using Astra.Core.Constants;
using Astra.Core.Data;
using Astra.Core.Nodes.Models;
using Astra.Plugins.DataImport.Helpers;
using Astra.Plugins.DataImport.Import;
using Astra.UI.Abstractions.Attributes;
using Astra.UI.Abstractions.Nodes;
using Astra.UI.PropertyEditors;
using Newtonsoft.Json;
using NVHDataBridge.IO.WAV;
using NVHDataBridge.Models;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace Astra.Plugins.DataImport.Nodes
{
    /// <summary>
    /// 从文件加载 <see cref="NvhMemoryFile"/> 并发布与多采集相同键规则的 Raw（虚拟设备「文件导入」）。
    /// 支持多文件导入：每个文件作为独立虚拟设备发布 Raw，下游节点可分别选择。
    /// </summary>
    public abstract class FileImportRawNodeBase : Node, IRawDataPipelineNode, IMultiRawDataPipelineNode, IDesignTimeDataSourceInfo
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
        public List<string> SelectedChannelNames { get; set; } = new();

        /// <summary>用于预览图表：取第一个已选通道，若未选则为 null（使用首通道）。</summary>
        private string? ResolvePreviewChannelKey()
        {
            var first = SelectedChannelNames?.FirstOrDefault(c => !string.IsNullOrWhiteSpace(c));
            return string.IsNullOrEmpty(first) ? null : first;
        }

        // ===== 通道发现 =====

        private void RefreshDiscoveredChannels()
        {
            var channels = new List<string>();
            var files = GetEffectiveFilePaths();
            foreach (var path in files)
            {
                try
                {
                    channels.AddRange(DiscoverChannelNamesForFile(path));
                }
                catch { }
            }
            _discoveredChannels = channels.Distinct().ToList();
            OnPropertyChanged(nameof(DiscoveredChannelOptions));
        }

        /// <summary>
        /// 轻量级通道名发现。WAV 只读文件头；其他格式做完整导入后提取。
        /// </summary>
        private static List<string> DiscoverChannelNamesForFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return new();

            var ext = Path.GetExtension(path)?.ToLowerInvariant();
            var baseName = Path.GetFileNameWithoutExtension(path) ?? "Data";

            if (ext == ".wav")
                return DiscoverWavChannels(path, baseName);

            var importer = NvhFormatImporterRegistry.FindForPath(path);
            if (importer == null)
                return new() { baseName };

            var file = importer.Import(path);
            return ExtractChannelNamesFromFile(file, baseName);
        }

        private static List<string> DiscoverWavChannels(string path, string baseName)
        {
            using var reader = WavReader.Open(path);
            var count = reader.Channels;
            if (count <= 1)
                return new List<string> { baseName };
            return Enumerable.Range(1, count)
                .Select(i => $"{baseName}_Ch{i}")
                .ToList();
        }

        private static List<string> ExtractChannelNamesFromFile(NvhMemoryFile file, string baseName)
        {
            NvhMemoryGroup? group = null;
            file.TryGetGroup(AstraSharedConstants.DataGroups.Signal, out group);
            group ??= file.Groups.Values.FirstOrDefault();
            if (group == null)
                return new() { baseName };

            var names = group.Channels.Keys.Where(k => !string.IsNullOrEmpty(k)).ToList();
            if (names.Count == 0)
            {
                var count = Math.Max(group.Channels.Count, 1);
                return Enumerable.Range(1, count).Select(i => $"{baseName}_Ch{i}").ToList();
            }
            return names;
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
            var myDevices = GetAvailableDeviceDisplayNames();
            if (!myDevices.Any(d => string.Equals(d, deviceDisplayName, StringComparison.Ordinal)))
                return Enumerable.Empty<string>();

            var selected = SelectedChannelNames?
                .Where(ch => !string.IsNullOrEmpty(ch))
                .ToList();
            if (selected != null && selected.Count > 0)
                return selected;

            if (_discoveredChannels.Count > 0)
                return _discoveredChannels;

            return VirtualDeviceChannelRegistry.GetChannels(deviceDisplayName)
                .Where(ch => !string.IsNullOrEmpty(ch));
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
            NvhMemoryFile? firstFile = null;
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

                firstFile ??= file;

                RegisterChannelsFromFile(file, displayName);

                bus.PublishRawData(
                    producerNodeId: Id,
                    artifactName: $"{deviceId}:raw",
                    rawData: file,
                    displayName: $"{displayName}-Raw",
                    deviceId: deviceId);

                importedCount++;
            }

            // 执行后刷新一次通道发现（确保与实际导入一致）
            RefreshDiscoveredChannels();

            var chKey = ResolvePreviewChannelKey();
            if (firstFile != null &&
                DataImportNvhSampleUtil.TryExtractAsDoubleArray(
                    firstFile, AstraSharedConstants.DataGroups.Signal, chKey, out var samples) &&
                samples.Length > 0)
            {
                if (!DataImportNvhSampleUtil.TryGetWaveformIncrement(
                        firstFile, AstraSharedConstants.DataGroups.Signal, chKey, out var dt) || dt <= 0)
                    dt = 1.0 / 48000.0;

                var n = samples.Length;
                var t = new double[n];
                for (var k = 0; k < n; k++)
                    t[k] = k * dt;
                var chart = ChartDisplayPayloadFactory.XYLine(t, samples, "时间 (s)", "幅值");

                var msg = importedCount > 1
                    ? $"已导入 {importedCount} 个文件并发布 Raw"
                    : "完成";
                return Task.FromResult(DataImportResultPublisher.SuccessWithChart(
                    context, Id, ChartArtifactName, chart, tag: ResultTag, message: msg));
            }

            return Task.FromResult(ExecutionResult.Successful(
                importedCount > 1
                    ? $"已导入 {importedCount} 个文件并发布 Raw（未生成预览图表）。"
                    : "已发布 Raw（未生成预览图表）。"));
        }
    }
}
