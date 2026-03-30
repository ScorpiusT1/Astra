using Astra.Core.Constants;

namespace Astra.Core.Devices
{
    /// <summary>
    /// 统一错误码
    /// </summary>
    public static class ErrorCodes
    {
        // 连接相关 (1000-1999)
        public const int ConnectFailed = AstraSharedConstants.DeviceErrorCodes.ConnectFailed;
        public const int DisconnectFailed = AstraSharedConstants.DeviceErrorCodes.DisconnectFailed;
        public const int ResetFailed = AstraSharedConstants.DeviceErrorCodes.ResetFailed;
        public const int DeviceNotFound = AstraSharedConstants.DeviceErrorCodes.DeviceNotFound;
        public const int DeviceNoResponse = AstraSharedConstants.DeviceErrorCodes.DeviceNoResponse;
        public const int DeviceNotOnline = AstraSharedConstants.DeviceErrorCodes.DeviceNotOnline;
        public const int DeviceInUse = AstraSharedConstants.DeviceErrorCodes.DeviceInUse;

        // 心跳相关 (2000-2999)
        public const int HeartbeatAlreadyRunning = AstraSharedConstants.DeviceErrorCodes.HeartbeatAlreadyRunning;
        public const int HeartbeatNotRunning = AstraSharedConstants.DeviceErrorCodes.HeartbeatNotRunning;
        public const int HeartbeatTimeout = AstraSharedConstants.DeviceErrorCodes.HeartbeatTimeout;
        public const int HeartbeatError = AstraSharedConstants.DeviceErrorCodes.HeartbeatError;

        // 数据传输相关 (3000-3999)
        public const int SendFailed = AstraSharedConstants.DeviceErrorCodes.SendFailed;
        public const int ReceiveFailed = AstraSharedConstants.DeviceErrorCodes.ReceiveFailed;
        public const int ReceiveTimeout = AstraSharedConstants.DeviceErrorCodes.ReceiveTimeout;
        public const int ChannelMismatch = AstraSharedConstants.DeviceErrorCodes.ChannelMismatch;
        public const int InvalidData = AstraSharedConstants.DeviceErrorCodes.InvalidData;

        // 采集相关 (4000-4999)
        public const int AcquisitionAlreadyRunning = AstraSharedConstants.DeviceErrorCodes.AcquisitionAlreadyRunning;
        public const int AcquisitionNotRunning = AstraSharedConstants.DeviceErrorCodes.AcquisitionNotRunning;
        public const int AcquisitionError = AstraSharedConstants.DeviceErrorCodes.AcquisitionError;

        // 缓冲区相关 (5000-5999)
        public const int BufferEmpty = AstraSharedConstants.DeviceErrorCodes.BufferEmpty;
        public const int BufferFull = AstraSharedConstants.DeviceErrorCodes.BufferFull;
        public const int BufferReadError = AstraSharedConstants.DeviceErrorCodes.BufferReadError;
        public const int BufferWriteError = AstraSharedConstants.DeviceErrorCodes.BufferWriteError;

        // 通道相关 (6000-6999)
        public const int ChannelNotFound = AstraSharedConstants.DeviceErrorCodes.ChannelNotFound;
        public const int ChannelDisabled = AstraSharedConstants.DeviceErrorCodes.ChannelDisabled;
        public const int ChannelError = AstraSharedConstants.DeviceErrorCodes.ChannelError;

        // 配置相关 (7000-7999)
        public const int InvalidConfig = AstraSharedConstants.DeviceErrorCodes.InvalidConfig;
        public const int ConfigRequireRestart = AstraSharedConstants.DeviceErrorCodes.ConfigRequireRestart;
        public const int ConfigApplyFailed = AstraSharedConstants.DeviceErrorCodes.ConfigApplyFailed;
        public const int ConfigSaveFailed = AstraSharedConstants.DeviceErrorCodes.ConfigSaveFailed;
        public const int ConfigLoadFailed = AstraSharedConstants.DeviceErrorCodes.ConfigLoadFailed;
        public const int FileNotFound = AstraSharedConstants.DeviceErrorCodes.FileNotFound;
        public const int NotSupported = AstraSharedConstants.DeviceErrorCodes.NotSupported;
        public const int InvalidConfigData = AstraSharedConstants.DeviceErrorCodes.InvalidConfigData;
        public const int ConfigNotFound = AstraSharedConstants.DeviceErrorCodes.ConfigNotFound;

        // 工作流相关 (8000-8999)
        public const int ExecutionFailed = AstraSharedConstants.DeviceErrorCodes.ExecutionFailed;
        public const int NotFound = AstraSharedConstants.DeviceErrorCodes.NotFound; // 通用"未找到"错误码，适用于工作流、节点等
    }
}