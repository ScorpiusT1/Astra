using System.Xml.Serialization;

namespace Astra.Core.Plugins.Manifest
{
    /// <summary>
    /// 节点信息 - 用于清单文件中声明插件提供的节点
    /// </summary>
    public class NodeInfo
    {
        /// <summary>
        /// 节点名称（显示名称）
        /// </summary>
        [XmlAttribute("Name")]
        public string Name { get; set; }

        /// <summary>
        /// 节点类型名称（完整类型名，如 "YourPlugin.YourNode"）
        /// </summary>
        [XmlAttribute("TypeName")]
        public string TypeName { get; set; }

        /// <summary>
        /// 图标代码
        /// </summary>
        [XmlAttribute("IconCode")]
        public string IconCode { get; set; }

        /// <summary>
        /// 节点描述
        /// </summary>
        [XmlAttribute("Description")]
        public string Description { get; set; }

        /// <summary>
        /// 节点分类（可选）- 用于将节点分组到不同的 ToolCategory
        /// 如果为空或未设置，则使用插件名称作为分类
        /// </summary>
        [XmlAttribute("Category")]
        public string Category { get; set; }
    }
}

