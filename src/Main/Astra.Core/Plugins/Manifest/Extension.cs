using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;

namespace Astra.Core.Plugins.Manifest
{
    public class Extension
    {
        public string Path { get; set; }
        public string TypeName { get; set; }
        
        [XmlIgnore]
        public Dictionary<string, object> Properties { get; set; } = new();

        /// <summary>
        /// XML 序列化用的属性项列表
        /// </summary>
        [XmlArray("Properties")]
        [XmlArrayItem("Property")]
        public List<PropertyItem> PropertyItems
        {
            get
            {
                return Properties?.Select(kvp => new PropertyItem
                {
                    Key = kvp.Key,
                    Value = kvp.Value?.ToString() ?? string.Empty
                }).ToList() ?? new List<PropertyItem>();
            }
            set
            {
                Properties = value?.ToDictionary(
                    item => item.Key,
                    item => (object)item.Value
                ) ?? new Dictionary<string, object>();
            }
        }
    }

    /// <summary>
    /// 属性项（用于 XML 序列化）
    /// </summary>
    public class PropertyItem
    {
        [XmlAttribute("key")]
        public string Key { get; set; }

        [XmlAttribute("value")]
        public string Value { get; set; }
    }
}
