using System;
using System.Collections.Generic;

namespace Astra.Core.Foundation.Abstractions
{
    /// <summary>
    /// 消息接口（平台通用）
    /// </summary>
    public interface IMessage
    {
        string ChannelId { get; set; }
        DateTime Timestamp { get; set; }
        Dictionary<string, object> Properties { get; set; }
        int Length { get; }
        byte[] GetData();
        void SetData(byte[] data);
    }
}

