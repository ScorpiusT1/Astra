using System;

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

        public string TypeName { get; }
        public string DisplayName { get; }
        public string? Description { get; set; }
        public string? Icon { get; set; }
        public string? Category { get; set; }
        public string? Manufacturer { get; set; }
        public int Order { get; set; }
    }
}

