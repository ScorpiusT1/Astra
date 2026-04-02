using Astra.Plugins.DataProcessing.Helpers;
using Astra.Core.Nodes.Models;
using Astra.Core.Reporting;
using Astra.Plugins.DataAcquisition.Providers;
using Astra.UI.Abstractions.Nodes;
using Astra.UI.Abstractions.Attributes;
using Astra.UI.PropertyEditors;
using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Runtime.Serialization;

namespace Astra.Plugins.DataProcessing.Nodes
{
    /// <summary>数据处理节点基类：与算法插件相同方式绑定上游多采集 Raw。</summary>
    public abstract class DataProcessingNodeBase : Node, IDesignTimeDataSourceInfo, IHomeTestItemChartEligibleNode, IReportWhitelistChartProducerNode
    {
        private string _deviceName = string.Empty;
        private string _channelName = string.Empty;
        private readonly List<IDesignTimeDataSourceInfo> _upstreamSources = new();
        [JsonIgnore] private bool _registrySubscribed;

        protected DataProcessingNodeBase(string nodeTypeKey, string defaultName)
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
            DesignTimeUpstreamRegistry.SourcesChanged += id =>
            {
                if (id == Id)
                {
                    RefreshAndCacheChannelOptions();
                    OnPropertyChanged(nameof(DeviceNameOptions));
                    OnPropertyChanged(nameof(ChannelNameOptions));
                }
            };
        }

        private void RefreshAndCacheChannelOptions()
        {
            if (string.IsNullOrEmpty(_deviceName)) return;
            var channels = DesignTimeUpstreamRegistry.GetChannelNamesForDevice(Id, _deviceName).ToList();
            if (channels.Count > 0)
            {
                CachedChannelOptions = channels;
                DesignTimeUpstreamRegistry.CacheChannelOptions(Id, channels);
            }
        }

        [JsonIgnore]
        public IEnumerable<string> DeviceNameOptions
        {
            get
            {
                EnsureRegistrySubscription();
                var list = new List<string> { DataProcessingDesignTimeOptions.UnselectedLabel };
                var fromRegistry = DesignTimeUpstreamRegistry.GetDeviceNames(Id).ToList();
                if (fromRegistry.Count > 0)
                {
                    list.AddRange(fromRegistry);
                }
                else if (!string.IsNullOrEmpty(_deviceName))
                {
                    list.Add(_deviceName);
                }
                return list;
            }
        }

        [Display(Name = "采集卡", GroupName = "输入", Order = 0, Description = "须与上游多采集或滤波节点配置一致")]
        [Editor(typeof(ComboBoxPropertyEditor))]
        [ItemsSource(nameof(DeviceNameOptions), DisplayMemberPath = ".")]
        public string DataAcquisitionDeviceName
        {
            get => string.IsNullOrEmpty(_deviceName) ? DataProcessingDesignTimeOptions.UnselectedLabel : _deviceName;
            set
            {
                var v = value ?? string.Empty;
                if (string.Equals(v, DataProcessingDesignTimeOptions.UnselectedLabel, StringComparison.Ordinal))
                    v = string.Empty;
                if (string.Equals(_deviceName, v, StringComparison.Ordinal))
                    return;
                _deviceName = v;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ChannelNameOptions));
            }
        }

        public List<string> CachedChannelOptions { get; set; } = new();

        [JsonIgnore]
        public IEnumerable<string> ChannelNameOptions
        {
            get
            {
                EnsureRegistrySubscription();
                if (string.IsNullOrEmpty(_deviceName))
                    return new[] { DataProcessingDesignTimeOptions.UnselectedLabel };

                var channels = DesignTimeUpstreamRegistry.GetChannelNamesForDevice(Id, _deviceName).ToList();
                if (channels.Count > 0)
                {
                    var opts = new List<string>(channels);
                    CachedChannelOptions = opts;
                    DesignTimeUpstreamRegistry.CacheChannelOptions(Id, opts);
                    opts.Insert(0, DataProcessingDesignTimeOptions.UseFirstChannelInGroupLabel);
                    return opts;
                }

                var cached = DesignTimeUpstreamRegistry.GetCachedChannelOptions(Id);
                if (cached.Count > 0)
                {
                    var opts = new List<string>(cached);
                    opts.Insert(0, DataProcessingDesignTimeOptions.UseFirstChannelInGroupLabel);
                    return opts;
                }

                if (!string.IsNullOrEmpty(_channelName))
                    return new[] { DataProcessingDesignTimeOptions.UseFirstChannelInGroupLabel, _channelName };

                return DataProcessingDesignTimeOptions.GetChannelNamesForDevice(DataAcquisitionDeviceName);
            }
        }

        [Display(Name = "通道", GroupName = "输入", Order = 1, Description = "空或默认项表示 Signal 组内首通道")]
        [Editor(typeof(ComboBoxPropertyEditor))]
        [ItemsSource(nameof(ChannelNameOptions), DisplayMemberPath = ".")]
        public string ChannelName
        {
            get => string.IsNullOrEmpty(_channelName) ? DataProcessingDesignTimeOptions.UseFirstChannelInGroupLabel : _channelName;
            set
            {
                var v = value ?? string.Empty;
                if (string.Equals(v, DataProcessingDesignTimeOptions.UseFirstChannelInGroupLabel, StringComparison.Ordinal))
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

        public IEnumerable<string> GetAvailableDeviceDisplayNames()
        {
            var d = _deviceName?.Trim();
            if (!string.IsNullOrEmpty(d))
                return new[] { d };
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
                _upstreamSources.Remove(src);

            if (_upstreamSources.Count == 0)
            {
                _deviceName = string.Empty;
                _channelName = string.Empty;
                CachedChannelOptions?.Clear();
                DesignTimeUpstreamRegistry.ClearChannelOptionsCache(Id);
                OnPropertyChanged(nameof(DataAcquisitionDeviceName));
                OnPropertyChanged(nameof(ChannelName));
            }
            DesignTimeUpstreamRegistry.SetSources(Id, _upstreamSources);
        }
    }
}
