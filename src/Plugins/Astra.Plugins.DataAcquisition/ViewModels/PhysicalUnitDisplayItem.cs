using Astra.Plugins.DataAcquisition.Configs;

namespace Astra.Plugins.DataAcquisition.ViewModels
{
    /// <summary>
    /// 物理单位显示项（用于ComboBox）
    /// </summary>
    public class PhysicalUnitDisplayItem
    {
        /// <summary>枚举值</summary>
        public PhysicalUnit EnumValue { get; set; }

        /// <summary>显示文本（简写）</summary>
        public string DisplayText { get; set; }

        /// <summary>完整名称（用于存储）</summary>
        public string FullName { get; set; }

        public override string ToString() => DisplayText;
    }
}

