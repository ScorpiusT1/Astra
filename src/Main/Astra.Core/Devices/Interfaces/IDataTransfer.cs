using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Astra.Core.Foundation.Common;

namespace Astra.Core.Devices.Interfaces
{
    /// <summary>
    /// 数据传输接口
    /// </summary>
    public interface IDataTransfer
    {
        /// <summary>
        /// 发送超时时间（毫秒）
        /// </summary>
        int SendTimeout { get; set; }

        /// <summary>
        /// 接收超时时间（毫秒）
        /// </summary>
        int ReceiveTimeout { get; set; }

        OperationResult Send(DeviceMessage message);
        OperationResult<DeviceMessage> Receive();
        OperationResult<DeviceMessage> Receive(string channelId);
        event EventHandler<DataReceivedEventArgs> DataReceived;
    }

    /// <summary>
    /// 异步数据传输接口
    /// </summary>
    public interface IAsyncDataTransfer
    {
        Task<OperationResult> SendAsync(DeviceMessage message, CancellationToken cancellationToken = default);
        Task<OperationResult<DeviceMessage>> ReceiveAsync(CancellationToken cancellationToken = default);
        Task<OperationResult<DeviceMessage>> ReceiveAsync(string channelId, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// 批量数据传输接口
    /// </summary>
    public interface IBatchDataTransfer
    {
        OperationResult<int> BatchSend(IEnumerable<DeviceMessage> messages);
        OperationResult<IEnumerable<DeviceMessage>> BatchReceive(int count);
        OperationResult<IEnumerable<DeviceMessage>> BatchReceive(IEnumerable<string> channelIds);
    }
}