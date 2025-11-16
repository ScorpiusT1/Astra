using System;
using System.Threading;
using System.Threading.Tasks;
using Astra.Core.Foundation.Common;

namespace Astra.Core.Devices.Interfaces
{
    /// <summary>
    /// 设备连接管理接口
    /// </summary>
    public interface IDeviceConnection
    {
        DeviceStatus Status { get; }
        bool IsOnline { get; }

        OperationResult Connect();
        Task<OperationResult> ConnectAsync(CancellationToken cancellationToken = default);

        OperationResult Disconnect();
        Task<OperationResult> DisconnectAsync(CancellationToken cancellationToken = default);

        OperationResult<bool> DeviceExists();
        Task<OperationResult<bool>> DeviceExistsAsync(CancellationToken cancellationToken = default);

        OperationResult<bool> IsAlive();
        Task<OperationResult<bool>> IsAliveAsync(CancellationToken cancellationToken = default);

        OperationResult Reset();
        Task<OperationResult> ResetAsync(CancellationToken cancellationToken = default);

        event EventHandler<DeviceStatusChangedEventArgs> StatusChanged;
        event EventHandler<DeviceErrorEventArgs> ErrorOccurred;
    }
}