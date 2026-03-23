using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Astra.Core.Foundation.Common;

namespace Astra.Communication.Abstractions
{
    /// <summary>
    /// PLC 设备通信接口
    /// 提供基于地址（点位）的通用读写能力，支持同步和异步调用。
    /// </summary>
    public interface IPLC
    {
        /// <summary>
        /// PLC 协议标识（如 S7、ModbusTcp 等）
        /// </summary>
        string Protocol { get; }

        /// <summary>
        /// 从指定地址读取单个值
        /// </summary>
        OperationResult<T> Read<T>(string address);

        /// <summary>
        /// 从指定地址异步读取单个值
        /// </summary>
        Task<OperationResult<T>> ReadAsync<T>(string address, CancellationToken cancellationToken = default);

        /// <summary>
        /// 向指定地址写入单个值
        /// </summary>
        OperationResult Write<T>(string address, T value);

        /// <summary>
        /// 向指定地址异步写入单个值
        /// </summary>
        Task<OperationResult> WriteAsync<T>(string address, T value, CancellationToken cancellationToken = default);

        /// <summary>
        /// 批量读取（key: 业务键，value: PLC 地址）
        /// </summary>
        OperationResult<Dictionary<string, object>> BatchRead(Dictionary<string, string> addressMap);

        /// <summary>
        /// 异步批量读取（key: 业务键，value: PLC 地址）
        /// </summary>
        Task<OperationResult<Dictionary<string, object>>> BatchReadAsync(
            Dictionary<string, string> addressMap,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// 批量写入（key: PLC 地址，value: 要写入的值）
        /// </summary>
        OperationResult BatchWrite(Dictionary<string, object> values);

        /// <summary>
        /// 异步批量写入（key: PLC 地址，value: 要写入的值）
        /// </summary>
        Task<OperationResult> BatchWriteAsync(
            Dictionary<string, object> values,
            CancellationToken cancellationToken = default);
    }
}
