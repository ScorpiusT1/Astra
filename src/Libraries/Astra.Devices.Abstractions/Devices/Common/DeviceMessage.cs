using Astra.Core.Foundation.Abstractions;

namespace Astra.Core.Devices
{
    /// <summary>
    /// 设备消息类
    /// </summary>
    public class DeviceMessage : MessageBase
    {
        public new byte[] Data
        {
            get => base.Data;
            set => base.Data = value;
        }

        public new int Length
        {
            get => base.Length;
            set { }
        }

        public DeviceMessage() : base()
        {
        }

        public DeviceMessage(byte[] data, string channelId = null) : base(data, channelId)
        {
        }

        public DeviceMessage(byte[] data, int length, string channelId = null) : base(data, channelId)
        {
        }

        public override byte[] GetData()
        {
            return Data;
        }

        public override void SetData(byte[] data)
        {
            Data = data;
        }
    }
}

