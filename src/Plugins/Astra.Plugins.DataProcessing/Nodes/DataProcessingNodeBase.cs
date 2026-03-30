using Astra.Plugins.DataProcessing.Helpers;
using Astra.Core.Nodes.Models;
using Astra.UI.Abstractions.Attributes;
using Astra.UI.PropertyEditors;
using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations;

namespace Astra.Plugins.DataProcessing.Nodes
{
    /// <summary>数据处理节点基类：与算法插件相同方式绑定上游多采集 Raw。</summary>
    public abstract class DataProcessingNodeBase : Node
    {
        private string _deviceName = string.Empty;
        private string _channelName = string.Empty;

        protected DataProcessingNodeBase(string nodeTypeKey, string defaultName)
        {
            NodeType = nodeTypeKey;
            Name = defaultName;
        }

        [Display(Name = "采集卡", GroupName = "输入", Order = 0, Description = "须与上游多采集或滤波节点配置一致")]
        [Editor(typeof(ComboBoxPropertyEditor))]
        [ItemsSource(typeof(DataProcessingDesignTimeOptions), nameof(DataProcessingDesignTimeOptions.GetAcquisitionDeviceNames), DisplayMemberPath = ".")]
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

        [JsonIgnore]
        public IEnumerable<string> ChannelNameOptions => DataProcessingDesignTimeOptions.GetChannelNamesForDevice(DataAcquisitionDeviceName);

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
    }
}
