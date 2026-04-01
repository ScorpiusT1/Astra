using Astra.Core.Nodes.Models;
using Astra.Plugins.Algorithms.Helpers;
using Astra.Plugins.Algorithms.APIs;
using Astra.UI.Abstractions.Attributes;
using Astra.UI.PropertyEditors;
using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations;
using System.Runtime.Serialization;

namespace Astra.Plugins.Algorithms.Nodes
{
    /// <summary>
    /// 算法节点基类：从上游多采集写入的 Raw（经 TestDataBus）读取 NVH 数据。
    /// 设备与通道下拉由上游连线动态驱动，通过静态注册表共享数据（解决属性编辑器使用克隆实例问题）。
    /// 通道选项额外缓存到 <see cref="CachedChannelOptions"/> 并序列化，确保重启后通道下拉框仍有数据。
    /// </summary>
    public abstract class AlgorithmNodeBase : Node
    {
        [JsonIgnore]
        private readonly List<IDesignTimeDataSourceInfo> _upstreamSources = new();
        [JsonIgnore]
        private bool _registrySubscribed;

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
                OnPropertyChanged(nameof(DeviceNameOptions));
                OnPropertyChanged(nameof(ChannelNameOptions));
            }
        }

        private void RefreshAndCacheChannelOptions()
        {
            var devices = DataAcquisitionDeviceNames?.Where(d => !string.IsNullOrWhiteSpace(d)).ToList();
            if (devices == null || devices.Count == 0) return;
            var fromRegistry = DesignTimeUpstreamRegistry.GetChannelNames(Id, devices).ToList();
            if (fromRegistry.Count > 0)
            {
                CachedChannelOptions = fromRegistry;
                DesignTimeUpstreamRegistry.CacheChannelOptions(Id, fromRegistry);
            }
        }

        [JsonIgnore]
        public IEnumerable<string> DeviceNameOptions
        {
            get
            {
                EnsureRegistrySubscription();
                var fromRegistry = DesignTimeUpstreamRegistry.GetDeviceNames(Id).ToList();
                if (fromRegistry.Count > 0)
                    return fromRegistry;

                var saved = DataAcquisitionDeviceNames?.Where(d => !string.IsNullOrWhiteSpace(d)).Distinct().ToList();
                return saved?.Count > 0 ? saved : Enumerable.Empty<string>();
            }
        }

        private List<string> _dataAcquisitionDeviceNames = new();

        [Order(1,0)]
        [Display(Name = "采集卡", GroupName = "输入", Description = "可多选；连线后自动显示上游设备")]
        [Editor(typeof(CheckComboBoxPropertyEditor))]
        [ItemsSource(nameof(DeviceNameOptions), DisplayMemberPath = ".")]
        public List<string> DataAcquisitionDeviceNames
        {
            get => _dataAcquisitionDeviceNames;
            set
            {
                _dataAcquisitionDeviceNames = value ?? new();
                OnPropertyChanged();
                OnPropertyChanged(nameof(ChannelNameOptions));
            }
        }

        /// <summary>
        /// 通道选项持久化缓存。连线后由 <see cref="RefreshAndCacheChannelOptions"/> 填充，
        /// 序列化到 JSON，重启后经 [OnDeserialized] 推入静态注册表。
        /// 不标 [Display] 以避免在属性编辑器中显示。
        /// </summary>
        public List<string> CachedChannelOptions { get; set; } = new();

        [JsonIgnore]
        public IEnumerable<string> ChannelNameOptions
        {
            get
            {
                EnsureRegistrySubscription();
                var devices = DataAcquisitionDeviceNames?.Where(d => !string.IsNullOrWhiteSpace(d)).ToList();
                if (devices == null || devices.Count == 0)
                    return Enumerable.Empty<string>();

                var fromRegistry = DesignTimeUpstreamRegistry.GetChannelNames(Id, devices).ToList();
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

        [Order(1, 1)]
        [Display(Name = "选择通道", GroupName = "输入",  Description = "可多选；格式为「设备名/通道名」，未选则使用各设备首通道")]
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

        protected List<(string DeviceName, string? ChannelName)> ResolveInputSpecs()
        {
            var devices = DataAcquisitionDeviceNames?
                .Where(d => !string.IsNullOrWhiteSpace(d))
                .Distinct()
                .ToList() ?? new List<string>();

            if (devices.Count == 0)
                return new List<(string, string?)>();

            var channels = ChannelNames?
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .ToList() ?? new List<string>();

            if (channels.Count == 0)
                return devices.Select(d => (d, (string?)null)).ToList();

            var specs = new List<(string, string?)>();
            foreach (var ch in channels)
            {
                var slashIdx = ch.IndexOf('/');
                if (slashIdx > 0 && slashIdx < ch.Length - 1)
                {
                    var dev = ch.Substring(0, slashIdx);
                    var chName = ch.Substring(slashIdx + 1);
                    if (devices.Contains(dev, StringComparer.Ordinal))
                        specs.Add((dev, chName));
                }
            }

            if (specs.Count == 0)
                return devices.Select(d => (d, (string?)null)).ToList();

            return specs;
        }

        public override void OnConnectionAttached(Edge edge, Node? sourceNode, Node? targetNode)
        {
            base.OnConnectionAttached(edge, sourceNode, targetNode);
            if (targetNode?.Id == Id && sourceNode is IDesignTimeDataSourceInfo src && !_upstreamSources.Contains(src))
            {
                _upstreamSources.Add(src);
                DesignTimeUpstreamRegistry.SetSources(Id, _upstreamSources);
                RefreshAndCacheChannelOptions();
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

            if (_upstreamSources.Count == 0)
            {
                DataAcquisitionDeviceNames?.Clear();
                ChannelNames?.Clear();
                CachedChannelOptions?.Clear();
                DesignTimeUpstreamRegistry.ClearChannelOptionsCache(Id);
                OnPropertyChanged(nameof(DataAcquisitionDeviceNames));
                OnPropertyChanged(nameof(ChannelNames));
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
