using System.Threading;
using System.Threading.Tasks;

namespace Astra.Core.Nodes.Management
{
    /// <summary>
    /// 工作流执行控制接口，提供暂停/恢复/取消协作能力。
    /// </summary>
    public interface IWorkflowExecutionController
    {
        bool IsPaused { get; }

        CancellationToken Token { get; }

        void Pause();

        void Resume();

        void Cancel();

        Task WaitIfPausedAsync(CancellationToken cancellationToken);
    }
}
