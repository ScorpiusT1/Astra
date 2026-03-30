using Astra.Core.Nodes.Models;
using Astra.Plugins.Algorithms.Helpers;
using Astra.Plugins.Algorithms.APIs;
using Astra.UI.Abstractions.Attributes;
using Astra.UI.PropertyEditors;
using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations;

namespace Astra.Plugins.Algorithms.Nodes
{
    /// <summary>
    /// 算法节点基类：从上游多采集写入的 Raw（经 TestDataBus）读取 NVH 数据。
    /// </summary>
    public abstract class AlgorithmNodeBase : Node
    {
        private string _deviceName = string.Empty;
        private string _channelName = string.Empty;

        protected AlgorithmNodeBase(string nodeTypeKey, string defaultName)
        {
            NodeType = nodeTypeKey;
            Name = defaultName;
        }

        [Display(Name = "采集卡", GroupName = "输入", Order = 0, Description = "须与上游多采集节点中勾选的设备名一致")]
        [Editor(typeof(ComboBoxPropertyEditor))]
        [ItemsSource(typeof(AlgorithmDesignTimeOptions), nameof(AlgorithmDesignTimeOptions.GetAcquisitionDeviceNames), DisplayMemberPath = ".")]
        public string DataAcquisitionDeviceName
        {
            get => string.IsNullOrEmpty(_deviceName) ? AlgorithmDesignTimeOptions.UnselectedLabel : _deviceName;
            set
            {
                var v = value ?? string.Empty;
                if (string.Equals(v, AlgorithmDesignTimeOptions.UnselectedLabel, StringComparison.Ordinal))
                    v = string.Empty;
                if (string.Equals(_deviceName, v, StringComparison.Ordinal))
                    return;
                _deviceName = v;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ChannelNameOptions));
            }
        }

        [JsonIgnore]
        public IEnumerable<string> ChannelNameOptions => AlgorithmDesignTimeOptions.GetChannelNamesForDevice(DataAcquisitionDeviceName);

        [Display(Name = "振动通道", GroupName = "输入", Order = 1, Description = "空或默认项表示 Signal 组内首通道")]
        [Editor(typeof(ComboBoxPropertyEditor))]
        [ItemsSource(nameof(ChannelNameOptions), DisplayMemberPath = ".")]
        public string ChannelName
        {
            get => string.IsNullOrEmpty(_channelName) ? AlgorithmDesignTimeOptions.UseFirstChannelInGroupLabel : _channelName;
            set
            {
                var v = value ?? string.Empty;
                if (string.Equals(v, AlgorithmDesignTimeOptions.UseFirstChannelInGroupLabel, StringComparison.Ordinal))
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

        /// <summary>
        /// 切换刻度时同步参考值：dB 使用 <see cref="AlgorithmScaleReferenceDefaults.ForDb"/>，Linear 使用 <see cref="AlgorithmScaleReferenceDefaults.ForLinear"/>。
        /// </summary>
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
