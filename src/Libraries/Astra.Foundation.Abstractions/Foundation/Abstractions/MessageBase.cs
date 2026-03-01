using System;
using System.Collections.Generic;

namespace Astra.Core.Foundation.Abstractions
{
    /// <summary>
    /// 消息基类（平台通用）
    /// </summary>
    public abstract class MessageBase : IMessage
    {
        public string ChannelId { get; set; }
        public DateTime Timestamp { get; set; }
        public Dictionary<string, object> Properties { get; set; }
        protected byte[] Data { get; set; }
        public virtual int Length => Data?.Length ?? 0;

        protected MessageBase()
        {
            Timestamp = DateTime.Now;
            Properties = new Dictionary<string, object>();
        }

        protected MessageBase(byte[] data, string channelId = null) : this()
        {
            Data = data;
            ChannelId = channelId;
        }

        public virtual byte[] GetData()
        {
            return Data;
        }

        public virtual void SetData(byte[] data)
        {
            Data = data;
        }

        public virtual T GetProperty<T>(string key, T defaultValue = default)
        {
            if (Properties != null && Properties.TryGetValue(key, out var value) && value is T typedValue)
            {
                return typedValue;
            }
            return defaultValue;
        }

        public virtual void SetProperty<T>(string key, T value)
        {
            if (Properties == null)
            {
                Properties = new Dictionary<string, object>();
            }
            Properties[key] = value;
        }

        public override string ToString()
        {
            return $"[{GetType().Name}] ChannelId={ChannelId}, Length={Length}, Timestamp={Timestamp:yyyy-MM-dd HH:mm:ss.fff}";
        }
    }
}

