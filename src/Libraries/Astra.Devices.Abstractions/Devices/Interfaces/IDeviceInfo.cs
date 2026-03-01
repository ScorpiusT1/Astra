using System.Collections.Generic;

namespace Astra.Core.Devices.Interfaces
{
    /// <summary>
    /// 设备基本信息接口（包含设备标识信息）
    /// </summary>
    public interface IDeviceInfo
    {
        string DeviceId { get; }
        string DeviceName { get; set; }
        DeviceType Type { get; }
        bool IsEnabled { get; set; }
        string GroupId { get; set; }
        string SlotId { get; set; }
        string Manufacturer { get; set; }
        string Model { get; set; }
        string SerialNumber { get; set; }
        Dictionary<string, string> GetDeviceInfo();
    }
}

