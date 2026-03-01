namespace Astra.Core.Devices
{
    /// <summary>
    /// 设备状态枚举（完整生命周期）
    /// </summary>
    public enum DeviceStatus
    {
        Uninitialized,
        Connecting,
        Connected,
        Online,
        Offline,
        Disconnecting,
        Disconnected,
        Error,
        Busy
    }
}

