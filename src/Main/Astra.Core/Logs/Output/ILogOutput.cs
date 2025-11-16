namespace Astra.Core.Logs.Output
{
    /// <summary>
    /// 日志输出接口
    /// 定义日志输出的抽象，支持多种输出方式（文件、控制台、数据库等）
    /// </summary>
    public interface ILogOutput
    {
        /// <summary>
        /// 输出日志条目
        /// </summary>
        /// <param name="entry">日志条目</param>
        void Write(LogEntry entry);

        /// <summary>
        /// 刷新输出缓冲区（如果适用）
        /// </summary>
        void Flush();

        /// <summary>
        /// 释放资源
        /// </summary>
        void Dispose();
    }
}

