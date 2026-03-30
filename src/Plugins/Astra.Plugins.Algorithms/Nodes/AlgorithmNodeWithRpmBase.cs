using Astra.Plugins.Algorithms.Helpers;
using Astra.UI.Abstractions.Attributes;
using Astra.UI.PropertyEditors;
using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations;

namespace Astra.Plugins.Algorithms.Nodes
{
    /// <summary>需要同 Raw 中转速通道的算法节点基类。</summary>
    public abstract class AlgorithmNodeWithRpmBase : AlgorithmNodeBase
    {
        private string _rpmChannel = string.Empty;

        protected AlgorithmNodeWithRpmBase(string nodeTypeKey, string defaultName) : base(nodeTypeKey, defaultName)
        {
        }

        [JsonIgnore]
        public IEnumerable<string> RpmChannelOptions => ChannelNameOptions;

        [Display(Name = "转速通道", GroupName = "输入", Order = 2, Description = "须为 Raw 中与振动同文件内的转速通道")]
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

        protected string? ResolveRpmChannelKey()
        {
            var c = _rpmChannel?.Trim();
            if (string.IsNullOrEmpty(c))
                return null;
            return c;
        }
    }
}
