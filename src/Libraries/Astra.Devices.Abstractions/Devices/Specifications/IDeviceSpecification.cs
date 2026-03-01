using System.Collections.Generic;

namespace Astra.Core.Devices.Specifications
{
    /// <summary>
    /// 设备规格接口
    /// </summary>
    public interface IDeviceSpecification
    {
        DeviceType DeviceType { get; }
        string Manufacturer { get; }
        string Model { get; }
        string DisplayName { get; }
        string Description { get; }
        Dictionary<string, object> Constraints { get; }
        Dictionary<string, object> Capabilities { get; }
        T GetConstraint<T>(string key, T defaultValue = default);
        T GetCapability<T>(string key, T defaultValue = default);
    }
}

