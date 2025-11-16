using System;
using System.Collections.Generic;

namespace Astra.Core.Foundation.Abstractions
{
    /// <summary>
    /// 消息基类（平台通用）
    /// 提供消息的通用实现，各模块可以继承此类或实现 IMessage 接口
    /// </summary>
    public abstract class MessageBase : IMessage
    {
        /// <summary>
        /// 消息通道ID
        /// </summary>
        public string ChannelId { get; set; }

        /// <summary>
        /// 消息时间戳
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// 消息扩展属性
        /// </summary>
        public Dictionary<string, object> Properties { get; set; }

        /// <summary>
        /// 消息数据（字节数组）
        /// </summary>
        protected byte[] Data { get; set; }

        /// <summary>
        /// 消息长度
        /// </summary>
        public virtual int Length => Data?.Length ?? 0;

        /// <summary>
        /// 构造函数
        /// </summary>
        protected MessageBase()
        {
            Timestamp = DateTime.Now;
            Properties = new Dictionary<string, object>();
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        protected MessageBase(byte[] data, string channelId = null) : this()
        {
            Data = data;
            ChannelId = channelId;
        }

        /// <summary>
        /// 获取消息数据
        /// </summary>
        public virtual byte[] GetData()
        {
            return Data;
        }

        /// <summary>
        /// 设置消息数据
        /// </summary>
        public virtual void SetData(byte[] data)
        {
            Data = data;
        }

        /// <summary>
        /// 获取属性值
        /// </summary>
        public virtual T GetProperty<T>(string key, T defaultValue = default)
        {
            if (Properties != null && Properties.TryGetValue(key, out var value) && value is T typedValue)
            {
                return typedValue;
            }
            return defaultValue;
        }

        /// <summary>
        /// 设置属性值
        /// </summary>
        public virtual void SetProperty<T>(string key, T value)
        {
            if (Properties == null)
            {
                Properties = new Dictionary<string, object>();
            }
            Properties[key] = value;
        }

        /// <summary>
        /// 转换为字符串
        /// </summary>
        public override string ToString()
        {
            return $"[{GetType().Name}] ChannelId={ChannelId}, Length={Length}, Timestamp={Timestamp:yyyy-MM-dd HH:mm:ss.fff}";
        }
    }
}

