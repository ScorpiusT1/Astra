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
    /// 继承 DeviceBase，并实现数据传输相关接口（同步、异步和批量）
    /// 子类需要实现核心的数据传输方法（Send、Receive等）
    /// </summary>
    /// <typeparam name="TConfig">设备配置类型，必须继承自 DeviceConfig</typeparam>
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

        #region IDataTransfer 接口实现

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

        /// <summary>
        /// 发送消息（子类必须实现）
        /// </summary>
        public abstract OperationResult Send(DeviceMessage message);

        /// <summary>
        /// 接收消息（子类必须实现）
        /// </summary>
        public abstract OperationResult<DeviceMessage> Receive();

        /// <summary>
        /// 从指定通道接收消息（子类必须实现）
        /// </summary>
        public abstract OperationResult<DeviceMessage> Receive(string channelId);

        public event EventHandler<DataReceivedEventArgs> DataReceived
        {
            add => _dataReceived += value;
            remove => _dataReceived -= value;
        }

        /// <summary>
        /// 触发数据接收事件（子类在接收到数据时调用）
        /// </summary>
        protected virtual void OnDataReceived(DataReceivedEventArgs e)
        {
            _dataReceived?.Invoke(this, e);
        }

        #endregion

        #region IAsyncDataTransfer 接口实现

        /// <summary>
        /// 异步发送消息（默认使用同步方法包装，子类可重写以提供真正的异步实现）
        /// </summary>
        public virtual Task<OperationResult> SendAsync(DeviceMessage message, CancellationToken cancellationToken = default)
        {
            return Task.Run(() => Send(message), cancellationToken);
        }

        /// <summary>
        /// 异步接收消息（默认使用同步方法包装，子类可重写以提供真正的异步实现）
        /// </summary>
        public virtual Task<OperationResult<DeviceMessage>> ReceiveAsync(CancellationToken cancellationToken = default)
        {
            return Task.Run(() => Receive(), cancellationToken);
        }

        /// <summary>
        /// 异步从指定通道接收消息（默认使用同步方法包装，子类可重写以提供真正的异步实现）
        /// </summary>
        public virtual Task<OperationResult<DeviceMessage>> ReceiveAsync(string channelId, CancellationToken cancellationToken = default)
        {
            return Task.Run(() => Receive(channelId), cancellationToken);
        }

        #endregion

        #region IBatchDataTransfer 接口实现

        /// <summary>
        /// 批量发送消息（默认实现：循环调用 Send，子类可重写以提供更高效的批量实现）
        /// </summary>
        public virtual OperationResult<int> BatchSend(IEnumerable<DeviceMessage> messages)
        {
            if (messages == null)
                return OperationResult<int>.Fail("消息列表不能为空", ErrorCodes.InvalidData);

            int successCount = 0;
            foreach (var message in messages)
            {
                var result = Send(message);
                if (result.Success)
                    successCount++;
            }
            return OperationResult<int>.Succeed(successCount, $"批量发送完成: 成功 {successCount}/{messages.Count()}");
        }

        /// <summary>
        /// 批量接收消息（默认实现：循环调用 Receive，子类可重写以提供更高效的批量实现）
        /// </summary>
        public virtual OperationResult<IEnumerable<DeviceMessage>> BatchReceive(int count)
        {
            if (count <= 0)
                return OperationResult<IEnumerable<DeviceMessage>>.Fail("接收数量必须大于0", ErrorCodes.InvalidData);

            var messages = new List<DeviceMessage>();
            for (int i = 0; i < count; i++)
            {
                var result = Receive();
                if (result.Success)
                    messages.Add(result.Data);
                else
                    break;
            }
            return OperationResult<IEnumerable<DeviceMessage>>.Succeed(messages, $"批量接收完成: 成功 {messages.Count}/{count}");
        }

        /// <summary>
        /// 从指定通道批量接收消息（默认实现：循环调用 Receive，子类可重写以提供更高效的批量实现）
        /// </summary>
        public virtual OperationResult<IEnumerable<DeviceMessage>> BatchReceive(IEnumerable<string> channelIds)
        {
            if (channelIds == null)
                return OperationResult<IEnumerable<DeviceMessage>>.Fail("通道ID列表不能为空", ErrorCodes.InvalidData);

            var messages = new List<DeviceMessage>();
            foreach (var channelId in channelIds)
            {
                var result = Receive(channelId);
                if (result.Success)
                    messages.Add(result.Data);
            }
            return OperationResult<IEnumerable<DeviceMessage>>.Succeed(messages, $"批量接收完成: 成功 {messages.Count}/{channelIds.Count()}");
        }

        #endregion

        #region 资源释放

        public override void Dispose()
        {
            // 调用基类的 Dispose
            base.Dispose();
        }

        #endregion
    }
}