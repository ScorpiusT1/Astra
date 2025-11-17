using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Astra.Core.Devices.Attributes
{
    /// <summary>
    /// 设备类型特性（标注在设备类上）
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class DeviceTypeAttribute : Attribute
    {
        public DeviceTypeAttribute(string typeName, string displayName)
        {
            TypeName = typeName;
            DisplayName = displayName;
        }

        /// <summary>
        /// 设备类型名称（唯一标识）
        /// </summary>
        public string TypeName { get; }

        /// <summary>
        /// 显示名称
        /// </summary>
        public string DisplayName { get; }

        /// <summary>
        /// 描述
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// 图标（资源路径或 FontAwesome 图标名）
        /// </summary>
        public string? Icon { get; set; }

        /// <summary>
        /// 分类（用于树形结构分组）
        /// </summary>
        public string? Category { get; set; }

        /// <summary>
        /// 厂商
        /// </summary>
        public string? Manufacturer { get; set; }

        /// <summary>
        /// 排序优先级
        /// </summary>
        public int Order { get; set; }
    }
}
