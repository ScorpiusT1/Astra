using System.Collections.Generic;

namespace Astra.Core.Devices.Specifications
{
    /// <summary>
    /// 设备规格接口
    /// </summary>
    public interface IDeviceSpecification
    {
        /// <summary>
        /// 设备类型
        /// </summary>
        DeviceType DeviceType { get; }

        /// <summary>
        /// 设备厂家
        /// </summary>
        string Manufacturer { get; }

        /// <summary>
        /// 设备型号
        /// </summary>
        string Model { get; }

        /// <summary>
        /// 显示名称
        /// </summary>
        string DisplayName { get; }

        /// <summary>
        /// 描述
        /// </summary>
        string Description { get; }

        /// <summary>
        /// 约束字典（用于限制配置参数，如最大通道数、最大采样率等）
        /// </summary>
        Dictionary<string, object> Constraints { get; }

        /// <summary>
        /// 能力字典（用于描述设备功能，如是否支持同步采样等）
        /// </summary>
        Dictionary<string, object> Capabilities { get; }

        /// <summary>
        /// 获取约束值（泛型方法，类型安全）
        /// </summary>
        T GetConstraint<T>(string key, T defaultValue = default);

        /// <summary>
        /// 获取能力值（泛型方法，类型安全）
        /// </summary>
        T GetCapability<T>(string key, T defaultValue = default);
    }
}

