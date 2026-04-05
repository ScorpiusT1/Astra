using System;
using System.Threading;
using System.Threading.Tasks;

namespace Astra.Core.Nodes.Management
{
    /// <summary>
    /// 工作流执行控制接口，提供暂停/恢复/取消协作能力。
    /// </summary>
    public interface IWorkflowExecutionController
    {
        /// <summary>
        /// 用户请求暂停后、门闩已关闭时触发；节点可在此停表、停硬件等（须在订阅方自行保证线程安全）。
        /// </summary>
        event EventHandler? Pausing;

        /// <summary>
        /// 用户请求继续且门闩已打开后触发；与 <see cref="WaitIfPausedAsync"/> 返回的顺序因调度可能交错，节点应幂等处理。
        /// </summary>
        event EventHandler? Resumed;

        /// <summary>
        /// 首次请求取消且 <see cref="Token"/> 已进入取消状态、暂停门闩已打开后触发；重复调用 <see cref="Cancel"/> 不会再次触发。
        /// 节点可在此做同步收尾；协作式取消仍以 <see cref="Token"/> 为准。
        /// </summary>
        event EventHandler? Cancelling;

        bool IsPaused { get; }

        CancellationToken Token { get; }

        void Pause();

        void Resume();

        void Cancel();

        Task WaitIfPausedAsync(CancellationToken cancellationToken);
    }
}
