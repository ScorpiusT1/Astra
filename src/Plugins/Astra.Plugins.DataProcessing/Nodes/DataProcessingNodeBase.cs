using System;
using Astra.Plugins.DataProcessing.Helpers;
using Astra.Core.Nodes.Models;
using Astra.Core.Reporting;
using Astra.UI.Abstractions.Attributes;
using Astra.UI.Abstractions.Nodes;
using Astra.UI.PropertyEditors;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.Serialization;

namespace Astra.Plugins.DataProcessing.Nodes
{
    /// <summary>数据处理节点基类：通道为「设备显示名/通道名」，与算法节点一致由上游注册表驱动。</summary>
    public abstract class DataProcessingNodeBase : Node, IDesignTimeDataSourceInfo, IHomeTestItemChartEligibleNode, IReportWhitelistChartProducerNode
    {
        private List<string> _channelNames = new();
        private readonly List<IDesignTimeDataSourceInfo> _upstreamSources = new();
        [JsonIgnore] private bool _registrySubscribed;

        /// <summary>与算法节点一致：持久化自动通道标题后缀，避免加载后叠层。</summary>
        [JsonProperty("AutoChannelListSuffix", NullValueHandling = NullValueHandling.Ignore)]
        private string? _autoChannelListNameSuffix;

        /// <summary>旧版工程仅保存了采集卡显示名；未选通道时用于回退为「该卡首通道」。</summary>
        [JsonProperty("DataAcquisitionDeviceName", NullValueHandling = NullValueHandling.Ignore)]
        private string? _legacyDataAcquisitionDeviceName;

        [JsonIgnore]
        private string? _deserializeHintDevice;

        /// <summary>旧版 JSON 中的单通道字段（无设备前缀），反序列化后合并到 <see cref="ChannelNames"/>。</summary>
        [JsonProperty("ChannelName", NullValueHandling = NullValueHandling.Ignore)]
        private string? _legacyChannelName;

        protected readonly string DefaultDataProcessingDisplayName;

        protected DataProcessingNodeBase(string nodeTypeKey, string defaultName)
        {
            NodeType = nodeTypeKey;
            Name = defaultName;
            DefaultDataProcessingDisplayName = defaultName ?? "";
        }

        [OnDeserialized]
        private void OnDeserializedCallback(StreamingContext ctx)
        {
            MigrateLegacyChannelNameIfNeeded();
            MigrateLegacyDataAcquisitionDeviceNameIfNeeded();
            if (CachedChannelOptions?.Count > 0 && !string.IsNullOrEmpty(Id))
                DesignTimeUpstreamRegistry.CacheChannelOptions(Id, CachedChannelOptions);

            if (string.IsNullOrEmpty(_autoChannelListNameSuffix))
            {
                var (tracked, recomposed) = NodeNameChannelSuffixHelper.ReconcileAutoSuffixAfterDeserialization(
                    Name, DefaultDataProcessingDisplayName, _channelNames);
                if (tracked != null)
                    _autoChannelListNameSuffix = tracked;
                if (recomposed != null)
                    Name = recomposed;
            }

            SyncDisplayNameFromSelectedChannels();
        }

        private void MigrateLegacyChannelNameIfNeeded()
        {
            if (string.IsNullOrWhiteSpace(_legacyChannelName))
                return;
            if (_channelNames.Count > 0)
            {
                _legacyChannelName = null;
                return;
            }

            var v = _legacyChannelName.Trim();
            _legacyChannelName = null;
            if (string.Equals(v, DataProcessingDesignTimeOptions.UseFirstChannelInGroupLabel, StringComparison.Ordinal))
                return;
            _channelNames.Add(v);
        }

        private void MigrateLegacyDataAcquisitionDeviceNameIfNeeded()
        {
            if (string.IsNullOrWhiteSpace(_legacyDataAcquisitionDeviceName))
                return;

            var dev = _legacyDataAcquisitionDeviceName.Trim();
            _legacyDataAcquisitionDeviceName = null;
            if (string.Equals(dev, DataProcessingDesignTimeOptions.UnselectedLabel, StringComparison.Ordinal))
                return;

            if (_channelNames.Count > 0)
            {
                for (var i = 0; i < _channelNames.Count; i++)
                {
                    var c = _channelNames[i]?.Trim() ?? string.Empty;
                    if (string.IsNullOrEmpty(c))
                        continue;
                    if (!QualifiedChannelHelper.TrySplit(c, out _, out _))
                        _channelNames[i] = $"{dev}/{c}";
                }
            }
            else
                _deserializeHintDevice = dev;
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
                if (!wr.TryGetTarget(out var t) || t is not DataProcessingNodeBase self) return;
                if (changedId != self.Id) return;
                self.RefreshAndCacheChannelOptions();
                self.OnPropertyChanged(nameof(ChannelNameOptions));
                DesignTimeUpstreamRegistry.NotifyUpstreamChannelOptionsChanged(self.Id);
            });
            DesignTimeUpstreamRegistry.RegisterUpstreamChannelOptionsListener(this, producerId =>
            {
                if (!wr.TryGetTarget(out var t) || t is not DataProcessingNodeBase self) return;
                if (!self.IsRegisteredUpstreamNodeId(producerId)) return;
                self.RefreshAndCacheChannelOptions();
                self.IntersectChannelNamesWithUpstreamOptions();
                self.OnPropertyChanged(nameof(ChannelNameOptions));
                DesignTimeUpstreamRegistry.NotifyUpstreamChannelOptionsChanged(self.Id);
            });
            DesignTimeUpstreamRegistry.RegisterDeviceChannelOptionsListener(this, deviceName =>
            {
                if (!wr.TryGetTarget(out var t) || t is not DataProcessingNodeBase self) return;
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
        /// 通道选项持久化缓存。连线后填充；序列化到 JSON，重启后经 <see cref="OnDeserializedCallback"/> 推入静态注册表。
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

                var saved = _channelNames?
                    .Where(c => !string.IsNullOrWhiteSpace(c)).Distinct().ToList();
                return saved?.Count > 0 ? saved : Enumerable.Empty<string>();
            }
        }

        [Display(Name = "选择通道", GroupName = "输入", Order = 0,
            Description = "可多选，格式为「设备名/通道名」。未选时对上游每张卡/每路 Raw 的全部可滤波通道执行滤波（与下游算法多卡多选一致）。删除/断开上游后仍保留已选；上游通道或设备配置变化时再剔除无效项。")]
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
        /// 已选「设备/通道」展开为规格；未选时按上游每一设备一路 Raw，通道占位为 null（表示该路全部通道）。
        /// </summary>
        protected List<(string DeviceName, string? ChannelName)> BuildInputDeviceChannelSpecs()
        {
            var channels = _channelNames?
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
                .Distinct(StringComparer.Ordinal)
                .ToList();

            if (devices.Count == 0 && !string.IsNullOrWhiteSpace(_deserializeHintDevice))
                devices = new List<string> { _deserializeHintDevice!.Trim() };

            if (devices.Count == 0)
                return new List<(string, string?)>();

            return devices.Select(d => (d, (string?)null)).ToList();
        }

        /// <summary>按设备分组后的滤波计划：可对多采集卡/多路 Raw 分别发布滤波结果。</summary>
        protected sealed class FilterDevicePlan
        {
            public required string DeviceDisplayName { get; init; }

            /// <summary>true 时对该设备 Raw 内全部可滤波（float/double）通道执行滤波。</summary>
            public bool FilterAllChannelsInRaw { get; init; }

            /// <summary><see cref="FilterAllChannelsInRaw"/> 为 false 时使用。</summary>
            public List<string> ExplicitChannelNames { get; init; } = new();
        }

        /// <summary>由 <see cref="BuildInputDeviceChannelSpecs"/> 构建多设备滤波计划。</summary>
        protected bool TryBuildFilterDevicePlans(
            out List<FilterDevicePlan> plans,
            [NotNullWhen(false)] out string? error)
        {
            plans = new List<FilterDevicePlan>();
            error = null;

            var specs = BuildInputDeviceChannelSpecs();
            if (specs.Count == 0)
            {
                error = "未指定输入：请连接上游采集或文件导入；若上游无可用设备名，请在「选择通道」中指定「设备名/通道名」。";
                return false;
            }

            var deviceOrder = new List<string>();
            foreach (var (dev, _) in specs)
            {
                if (!deviceOrder.Contains(dev, StringComparer.Ordinal))
                    deviceOrder.Add(dev);
            }

            foreach (var dev in deviceOrder)
            {
                var slice = specs.Where(s => string.Equals(s.DeviceName, dev, StringComparison.Ordinal)).ToList();
                var explicitChannels = slice
                    .Select(s => s.ChannelName)
                    .Where(c => !string.IsNullOrWhiteSpace(c))
                    .Select(c => c!.Trim())
                    .Distinct(StringComparer.Ordinal)
                    .ToList();
                var anyNull = slice.Any(s => s.ChannelName == null);

                if (explicitChannels.Count > 0)
                {
                    plans.Add(new FilterDevicePlan
                    {
                        DeviceDisplayName = dev,
                        FilterAllChannelsInRaw = false,
                        ExplicitChannelNames = explicitChannels
                    });
                }
                else if (anyNull)
                {
                    plans.Add(new FilterDevicePlan
                    {
                        DeviceDisplayName = dev,
                        FilterAllChannelsInRaw = true,
                        ExplicitChannelNames = new List<string>()
                    });
                }
                else
                {
                    error = $"设备「{dev}」未指定通道。";
                    return false;
                }
            }

            return true;
        }

        protected bool TryResolveTargetDeviceForDesignTime([NotNullWhen(true)] out string? deviceDisplayName)
        {
            deviceDisplayName = null;
            if (!TryBuildFilterDevicePlans(out var plans, out _) || plans.Count == 0)
                return false;
            deviceDisplayName = plans[0].DeviceDisplayName;
            return true;
        }

        private void IntersectChannelNamesWithUpstreamOptions()
        {
            var available = DesignTimeUpstreamRegistry.GetAllQualifiedChannelNames(Id).ToHashSet(StringComparer.Ordinal);
            if (available.Count == 0)
                return;

            var list = _channelNames.Where(available.Contains).ToList();
            if (list.Count == _channelNames.Count)
                return;

            _channelNames = list;
            OnPropertyChanged(nameof(ChannelNames));
            SyncDisplayNameFromSelectedChannels();
        }

        public IEnumerable<string> GetAvailableDeviceDisplayNames()
        {
            if (TryBuildFilterDevicePlans(out var plans, out _) && plans.Count > 0)
                return plans.Select(p => p.DeviceDisplayName).Distinct(StringComparer.Ordinal);
            return _upstreamSources.SelectMany(s => s.GetAvailableDeviceDisplayNames()).Distinct();
        }

        public IEnumerable<string> GetAvailableChannelNames(string deviceDisplayName)
        {
            return _upstreamSources.SelectMany(s => s.GetAvailableChannelNames(deviceDisplayName)).Distinct();
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

            if (_upstreamSources.Count == 0)
            {
                _channelNames.Clear();
                _deserializeHintDevice = null;
                CachedChannelOptions?.Clear();
                DesignTimeUpstreamRegistry.ClearChannelOptionsCache(Id);
                OnPropertyChanged(nameof(ChannelNames));
                SyncDisplayNameFromSelectedChannels();
            }

            DesignTimeUpstreamRegistry.SetSources(Id, _upstreamSources);
        }
    }
}
