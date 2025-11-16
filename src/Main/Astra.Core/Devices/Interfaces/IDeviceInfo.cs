using System.Collections.Generic;

namespace Astra.Core.Devices.Interfaces
{
    /// <summary>
    /// 设备基本信息接口
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
        /// 获取设备详细信息字典
        /// </summary>
        Dictionary<string, string> GetDeviceInfo();
    }
}