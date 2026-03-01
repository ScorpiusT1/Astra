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
    /// 鏁版嵁浼犺緭璁惧鍩虹被
    /// 缁ф壙 DeviceBase锛屽苟瀹炵幇鏁版嵁浼犺緭鐩稿叧鎺ュ彛锛堝悓姝ャ€佸紓姝ュ拰鎵归噺锛?    /// 瀛愮被闇€瑕佸疄鐜版牳蹇冪殑鏁版嵁浼犺緭鏂规硶锛圫end銆丷eceive绛夛級
    /// </summary>
    /// <typeparam name="TConfig">璁惧閰嶇疆绫诲瀷锛屽繀椤荤户鎵胯嚜 DeviceConfig</typeparam>
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

        #region IDataTransfer 鎺ュ彛瀹炵幇

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
        /// 鍙戦€佹秷鎭紙瀛愮被蹇呴』瀹炵幇锛?        /// </summary>
        public abstract OperationResult Send(DeviceMessage message);

        /// <summary>
        /// 鎺ユ敹娑堟伅锛堝瓙绫诲繀椤诲疄鐜帮級
        /// </summary>
        public abstract OperationResult<DeviceMessage> Receive();

        /// <summary>
        /// 浠庢寚瀹氶€氶亾鎺ユ敹娑堟伅锛堝瓙绫诲繀椤诲疄鐜帮級
        /// </summary>
        public abstract OperationResult<DeviceMessage> Receive(string channelId);

        public event EventHandler<DataReceivedEventArgs> DataReceived
        {
            add => _dataReceived += value;
            remove => _dataReceived -= value;
        }

        /// <summary>
        /// 瑙﹀彂鏁版嵁鎺ユ敹浜嬩欢锛堝瓙绫诲湪鎺ユ敹鍒版暟鎹椂璋冪敤锛?        /// </summary>
        protected virtual void OnDataReceived(DataReceivedEventArgs e)
        {
            _dataReceived?.Invoke(this, e);
        }

        #endregion

        #region IAsyncDataTransfer 鎺ュ彛瀹炵幇

        /// <summary>
        /// 寮傛鍙戦€佹秷鎭紙榛樿浣跨敤鍚屾鏂规硶鍖呰锛屽瓙绫诲彲閲嶅啓浠ユ彁渚涚湡姝ｇ殑寮傛瀹炵幇锛?        /// </summary>
        public virtual Task<OperationResult> SendAsync(DeviceMessage message, CancellationToken cancellationToken = default)
        {
            return Task.Run(() => Send(message), cancellationToken);
        }

        /// <summary>
        /// 寮傛鎺ユ敹娑堟伅锛堥粯璁や娇鐢ㄥ悓姝ユ柟娉曞寘瑁咃紝瀛愮被鍙噸鍐欎互鎻愪緵鐪熸鐨勫紓姝ュ疄鐜帮級
        /// </summary>
        public virtual Task<OperationResult<DeviceMessage>> ReceiveAsync(CancellationToken cancellationToken = default)
        {
            return Task.Run(() => Receive(), cancellationToken);
        }

        /// <summary>
        /// 寮傛浠庢寚瀹氶€氶亾鎺ユ敹娑堟伅锛堥粯璁や娇鐢ㄥ悓姝ユ柟娉曞寘瑁咃紝瀛愮被鍙噸鍐欎互鎻愪緵鐪熸鐨勫紓姝ュ疄鐜帮級
        /// </summary>
        public virtual Task<OperationResult<DeviceMessage>> ReceiveAsync(string channelId, CancellationToken cancellationToken = default)
        {
            return Task.Run(() => Receive(channelId), cancellationToken);
        }

        #endregion

        #region IBatchDataTransfer 鎺ュ彛瀹炵幇

        /// <summary>
        /// 鎵归噺鍙戦€佹秷鎭紙榛樿瀹炵幇锛氬惊鐜皟鐢?Send锛屽瓙绫诲彲閲嶅啓浠ユ彁渚涙洿楂樻晥鐨勬壒閲忓疄鐜帮級
        /// </summary>
        public virtual OperationResult<int> BatchSend(IEnumerable<DeviceMessage> messages)
        {
            if (messages == null)
                return OperationResult<int>.Failure("娑堟伅鍒楄〃涓嶈兘涓虹┖", ErrorCodes.InvalidData);

            int successCount = 0;
            foreach (var message in messages)
            {
                var result = Send(message);
                if (result.Success)
                    successCount++;
            }
            return OperationResult<int>.Succeed(successCount, $"鎵归噺鍙戦€佸畬鎴? 鎴愬姛 {successCount}/{messages.Count()}");
        }

        /// <summary>
        /// 鎵归噺鎺ユ敹娑堟伅锛堥粯璁ゅ疄鐜帮細寰幆璋冪敤 Receive锛屽瓙绫诲彲閲嶅啓浠ユ彁渚涙洿楂樻晥鐨勬壒閲忓疄鐜帮級
        /// </summary>
        public virtual OperationResult<IEnumerable<DeviceMessage>> BatchReceive(int count)
        {
            if (count <= 0)
                return OperationResult<IEnumerable<DeviceMessage>>.Failure("鎺ユ敹鏁伴噺蹇呴』澶т簬0", ErrorCodes.InvalidData);

            var messages = new List<DeviceMessage>();
            for (int i = 0; i < count; i++)
            {
                var result = Receive();
                if (result.Success)
                    messages.Add(result.Data);
                else
                    break;
            }
            return OperationResult<IEnumerable<DeviceMessage>>.Succeed(messages, $"鎵归噺鎺ユ敹瀹屾垚: 鎴愬姛 {messages.Count}/{count}");
        }

        /// <summary>
        /// 浠庢寚瀹氶€氶亾鎵归噺鎺ユ敹娑堟伅锛堥粯璁ゅ疄鐜帮細寰幆璋冪敤 Receive锛屽瓙绫诲彲閲嶅啓浠ユ彁渚涙洿楂樻晥鐨勬壒閲忓疄鐜帮級
        /// </summary>
        public virtual OperationResult<IEnumerable<DeviceMessage>> BatchReceive(IEnumerable<string> channelIds)
        {
            if (channelIds == null)
                return OperationResult<IEnumerable<DeviceMessage>>.Failure("閫氶亾ID鍒楄〃涓嶈兘涓虹┖", ErrorCodes.InvalidData);

            var messages = new List<DeviceMessage>();
            foreach (var channelId in channelIds)
            {
                var result = Receive(channelId);
                if (result.Success)
                    messages.Add(result.Data);
            }
            return OperationResult<IEnumerable<DeviceMessage>>.Succeed(messages, $"鎵归噺鎺ユ敹瀹屾垚: 鎴愬姛 {messages.Count}/{channelIds.Count()}");
        }

        #endregion

        #region 璧勬簮閲婃斁

        public override void Dispose()
        {
            // 璋冪敤鍩虹被鐨?Dispose
            base.Dispose();
        }

        #endregion
    }
}