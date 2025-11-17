using Astra.Core.Devices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Astra.Plugins.DataAcquisition.Abstractions
{
    /// <summary>
    /// 数据采集器接口 - 核心接口
    /// </summary>
    public interface IDataAcquisition
    {
        /// <summary>
        /// 设备唯一标识
        /// </summary>
        string DeviceId { get; }

        /// <summary>
        /// 初始化采集设备
        /// </summary>
        Task<bool> InitializeAsync();

        /// <summary>
        /// 启动数据采集
        /// </summary>
        Task StartAcquisitionAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// 停止数据采集
        /// </summary>
        Task StopAcquisitionAsync();

        /// <summary>
        /// 暂停采集
        /// </summary>
        Task PauseAsync();

        /// <summary>
        /// 恢复采集
        /// </summary>
        Task ResumeAsync();

        /// <summary>
        /// 获取当前采集状态
        /// </summary>
        AcquisitionState GetState();

        /// <summary>
        /// 释放资源
        /// </summary>
        Task DisposeAsync();

        /// <summary>
        /// 数据就绪事件
        /// </summary>
        event EventHandler<DeviceMessage> DataReceived;

        /// <summary>
        /// 错误事件
        /// </summary>
        event EventHandler<Exception> ErrorOccurred;
    }

    /// <summary>
    /// 采集状态
    /// </summary>
    public enum AcquisitionState
    {
        Idle,
        Running,
        Paused,
        Error
    }
}
