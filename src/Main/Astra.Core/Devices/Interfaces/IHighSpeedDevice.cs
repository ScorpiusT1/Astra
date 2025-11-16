using Astra.Core.Foundation.Abstractions;

namespace Astra.Core.Devices.Interfaces
{
    /// <summary>
    /// 高性能设备接口
    /// 提供高速数据传输能力（异步和批量）
    /// </summary>
    public interface IHighSpeedDevice :
        IDevice,
        IAsyncDataTransfer,
        IBatchDataTransfer
    {
    }
}