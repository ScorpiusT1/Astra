using System.Text.Json.Serialization;
using System.Xml.Serialization;

namespace Astra.Core.Plugins.Manifest
{
    public class AddinInfo
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Version { get; set; }
        public string Description { get; set; }
        public string Author { get; set; }
        public string Copyright { get; set; }
        public string Website { get; set; }
        public string IconPath { get; set; }

        /// <summary>
        /// 工具箱根层中该插件所有分类的相对顺序（可选）。数值越小越靠前；未指定时排在已指定值的插件之后。
        /// XML 元素名为 <c>Order</c>。不使用 C# 属性名 <c>Order</c>，以免与 <see cref="XmlElementAttribute.Order"/>（元素序列顺序）混淆导致 XmlSerializer 无法填充该节点。
        /// </summary>
        [XmlElement("Order")]
        [JsonPropertyName("order")]
        public int? ToolboxOrder { get; set; }

        public RuntimeInfo Runtime { get; set; } = new();
        public List<AddinDependency> Dependencies { get; set; } = new();
        public PermissionsInfo Permissions { get; set; } = new();
    }
}
