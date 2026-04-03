namespace Astra.Core.Constants
{
    /// <summary>
    /// 跨 Engine / Plugins / Core 复用的公共常量。
    /// </summary>
    public static class AstraSharedConstants
    {
        public static class MetadataKeys
        {
            public const string UiLogWriter = "UiLogWriter";
            public const string RawDataStore = "RawDataStore";
            public const string WorkflowExecutionController = "WorkflowExecutionController";
            public const string ExecutionId = "ExecutionId";
            public const string WorkFlowKey = "WorkFlowKey";
            public const string TestDataBus = "TestDataBus";
        }

        public static class WorkflowOutputKeys
        {
            public const string SkipReason = "SkipReason";
            public const string ExecutionStrategy = "ExecutionStrategy";
        }

        public static class WorkflowOutputValues
        {
            public const string Disabled = "Disabled";
            public const string BlockedByUpstream = "BlockedByUpstream";
        }

        public static class DesignTimeLabels
        {
            public const string Unselected = "未选择";
            public const string UseFirstChannelInGroup = "（默认：组内首通道）";
            public const string DefaultPlaybackDevice = "默认";
        }

        public static class DataGroups
        {
            public const string Signal = "Signal";
        }

        public static class SerializationErrorCodes
        {
            public const int InvalidData = 1001;
            public const int FileNotFound = 1002;
            public const int FileSaveFailed = 1003;
            public const int FileLoadFailed = 1004;
            public const int SerializationFailed = 1005;
            public const int DeserializationFailed = 1006;
            public const int InvalidJson = 1007;
        }

        public static class ConfigDefaults
        {
            public const string DefaultConfigTypeSuffix = "Config";
        }

        public static class SecurityDefaults
        {
            public const int MinPasswordLength = 6;
        }

        public static class EngineDefaults
        {
            public const int RawDataTtlMinutes = 10;
            public const int RawDataMaxItems = 2000;
            public const long RawDataMaxBytes = 128L * 1024L * 1024L;
        }

        public static class DataAcquisitionDefaults
        {
            public const string CodeDefinedChartXAxisLabel = "时间";
            public const string CodeDefinedChartXAxisUnit = "s";
            /// <summary>TDMS 波形通道 wf_xunit_string 默认值（中文单位）。</summary>
            public const string CodeDefinedWfXUnitString = "秒";
            public const string CodeDefinedChartYAxisLabel = "幅值";
            public const string CodeDefinedChartYAxisUnitFallback = "";
            public const int DelaySliceMs = 100;
            public const int PublishQueueCapacity = 256;
            public const string BrcSdkDllPath = "Lib/x64/brc_daq_sdk.dll";
            public const int BrcSdkBufferSize = 1024;
            public const int BrcSdkArrayBufferSize = 512;
        }

        public static class AudioDefaults
        {
            public const int ChunkSamples = 8192;
        }

        public static class PlcDefaults
        {
            public const string NewIoNamePrefix = "NewIO #";
        }

        /// <summary>
        /// 文件导入类节点发布的 Raw 使用与多采集相同的「设备显示名 → DeviceId」规则；
        /// 数据采集插件中的 <c>DataAcquisitionCardProvider</c> 将 <see cref="DisplayName"/> 解析为 <see cref="DeviceId"/>。
        /// </summary>
        public static class VirtualImportDevices
        {
            /// <summary>属性面板与算法节点中可选的虚拟采集卡名称。</summary>
            public const string DisplayName = "文件导入";

            /// <summary>对应 Raw 工件键中的设备段：<c>{DeviceId}:raw</c>。</summary>
            public const string DeviceId = "astra-virtual-file-import";
        }

        public static class DeviceErrorCodes
        {
            public const int ConnectFailed = 1001;
            public const int DisconnectFailed = 1002;
            public const int ResetFailed = 1003;
            public const int DeviceNotFound = 1004;
            public const int DeviceNoResponse = 1005;
            public const int DeviceNotOnline = 1006;
            public const int DeviceInUse = 1007;

            public const int HeartbeatAlreadyRunning = 2001;
            public const int HeartbeatNotRunning = 2002;
            public const int HeartbeatTimeout = 2003;
            public const int HeartbeatError = 2004;

            public const int SendFailed = 3001;
            public const int ReceiveFailed = 3002;
            public const int ReceiveTimeout = 3003;
            public const int ChannelMismatch = 3004;
            public const int InvalidData = 3005;

            public const int AcquisitionAlreadyRunning = 4001;
            public const int AcquisitionNotRunning = 4002;
            public const int AcquisitionError = 4003;

            public const int BufferEmpty = 5001;
            public const int BufferFull = 5002;
            public const int BufferReadError = 5003;
            public const int BufferWriteError = 5004;

            public const int ChannelNotFound = 6001;
            public const int ChannelDisabled = 6002;
            public const int ChannelError = 6003;

            public const int InvalidConfig = 7001;
            public const int ConfigRequireRestart = 7002;
            public const int ConfigApplyFailed = 7003;
            public const int ConfigSaveFailed = 7004;
            public const int ConfigLoadFailed = 7005;
            public const int FileNotFound = 7006;
            public const int NotSupported = 7007;
            public const int InvalidConfigData = 7008;
            public const int ConfigNotFound = 7009;

            public const int ExecutionFailed = 8001;
            public const int NotFound = 8002;
        }
    }
}
