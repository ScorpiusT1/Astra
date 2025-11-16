namespace Astra.Core.Devices
{

    /// <summary>
    /// 设备状态枚举（完整生命周期）
    /// </summary>
    public enum DeviceStatus
    {
        /// <summary>
        /// 未初始化
        /// </summary>
        Uninitialized,

        /// <summary>
        /// 连接中
        /// </summary>
        Connecting,

        /// <summary>
        /// 已连接
        /// </summary>
        Connected,

        /// <summary>
        /// 在线
        /// </summary>
        Online,

        /// <summary>
        /// 离线
        /// </summary>
        Offline,

        /// <summary>
        /// 断开中
        /// </summary>
        Disconnecting,

        /// <summary>
        /// 已断开
        /// </summary>
        Disconnected,

        /// <summary>
        /// 错误
        /// </summary>
        Error,

        /// <summary>
        /// 忙碌
        /// </summary>
        Busy
    }
}