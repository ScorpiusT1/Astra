using Astra.Core.Data;
using Astra.Core.Nodes.Models;
using Astra.Plugins.Algorithms.Helpers;
using Astra.Plugins.Algorithms.APIs;
using Astra.UI.Abstractions.Attributes;
using Astra.UI.Abstractions.Nodes;
using Astra.UI.PropertyEditors;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Runtime.Serialization;

namespace Astra.Plugins.Algorithms.Nodes
{
    /// <summary>
    /// 算法节点基类：从上游多采集写入的 Raw（经 TestDataBus）读取 NVH 数据。
    /// 通道为「设备显示名/通道名」，由上游连线驱动注册表；选项缓存到 <see cref="CachedChannelOptions"/> 并序列化。
    /// 断开上游后保留已选通道，重连后按当前上游选项自动剔除无效项。
    /// </summary>
    public abstract class AlgorithmNodeBase : Node, IHomeTestItemChartEligibleNode, IDesignTimeScalarOutputProvider
    {
        [JsonIgnore]
        private readonly List<IDesignTimeDataSourceInfo> _upstreamSources = new();
        [JsonIgnore]
        private bool _registrySubscribed;

        /// <summary>旧版工程中的采集卡多选，仅用于未选通道时回退为「各卡首通道」。</summary>
        [JsonProperty("DataAcquisitionDeviceNames", NullValueHandling = NullValueHandling.Ignore)]
        private List<string>? _legacyDataAcquisitionDeviceNames;

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
        }

        private void EnsureRegistrySubscription()
        {
            if (_registrySubscribed) return;
            _registrySubscribed = true;
            DesignTimeUpstreamRegistry.SourcesChanged += OnRegistryChanged;
        }

        private void OnRegistryChanged(string nodeId)
        {
            if (nodeId == Id)
            {
                RefreshAndCacheChannelOptions();
                IntersectChannelNamesWithUpstreamOptions();
                OnPropertyChanged(nameof(ChannelNameOptions));
            }
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
            Description = "可多选，格式为「设备名/通道名」。未选时按各采集卡首通道；建议显式选择。断开上游后仍保留已选，重连后自动剔除无效项。")]
        [Editor(typeof(CheckComboBoxPropertyEditor))]
        [ItemsSource(nameof(ChannelNameOptions), DisplayMemberPath = ".")]
        public List<string> ChannelNames
        {
            get => _channelNames;
            set
            {
                _channelNames = value ?? new();
                OnPropertyChanged();
            }
        }

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
            return AlgorithmResultPublisher.SuccessWithMultiChart(context, Id, artifactName, charts, ChartDisplayLayout, tag, message);
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
            return AlgorithmResultPublisher.SuccessWithMultiChartAndScalars(context, Id, artifactName, charts, scalars, ChartDisplayLayout, tag, message);
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
                    bus.PublishScalar(Id, name, value, unit: unit, tag: tag);

                if (chartResult.OutputData != null &&
                    chartResult.OutputData.TryGetValue(NodeUiOutputKeys.ChartArtifactKey, out var keyObj) &&
                    keyObj is string artifactKey &&
                    !string.IsNullOrWhiteSpace(artifactKey) &&
                    bus.TryGet<ChartDisplayPayload>(artifactKey.Trim(), out var payload) &&
                    payload != null)
                {
                    var embedded = ChartDisplayPayload.EmbedScalarsForDisplay(payload, scalars);
                    bus.PublishAlgorithmResult(Id, chartArtifactName, embedded, tag: tag);
                }
            }

            return AlgorithmResultPublisher.AppendScalarOutputs(chartResult, scalars);
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
        }

        public override void OnConnectionAttached(Edge edge, Node? sourceNode, Node? targetNode)
        {
            base.OnConnectionAttached(edge, sourceNode, targetNode);
            if (targetNode?.Id == Id && sourceNode is IDesignTimeDataSourceInfo src && !_upstreamSources.Contains(src))
            {
                _upstreamSources.Add(src);
                DesignTimeUpstreamRegistry.SetSources(Id, _upstreamSources);
                RefreshAndCacheChannelOptions();
                IntersectChannelNamesWithUpstreamOptions();
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

            if (targetNode?.Id == Id && sourceNode is IDesignTimeDataSourceInfo src)
            {
                _upstreamSources.Remove(src);
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
