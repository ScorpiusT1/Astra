using Astra.Core.Devices.Configuration;
using Astra.Core.Devices.Interfaces;
using Astra.Core.Foundation.Common;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Astra.Core.Devices.Base
{
    /// <summary>
    /// 数据传输设备基类
    /// </summary>
    public abstract class DataTransferDeviceBase<TConfig> : DeviceBase<TConfig>,
        IDataTransfer,
        IAsyncDataTransfer,
        IBatchDataTransfer
        where TConfig : DeviceConfig
    {
        private int _sendTimeout = 5000;
        private int _receiveTimeout = 5000;
        private event EventHandler<DataReceivedEventArgs> _dataReceived;

        protected DataTransferDeviceBase(
            DeviceConnectionBase connection,
            TConfig config)
            : base(connection, config)
        {
        }

        public int SendTimeout
        {
            get => _sendTimeout;
            set => _sendTimeout = value;
        }

        public int ReceiveTimeout
        {
            get => _receiveTimeout;
            set => _receiveTimeout = value;
        }

        public abstract OperationResult Send(DeviceMessage message);
        public abstract OperationResult<DeviceMessage> Receive();
        public abstract OperationResult<DeviceMessage> Receive(string channelId);

        public event EventHandler<DataReceivedEventArgs> DataReceived
        {
            add => _dataReceived += value;
            remove => _dataReceived -= value;
        }

        protected virtual void OnDataReceived(DataReceivedEventArgs e)
        {
            _dataReceived?.Invoke(this, e);
        }

        public virtual Task<OperationResult> SendAsync(DeviceMessage message, CancellationToken cancellationToken = default)
        {
            return Task.Run(() => Send(message), cancellationToken);
        }

        public virtual Task<OperationResult<DeviceMessage>> ReceiveAsync(CancellationToken cancellationToken = default)
        {
            return Task.Run(() => Receive(), cancellationToken);
        }

        public virtual Task<OperationResult<DeviceMessage>> ReceiveAsync(string channelId, CancellationToken cancellationToken = default)
        {
            return Task.Run(() => Receive(channelId), cancellationToken);
        }

        public virtual OperationResult<int> BatchSend(IEnumerable<DeviceMessage> messages)
        {
            if (messages == null)
                return OperationResult<int>.Failure("消息列表不能为空", ErrorCodes.InvalidData);

            int successCount = 0;
            foreach (var message in messages)
            {
                var result = Send(message);
                if (result.Success)
                {
                    successCount++;
                }
            }

            return OperationResult<int>.Succeed(successCount);
        }

        public virtual OperationResult<IEnumerable<DeviceMessage>> BatchReceive(int count)
        {
            var list = new List<DeviceMessage>();
            for (int i = 0; i < count; i++)
            {
                var result = Receive();
                if (result.Success && result.Data != null)
                {
                    list.Add(result.Data);
                }
            }
            return OperationResult<IEnumerable<DeviceMessage>>.Succeed(list);
        }

        public virtual OperationResult<IEnumerable<DeviceMessage>> BatchReceive(IEnumerable<string> channelIds)
        {
            if (channelIds == null)
                return OperationResult<IEnumerable<DeviceMessage>>.Failure("通道列表不能为空", ErrorCodes.InvalidData);

            var list = new List<DeviceMessage>();
            foreach (var channelId in channelIds)
            {
                var result = Receive(channelId);
                if (result.Success && result.Data != null)
                {
                    list.Add(result.Data);
                }
            }
            return OperationResult<IEnumerable<DeviceMessage>>.Succeed(list);
        }
    }
}

