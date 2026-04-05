using System;
using Astra.Core.Data;
using Astra.Core.Nodes.Models;
using Astra.Core.Reporting;
using Astra.Workflow.AlgorithmChannel.Helpers;
using Astra.Workflow.AlgorithmChannel.APIs;
using Astra.UI.Abstractions.Attributes;
using Astra.UI.Abstractions.Nodes;
using Astra.UI.PropertyEditors;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Runtime.Serialization;

namespace Astra.Workflow.AlgorithmChannel.Nodes
{
    /// <summary>
    /// 算法节点基类：从上游多采集写入的 Raw（经 TestDataBus）读取 NVH 数据。
    /// 通道为「设备显示名/通道名」，由上游连线驱动注册表；选项缓存到 <see cref="CachedChannelOptions"/> 并序列化。
    /// 断开上游或删除上游节点后仍保留已选通道（便于撤销恢复）；在上游通道枚举或设备配置实际变化时再剔除无效项。
    /// 实现 <see cref="IDesignTimeDataSourceInfo"/>，使下游算法/滤波等节点能把本节点当作「通道来源」继续传递（与数据处理节点链式行为一致）。
    /// </summary>
    public abstract class AlgorithmNodeBase : Node, IHomeTestItemChartEligibleNode, IDesignTimeScalarOutputProvider, IReportWhitelistChartProducerNode, IDesignTimeDataSourceInfo
    {
        [JsonIgnore]
        private readonly List<IDesignTimeDataSourceInfo> _upstreamSources = new();
        [JsonIgnore]
        private bool _registrySubscribed;

        [JsonIgnore]
        private string? _autoChannelListNameSuffix;

        /// <summary>旧版工程中的采集卡多选，仅用于未选通道时回退为「各卡首通道」。</summary>
        [JsonProperty("DataAcquisitionDeviceNames", NullValueHandling = NullValueHandling.Ignore)]
        private List<string>? _legacyDataAcquisitionDeviceNames = null;

        protected AlgorithmNodeBase(string nodeTypeKey, string defaultName)
        {
            NodeType = nodeTypeKey;
            Name = defaultName;
        }

        [OnDeserialized]
        private void OnDeserializedCallback(StreamingContext ctx)
        {
            if (CachedChannelOptions?.Count > 0 && !string.IsNullOrEmpty(Id))
                DesignTimeUpstreamRegistry.CacheChannelOptions(Id, CachedChannelOptions);
            SyncDisplayNameFromSelectedChannels();
        }

        private bool IsRegisteredUpstreamNodeId(string producerNodeId) =>
            !string.IsNullOrEmpty(producerNodeId) &&
            _upstreamSources.OfType<Node>().Any(n => n.Id == producerNodeId);

        private void EnsureRegistrySubscription()
        {
            if (_registrySubscribed || string.IsNullOrEmpty(Id)) return;
            _registrySubscribed = true;
            var wr = new WeakReference<object>(this);
            DesignTimeUpstreamRegistry.RegisterOwnSourcesChanged(Id, this, changedId =>
            {
                if (!wr.TryGetTarget(out var t) || t is not AlgorithmNodeBase self) return;
                self.OnRegistryChanged(changedId);
            });
            DesignTimeUpstreamRegistry.RegisterUpstreamChannelOptionsListener(this, producerId =>
            {
                if (!wr.TryGetTarget(out var t) || t is not AlgorithmNodeBase self) return;
                if (!self.IsRegisteredUpstreamNodeId(producerId)) return;
                self.RefreshAndCacheChannelOptions();
                self.IntersectChannelNamesWithUpstreamOptions();
                self.OnPropertyChanged(nameof(ChannelNameOptions));
                DesignTimeUpstreamRegistry.NotifyUpstreamChannelOptionsChanged(self.Id);
            });
            DesignTimeUpstreamRegistry.RegisterDeviceChannelOptionsListener(this, deviceName =>
            {
                if (!wr.TryGetTarget(out var t) || t is not AlgorithmNodeBase self) return;
                if (!DesignTimeUpstreamRegistry.RegisteredUpstreamExposesDevice(self.Id, deviceName)) return;
                self.RefreshAndCacheChannelOptions();
                self.IntersectChannelNamesWithUpstreamOptions();
                self.OnPropertyChanged(nameof(ChannelNameOptions));
                DesignTimeUpstreamRegistry.NotifyUpstreamChannelOptionsChanged(self.Id);
            });
        }

        public override void OnRemovedFromWorkflow()
        {
            if (_registrySubscribed)
            {
                DesignTimeUpstreamRegistry.UnregisterDesignTimeListeners(this);
                _registrySubscribed = false;
            }

            base.OnRemovedFromWorkflow();
        }

        private void OnRegistryChanged(string nodeId)
        {
            if (nodeId != Id)
                return;
            RefreshAndCacheChannelOptions();
            // 拓扑（增删连线/节点）不在这里做 Intersect：否则多上游时删掉其一会把该上游通道从已选列表抹掉，撤销无法恢复。
            OnPropertyChanged(nameof(ChannelNameOptions));
            // 上游拓扑变更后本节点可枚举通道已变，通知下游（曲线卡控等）刷新 ItemsSource
            DesignTimeUpstreamRegistry.NotifyUpstreamChannelOptionsChanged(Id);
        }

        private void RefreshAndCacheChannelOptions()
        {
            var fromRegistry = DesignTimeUpstreamRegistry.GetAllQualifiedChannelNames(Id).ToList();
            if (fromRegistry.Count > 0)
            {
                CachedChannelOptions = fromRegistry;
                DesignTimeUpstreamRegistry.CacheChannelOptions(Id, fromRegistry);
            }
        }

        /// <summary>
        /// 通道选项持久化缓存。连线后由 <see cref="RefreshAndCacheChannelOptions"/> 填充，
        /// 序列化到 JSON，重启后经 [OnDeserialized] 推入静态注册表。
        /// </summary>
        public List<string> CachedChannelOptions { get; set; } = new();

        [JsonIgnore]
        public IEnumerable<string> ChannelNameOptions
        {
            get
            {
                EnsureRegistrySubscription();
                var fromRegistry = DesignTimeUpstreamRegistry.GetAllQualifiedChannelNames(Id).ToList();
                if (fromRegistry.Count > 0)
                {
                    CachedChannelOptions = fromRegistry;
                    DesignTimeUpstreamRegistry.CacheChannelOptions(Id, fromRegistry);
                    return fromRegistry;
                }

                var cached = DesignTimeUpstreamRegistry.GetCachedChannelOptions(Id);
                if (cached.Count > 0)
                    return cached;

                var saved = ChannelNames?.Where(c => !string.IsNullOrWhiteSpace(c)).Distinct().ToList();
                return saved?.Count > 0 ? saved : Enumerable.Empty<string>();
            }
        }

        private List<string> _channelNames = new();

        [Order(1, 0)]
        [Display(Name = "选择通道", GroupName = "输入",
            Description = "可多选，格式为「设备名/通道名」。未选时按各采集卡首通道；建议显式选择。删除/断开上游后仍保留已选；上游通道或设备配置变化时再剔除无效项。")]
        [Editor(typeof(CheckComboBoxPropertyEditor))]
        [ItemsSource(nameof(ChannelNameOptions), DisplayMemberPath = ".")]
        public List<string> ChannelNames
        {
            get => _channelNames;
            set
            {
                _channelNames = value ?? new();
                OnPropertyChanged();
                SyncDisplayNameFromSelectedChannels();
                DesignTimeUpstreamRegistry.NotifyUpstreamChannelOptionsChanged(Id);
            }
        }

        private void SyncDisplayNameFromSelectedChannels()
        {
            var frag = NodeNameChannelSuffixHelper.BuildMultiQualifiedChannelSuffix(_channelNames);
            ApplyAutoChannelSuffixToDisplayName(ref _autoChannelListNameSuffix, frag);
        }

        /// <summary>
        /// 分析时间窗起点（秒，相对波形 t=0）。仅当 <see cref="AnalysisWindowStartSeconds"/> &lt; <see cref="AnalysisWindowEndSeconds"/> 时对样本截取后再计算；否则使用全长数据。
        /// </summary>
        [Order(1, 1)]
        [Display(Name = "分析起始时间(s)", GroupName = "输入",
            Description = "相对波形起点（秒）。仅当起始时间小于结束时间时截取该时段参与算法；起始≥结束时使用全长数据。")]
        public double AnalysisWindowStartSeconds { get; set; }

        [Order(1, 2)]
        [Display(Name = "分析结束时间(s)", GroupName = "输入",
            Description = "相对波形起点（秒），与起始时间组成左闭右闭区间 [起始, 结束] 对应的样本段。")]
        public double AnalysisWindowEndSeconds { get; set; }

        [Order(3, 0)]
        [Display(Name = "多曲线显示模式", GroupName = "图表", Order = 0,
            Description = "单图叠加：所有系列绘在同一坐标系；分子图：每个系列独立子图（多路柱状图、混合类型图表等建议使用分子图）。")]
        public ChartLayoutMode ChartDisplayLayout { get; set; } = ChartLayoutMode.SinglePlot;

        /// <summary>
        /// 发布多系列图表，布局由 <see cref="ChartDisplayLayout"/> 决定。
        /// </summary>
        protected ExecutionResult PublishMultiChart(
            NodeContext context,
            string artifactName,
            IReadOnlyList<(string SeriesName, ChartDisplayPayload Chart)> charts,
            string? tag = null,
            string message = "完成")
        {
            return AlgorithmResultPublisher.SuccessWithMultiChart(context, Id, artifactName, charts, ChartDisplayLayout, tag, message,
                BuildReportArtifactPreviewDictionary(), IncludeInTestReport);
        }

        /// <summary>
        /// 发布多系列图表及标量，布局由 <see cref="ChartDisplayLayout"/> 决定。
        /// </summary>
        protected ExecutionResult PublishMultiChartAndScalars(
            NodeContext context,
            string artifactName,
            IReadOnlyList<(string SeriesName, ChartDisplayPayload Chart)> charts,
            IReadOnlyList<(string Name, double Value, string Unit)> scalars,
            string? tag = null,
            string message = "完成")
        {
            return AlgorithmResultPublisher.SuccessWithMultiChartAndScalars(context, Id, artifactName, charts, scalars, ChartDisplayLayout, tag, message,
                BuildReportArtifactPreviewDictionary(), IncludeInTestReport);
        }

        /// <summary>
        /// 在仅含图表的执行结果上追加标量：发布到测试总线、更新图表产物中的标量角标，并写入 <c>Scalar.*</c> 输出键。
        /// </summary>
        protected ExecutionResult AppendScalarsToChartResult(
            NodeContext context,
            ExecutionResult chartResult,
            IReadOnlyList<(string Name, double Value, string Unit)> scalars,
            string? tag,
            string chartArtifactName)
        {
            if (scalars == null || scalars.Count == 0)
                return chartResult;
            var bus = context.GetDataBus();
            if (bus != null)
            {
                foreach (var (name, value, unit) in scalars)
                    bus.PublishScalar(Id, name, value, unit: unit, tag: tag, includeInTestReport: IncludeInTestReport);

                if (chartResult.OutputData != null &&
                    chartResult.OutputData.TryGetValue(NodeUiOutputKeys.ChartArtifactKey, out var keyObj) &&
                    keyObj is string artifactKey &&
                    !string.IsNullOrWhiteSpace(artifactKey) &&
                    bus.TryGet<ChartDisplayPayload>(artifactKey.Trim(), out var payload) &&
                    payload != null)
                {
                    var embedded = ChartDisplayPayload.EmbedScalarsForDisplay(payload, scalars);
                    bus.PublishAlgorithmResult(Id, chartArtifactName, embedded, tag: tag, parameters: BuildReportArtifactPreviewDictionary(), includeInTestReport: IncludeInTestReport);
                }
            }

            return AlgorithmResultPublisher.AppendScalarOutputs(chartResult, scalars);
        }

        /// <summary>供测试报告标题「设备/通道」段写入总线 Preview（<see cref="ReportArtifactPreviewKeys.DeviceChannel"/>）。</summary>
        protected Dictionary<string, object>? BuildReportArtifactPreviewDictionary()
        {
            var specs = ResolveInputSpecs();
            if (specs.Count == 0)
                return null;

            var parts = new List<string>();
            foreach (var (dev, ch) in specs)
            {
                if (!string.IsNullOrWhiteSpace(ch))
                    parts.Add($"{dev}/{ch}");
                else if (!string.IsNullOrWhiteSpace(dev))
                    parts.Add(dev.Trim());
            }

            if (parts.Count == 0)
                return null;

            return new Dictionary<string, object> { [ReportArtifactPreviewKeys.DeviceChannel] = string.Join("; ", parts) };
        }

        protected List<(string DeviceName, string? ChannelName)> ResolveInputSpecs()
        {
            var channels = ChannelNames?
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .ToList() ?? new List<string>();

            var specs = new List<(string, string?)>();
            foreach (var ch in channels)
            {
                if (QualifiedChannelHelper.TrySplit(ch, out var dev, out var chName))
                    specs.Add((dev, chName));
            }

            if (specs.Count > 0)
                return specs;

            var devices = DesignTimeUpstreamRegistry.GetDeviceNames(Id)
                .Where(d => !string.IsNullOrWhiteSpace(d))
                .Distinct()
                .ToList();

            if (devices.Count == 0 && _legacyDataAcquisitionDeviceNames != null)
            {
                devices = _legacyDataAcquisitionDeviceNames
                    .Where(d => !string.IsNullOrWhiteSpace(d))
                    .Distinct()
                    .ToList();
            }

            if (devices.Count > 0)
                return devices.Select(d => (d, (string?)null)).ToList();

            return new List<(string, string?)>();
        }

        /// <inheritdoc />
        /// <remarks>
        /// 已选「设备/通道」时仅透出所选设备；未选时透传上游数据源设备列表，便于链式节点与采集一致。
        /// </remarks>
        public IEnumerable<string> GetAvailableDeviceDisplayNames()
        {
            var fromSelection = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var c in ChannelNames ?? Enumerable.Empty<string>())
            {
                if (string.IsNullOrWhiteSpace(c))
                    continue;
                if (QualifiedChannelHelper.TrySplit(c.Trim(), out var dev, out _) && !string.IsNullOrWhiteSpace(dev))
                    fromSelection.Add(dev.Trim());
            }

            if (fromSelection.Count > 0)
                return fromSelection;

            return _upstreamSources
                .SelectMany(s => s.GetAvailableDeviceDisplayNames())
                .Distinct(StringComparer.OrdinalIgnoreCase);
        }

        /// <inheritdoc />
        /// <remarks>
        /// 对本节点已选通道：仅返回该设备下被选中的通道名；未选通道时透传上游在该设备下的通道枚举。
        /// </remarks>
        public IEnumerable<string> GetAvailableChannelNames(string deviceDisplayName)
        {
            if (string.IsNullOrWhiteSpace(deviceDisplayName))
                return Enumerable.Empty<string>();

            var dev = deviceDisplayName.Trim();
            var fromSelection = new HashSet<string>(StringComparer.Ordinal);
            foreach (var c in ChannelNames ?? Enumerable.Empty<string>())
            {
                if (string.IsNullOrWhiteSpace(c))
                    continue;
                if (!QualifiedChannelHelper.TrySplit(c.Trim(), out var d, out var ch))
                    continue;
                if (!string.Equals(d.Trim(), dev, StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(ch))
                    continue;
                fromSelection.Add(ch.Trim());
            }

            if (fromSelection.Count > 0)
                return fromSelection;

            return _upstreamSources
                .SelectMany(s => s.GetAvailableChannelNames(deviceDisplayName))
                .Distinct(StringComparer.Ordinal);
        }

        string IDesignTimeScalarOutputProvider.ProviderNodeId => Id;

        /// <inheritdoc />
        public IEnumerable<string> EnumerateDesignTimeScalarInputKeys()
        {
            foreach (var (d, ch) in ResolveInputSpecs())
            {
                var label = string.IsNullOrWhiteSpace(ch) ? d : $"{d}/{ch}";
                foreach (var logical in EnumerateDesignTimeScalarLogicalNames(label))
                {
                    var dataKey = NodeUiOutputKeys.FormatScalarOutputKey(logical);
                    yield return $"{Id}:{dataKey}";
                }
            }
        }

        /// <summary>
        /// 按与运行时一致的通道标签，枚举该节点将写入 <see cref="NodeUiOutputKeys.FormatScalarOutputKey"/> 的逻辑名（无标量输出则保持空序列）。
        /// </summary>
        protected virtual IEnumerable<string> EnumerateDesignTimeScalarLogicalNames(string channelLabel) =>
            Enumerable.Empty<string>();

        private void IntersectChannelNamesWithUpstreamOptions()
        {
            var available = DesignTimeUpstreamRegistry.GetAllQualifiedChannelNames(Id).ToHashSet(StringComparer.Ordinal);
            if (available.Count == 0)
                return;

            var list = _channelNames.Where(c => available.Contains(c)).ToList();
            if (list.Count == _channelNames.Count)
                return;

            _channelNames = list;
            OnPropertyChanged(nameof(ChannelNames));
            SyncDisplayNameFromSelectedChannels();
        }

        public override void OnConnectionAttached(Edge edge, Node? sourceNode, Node? targetNode)
        {
            base.OnConnectionAttached(edge, sourceNode, targetNode);
            if (targetNode?.Id == Id && sourceNode is IDesignTimeDataSourceInfo src && !_upstreamSources.Contains(src))
            {
                _upstreamSources.Add(src);
                EnsureRegistrySubscription();
                DesignTimeUpstreamRegistry.SetSources(Id, _upstreamSources);
            }
        }

        public override void OnConnectionDetached(Edge? edge, Node? sourceNode, Node? targetNode)
        {
            base.OnConnectionDetached(edge, sourceNode, targetNode);
            if (edge == null)
            {
                _upstreamSources.Clear();
                DesignTimeUpstreamRegistry.SetSources(Id, _upstreamSources);
                return;
            }

            if (targetNode?.Id == Id)
            {
                // 与 Limits 的 LimitUpstreamDetachHelper 一致：按边 SourceNodeId 移除，避免 sourceNode 引用与列表项不一致时 Remove 失败（删节点后下游仍枚举到旧通道）。
                if (!string.IsNullOrEmpty(edge.SourceNodeId))
                {
                    var sid = edge.SourceNodeId;
                    _upstreamSources.RemoveAll(s =>
                        s is Node n && string.Equals(n.Id, sid, StringComparison.Ordinal));
                }
                else if (sourceNode is IDesignTimeDataSourceInfo src)
                {
                    _upstreamSources.Remove(src);
                }
            }

            DesignTimeUpstreamRegistry.SetSources(Id, _upstreamSources);
        }

        protected void SetScaleWithReferenceSync(
            ref Scale scaleField,
            Scale newValue,
            ref double referenceField,
            string scalePropertyName,
            string referencePropertyName)
        {
            if (scaleField == newValue)
                return;
            scaleField = newValue;
            referenceField = AlgorithmScaleReferenceDefaults.ForScale(newValue);
            OnPropertyChanged(scalePropertyName);
            OnPropertyChanged(referencePropertyName);
        }
    }
}
