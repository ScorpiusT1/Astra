namespace Astra.Core.Logs
{
    /// <summary>
    /// 日志模块错误码
    /// 用于标识日志相关的错误类型
    /// </summary>
    public static class LogErrorCodes
    {
        // 配置相关 (9000-9099)
        /// <summary>
        /// 配置无效
        /// </summary>
        public const int InvalidConfig = 9001;

        /// <summary>
        /// 日志文件路径无效
        /// </summary>
        public const int InvalidFilePath = 9002;

        /// <summary>
        /// 日志级别无效
        /// </summary>
        public const int InvalidLogLevel = 9003;

        // 输出相关 (9100-9199)
        /// <summary>
        /// 日志输出失败
        /// </summary>
        public const int OutputFailed = 9101;

        /// <summary>
        /// 日志格式化失败
        /// </summary>
        public const int FormatFailed = 9102;

        // 队列相关 (9200-9299)
        /// <summary>
        /// 日志队列已满
        /// </summary>
        public const int QueueFull = 9201;

        /// <summary>
        /// 日志队列处理失败
        /// </summary>
        public const int QueueProcessFailed = 9202;
    }
}

