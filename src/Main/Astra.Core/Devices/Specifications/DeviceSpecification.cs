using System;
using System.Collections.Generic;

namespace Astra.Core.Devices.Specifications
{
    /// <summary>
    /// 设备规格实现
    /// </summary>
    public class DeviceSpecification : IDeviceSpecification
    {
        public DeviceType DeviceType { get; set; }
        public string Manufacturer { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public Dictionary<string, object> Constraints { get; set; } = new Dictionary<string, object>();
        public Dictionary<string, object> Capabilities { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// 获取约束值（泛型方法，类型安全）
        /// </summary>
        public T GetConstraint<T>(string key, T defaultValue = default)
        {
            if (Constraints.TryGetValue(key, out var value) && value is T typedValue)
            {
                return typedValue;
            }
            return defaultValue;
        }

        /// <summary>
        /// 获取能力值（泛型方法，类型安全）
        /// </summary>
        public T GetCapability<T>(string key, T defaultValue = default)
        {
            if (Capabilities.TryGetValue(key, out var value) && value is T typedValue)
            {
                return typedValue;
            }
            return defaultValue;
        }
    }
}

