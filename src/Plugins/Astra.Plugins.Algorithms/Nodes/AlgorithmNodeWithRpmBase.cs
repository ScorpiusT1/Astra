using Astra.Plugins.Algorithms.Helpers;
using Astra.UI.Abstractions.Attributes;
using Astra.UI.PropertyEditors;
using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations;

namespace Astra.Plugins.Algorithms.Nodes
{
    /// <summary>需要同 Raw 中转速通道的算法节点基类（转速通道保持单选）。</summary>
    public abstract class AlgorithmNodeWithRpmBase : AlgorithmNodeBase
    {
        private string _rpmChannel = string.Empty;

        protected AlgorithmNodeWithRpmBase(string nodeTypeKey, string defaultName) : base(nodeTypeKey, defaultName)
        {
        }

        [JsonIgnore]
        public IEnumerable<string> RpmChannelOptions => ChannelNameOptions;

        [Display(Name = "转速通道", GroupName = "输入", Order = 2, Description = "格式为「设备名/通道名」；须与振动通道在同一采集卡内")]
        [Editor(typeof(ComboBoxPropertyEditor))]
        [ItemsSource(nameof(RpmChannelOptions), DisplayMemberPath = ".")]
        public string RpmChannelName
        {
            get => string.IsNullOrEmpty(_rpmChannel) ? AlgorithmDesignTimeOptions.UnselectedLabel : _rpmChannel;
            set
            {
                var v = value ?? string.Empty;
                if (string.Equals(v, AlgorithmDesignTimeOptions.UnselectedLabel, StringComparison.Ordinal))
                    v = string.Empty;
                if (string.Equals(_rpmChannel, v, StringComparison.Ordinal))
                    return;
                _rpmChannel = v;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// 解析转速通道选择，返回 (DeviceName, ChannelName)。
        /// 格式为「设备名/通道名」时拆分；否则尝试从首个选中设备解析。
        /// </summary>
        protected (string? DeviceName, string? ChannelName) ResolveRpmSpec()
        {
            var c = _rpmChannel?.Trim();
            if (string.IsNullOrEmpty(c))
                return (null, null);

            var slashIdx = c.IndexOf('/');
            if (slashIdx > 0 && slashIdx < c.Length - 1)
                return (c.Substring(0, slashIdx), c.Substring(slashIdx + 1));

            var firstDevice = DataAcquisitionDeviceNames?.FirstOrDefault(d => !string.IsNullOrWhiteSpace(d));
            return (firstDevice, c);
        }
    }
}
