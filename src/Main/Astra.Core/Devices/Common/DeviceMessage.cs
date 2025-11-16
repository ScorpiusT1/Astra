using System;
using System.Collections.Generic;
using Astra.Core.Foundation.Abstractions;

namespace Astra.Core.Devices
{
    /// <summary>
    /// 设备消息类
    /// 继承自 MessageBase，提供设备特定的消息实现
    /// </summary>
    public class DeviceMessage : MessageBase
    {
        /// <summary>
        /// 消息数据（字节数组）
        /// 重写基类的 Data 属性，使其可公开访问
        /// </summary>
        public new byte[] Data
        {
            get => base.Data;
            set => base.Data = value;
        }

        /// <summary>
        /// 消息长度
        /// </summary>
        public new int Length
        {
            get => base.Length;
            set { } // 保持向后兼容，但长度由 Data 自动计算
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        public DeviceMessage() : base()
        {
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        public DeviceMessage(byte[] data, string channelId = null) : base(data, channelId)
        {
        }

        /// <summary>
        /// 构造函数（指定长度，用于内存池场景）
        /// </summary>
        public DeviceMessage(byte[] data, int length, string channelId = null) : base(data, channelId)
        {
            // Length 属性由基类自动计算，这里保留参数以保持向后兼容
        }

        /// <summary>
        /// 重写获取数据方法，直接返回 Data 属性
        /// </summary>
        public override byte[] GetData()
        {
            return Data;
        }

        /// <summary>
        /// 重写设置数据方法，同步更新 Data 属性
        /// </summary>
        public override void SetData(byte[] data)
        {
            Data = data;
        }
    }
}