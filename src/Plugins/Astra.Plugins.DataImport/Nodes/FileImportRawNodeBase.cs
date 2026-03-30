using Astra.Core.Constants;
using Astra.Core.Data;
using Astra.Core.Nodes.Models;
using Astra.Plugins.DataImport.Helpers;
using Astra.Plugins.DataImport.Import;
using Astra.UI.Abstractions.Attributes;
using Astra.UI.Abstractions.Nodes;
using Astra.UI.PropertyEditors;
using NVHDataBridge.Models;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace Astra.Plugins.DataImport.Nodes
{
    /// <summary>
    /// 从文件加载 <see cref="NvhMemoryFile"/> 并发布与多采集相同键规则的 Raw（虚拟设备「文件导入」）。
    /// 支持多文件导入：每个文件作为独立虚拟设备发布 Raw，下游节点可分别选择。
    /// </summary>
    public abstract class FileImportRawNodeBase : Node, IRawDataPipelineNode, IMultiRawDataPipelineNode
    {
        private string _channelName = string.Empty;
        private string _virtualDeviceAlias = string.Empty;

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

        /// <summary>新建节点时默认生成「文件导入-」+ 节点 Id 前 8 位，便于多节点并行区分；清空则与默认「文件导入」一致。</summary>
        private static string CreateDefaultVirtualDeviceAliasForId(string? nodeId)
        {
            var compact = (nodeId ?? string.Empty).Replace("-", "");
            if (compact.Length < 8)
                compact = Guid.NewGuid().ToString("N");
            var suffix = compact[..8];
            return $"{AstraSharedConstants.VirtualImportDevices.DisplayName}-{suffix}";
        }

        /// <summary>
        /// 可选的虚拟设备别名。设置后下游节点可在采集卡下拉中选择此别名而非默认的「文件导入」，
        /// 从而在同一工作流中区分多个不同的文件导入源。留空则使用默认名称。
        /// </summary>
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

        /// <summary>主设备显示名（单文件或别名场景）。</summary>
        public string DataAcquisitionDeviceDisplayName =>
            string.IsNullOrEmpty(_virtualDeviceAlias)
                ? AstraSharedConstants.VirtualImportDevices.DisplayName
                : _virtualDeviceAlias;

        /// <summary>多文件时返回每个文件对应的虚拟设备名称列表。</summary>
        IEnumerable<string> IMultiRawDataPipelineNode.DataAcquisitionDeviceDisplayNames =>
            GetPerFileDeviceDisplayNames();

        /// <summary>
        /// 多文件导入列表。从对话框选择一个或多个文件。
        /// </summary>
        [Display(Name = "导入文件", GroupName = "输入", Order = 0,
            Description = "选择一个或多个数据文件")]
        [Editor(typeof(MultiFilePickerPropertyEditor))]
        [FilePicker(FilePickerMode.Open,
            "NVH 数据文件 (*.tdms;*.wav)|*.tdms;*.wav|TDMS 文件 (*.tdms)|*.tdms|WAV 文件 (*.wav)|*.wav|所有文件 (*.*)|*.*",
            Title = "选择数据文件", Multiselect = true)]
        public List<string> SourceFilePaths { get; set; } = new();

        /// <summary>
        /// 兼容旧版单文件路径。当 <see cref="SourceFilePaths"/> 为空时作为回退。
        /// </summary>
        public string SourceFilePath { get; set; } = string.Empty;

        [Display(Name = "通道", GroupName = "输入", Order = 1, Description = "空或默认项表示 Signal 组内首通道")]
        public string ChannelName
        {
            get => string.IsNullOrEmpty(_channelName) ? AstraSharedConstants.DesignTimeLabels.UseFirstChannelInGroup : _channelName;
            set
            {
                var v = value ?? string.Empty;
                if (string.Equals(v, AstraSharedConstants.DesignTimeLabels.UseFirstChannelInGroup, StringComparison.Ordinal))
                    v = string.Empty;
                if (string.Equals(_channelName, v, StringComparison.Ordinal))
                    return;
                _channelName = v;
                OnPropertyChanged();
            }
        }

        protected string? ResolveChannelKey()
        {
            var c = _channelName?.Trim();
            if (string.IsNullOrEmpty(c))
                return null;
            return c;
        }

        protected abstract INvhFormatImporter? GetImporter();

        protected abstract string ChartArtifactName { get; }

        protected abstract string ResultTag { get; }

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
                    return;

                var channelNames = group.Channels.Keys.ToList();
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

            var chKey = ResolveChannelKey();
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
