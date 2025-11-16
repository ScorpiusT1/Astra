namespace Astra.Core.Devices.Interfaces
{
    /// <summary>
    /// 设备核心接口
    /// </summary>
    public interface IDevice : IDeviceInfo, IDeviceConnection, IDisposable
    {
    }
}