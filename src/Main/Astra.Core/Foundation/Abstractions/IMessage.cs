using System;
using System.Collections.Generic;

namespace Astra.Core.Foundation.Abstractions
{
    /// <summary>
    /// 消息接口（平台通用）
    /// 所有模块的消息类型都应实现此接口，以提供统一的消息结构
    /// </summary>
    public interface IMessage
    {
        /// <summary>
        /// 消息通道ID（可选）
        /// </summary>
        string ChannelId { get; set; }

        /// <summary>
        /// 消息时间戳
        /// </summary>
        DateTime Timestamp { get; set; }

        /// <summary>
        /// 消息扩展属性
        /// </summary>
        Dictionary<string, object> Properties { get; set; }

        /// <summary>
        /// 消息长度
        /// </summary>
        int Length { get; }

        /// <summary>
        /// 获取消息数据（字节数组形式）
        /// </summary>
        byte[] GetData();

        /// <summary>
        /// 设置消息数据
        /// </summary>
        void SetData(byte[] data);
    }
}

