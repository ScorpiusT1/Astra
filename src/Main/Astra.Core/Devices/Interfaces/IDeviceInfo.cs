using System.Collections.Generic;

namespace Astra.Core.Devices.Interfaces
{
    /// <summary>
    /// 设备基本信息接口（包含设备标识信息）
    /// </summary>
    public interface IDeviceInfo
    {
        /// <summary>
        /// 设备唯一编号
        /// </summary>
        string DeviceId { get; }

        /// <summary>
        /// 设备显示名称
        /// </summary>
        string DeviceName { get; set; }

        /// <summary>
        /// 设备类型（枚举）
        /// </summary>
        DeviceType Type { get; }

        /// <summary>
        /// 设备是否启用
        /// </summary>
        bool IsEnabled { get; set; }

        /// <summary>
        /// 设备所属分组ID（用于多产品测试分区）
        /// </summary>
        string GroupId { get; set; }

        /// <summary>
        /// 设备所在槽位ID（用于多槽位部署）
        /// </summary>
        string SlotId { get; set; }

        /// <summary>
        /// 设备厂家（用于区分不同厂家的同类型设备）
        /// </summary>
        string Manufacturer { get; set; }

        /// <summary>
        /// 设备型号（用于区分同一厂家的不同型号）
        /// </summary>
        string Model { get; set; }

        /// <summary>
        /// 设备序列号
        /// </summary>
        string SerialNumber { get; set; }

        /// <summary>
        /// 获取设备详细信息字典
        /// </summary>
        Dictionary<string, string> GetDeviceInfo();
    }
}