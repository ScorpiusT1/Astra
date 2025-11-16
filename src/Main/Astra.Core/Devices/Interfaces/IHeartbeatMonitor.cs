using Astra.Core.Foundation.Common;

namespace Astra.Core.Devices.Interfaces
{
    /// <summary>
    /// 心跳监测接口
    /// </summary>
    public interface IHeartbeatMonitor
    {
        OperationResult StartHeartbeat();
        OperationResult StopHeartbeat();
        bool IsHeartbeatRunning { get; }
        int HeartbeatInterval { get; set; }
        int HeartbeatTimeoutThreshold { get; set; }
    }
}