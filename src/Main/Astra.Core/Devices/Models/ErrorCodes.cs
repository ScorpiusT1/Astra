namespace Astra.Core.Devices
{
    /// <summary>
    /// 统一错误码
    /// </summary>
    public static class ErrorCodes
    {
        // 连接相关 (1000-1999)
        public const int ConnectFailed = 1001;
        public const int DisconnectFailed = 1002;
        public const int ResetFailed = 1003;
        public const int DeviceNotFound = 1004;
        public const int DeviceNoResponse = 1005;
        public const int DeviceNotOnline = 1006;
        public const int DeviceInUse = 1007;

        // 心跳相关 (2000-2999)
        public const int HeartbeatAlreadyRunning = 2001;
        public const int HeartbeatNotRunning = 2002;
        public const int HeartbeatTimeout = 2003;
        public const int HeartbeatError = 2004;

        // 数据传输相关 (3000-3999)
        public const int SendFailed = 3001;
        public const int ReceiveFailed = 3002;
        public const int ReceiveTimeout = 3003;
        public const int ChannelMismatch = 3004;
        public const int InvalidData = 3005;

        // 采集相关 (4000-4999)
        public const int AcquisitionAlreadyRunning = 4001;
        public const int AcquisitionNotRunning = 4002;
        public const int AcquisitionError = 4003;

        // 缓冲区相关 (5000-5999)
        public const int BufferEmpty = 5001;
        public const int BufferFull = 5002;
        public const int BufferReadError = 5003;
        public const int BufferWriteError = 5004;

        // 通道相关 (6000-6999)
        public const int ChannelNotFound = 6001;
        public const int ChannelDisabled = 6002;
        public const int ChannelError = 6003;

        // 配置相关 (7000-7999)
        public const int InvalidConfig = 7001;
        public const int ConfigRequireRestart = 7002;
        public const int ConfigApplyFailed = 7003;
        public const int ConfigSaveFailed = 7004;
        public const int ConfigLoadFailed = 7005;
        public const int FileNotFound = 7006;
        public const int NotSupported = 7007;
        public const int InvalidConfigData = 7008;
        public const int ConfigNotFound = 7009;

        // 工作流相关 (8000-8999)
        public const int ExecutionFailed = 8001;
        public const int NotFound = 8002; // 通用"未找到"错误码，适用于工作流、节点等
    }
}