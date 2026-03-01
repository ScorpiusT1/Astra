namespace Astra.Core.Devices.Interfaces
{
    /// <summary>
    /// 高性能设备接口
    /// </summary>
    public interface IHighSpeedDevice :
        IDevice,
        IAsyncDataTransfer,
        IBatchDataTransfer
    {
    }
}

