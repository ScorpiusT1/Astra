using System;
using System.Threading.Tasks;

namespace Astra.Core.Logs.Output
{
    /// <summary>
    /// 异步日志输出接口
    /// 提供异步的日志输出方法，避免阻塞线程
    /// 
    /// 设计说明：
    /// - 不继承 ILogOutput，作为独立接口，符合接口隔离原则
    /// - 实现类可以选择只实现 IAsyncLogOutput（纯异步），或同时实现两个接口（支持同步和异步）
    /// - Logger 会通过类型检查自动选择使用同步或异步方法
    /// </summary>
    public interface IAsyncLogOutput : IDisposable
    {
        /// <summary>
        /// 异步输出日志条目
        /// </summary>
        /// <param name="entry">日志条目</param>
        /// <returns>异步任务</returns>
        Task WriteAsync(LogEntry entry);

        /// <summary>
        /// 异步刷新输出缓冲区（如果适用）
        /// </summary>
        /// <returns>异步任务</returns>
        Task FlushAsync();
    }
}

