namespace Astra.Core.Logs
{
    /// <summary>
    /// 日志级别枚举
    /// 定义日志的严重程度，数值越大表示越严重
    /// </summary>
    public enum LogLevel
    {
        /// <summary>
        /// 调试级别 - 详细的调试信息，通常只在开发时使用
        /// </summary>
        Debug = 10,

        /// <summary>
        /// 信息级别 - 一般信息，记录程序正常运行的关键信息
        /// </summary>
        Info = 20,

        /// <summary>
        /// 警告级别 - 警告信息，表示潜在的问题但不影响程序运行
        /// </summary>
        Warning = 30,

        /// <summary>
        /// 错误级别 - 错误信息，表示程序发生了错误但仍可继续运行
        /// </summary>
        Error = 40,

        /// <summary>
        /// 严重级别 - 严重错误，可能导致程序崩溃或无法继续运行
        /// </summary>
        Critical = 50
    }
}
