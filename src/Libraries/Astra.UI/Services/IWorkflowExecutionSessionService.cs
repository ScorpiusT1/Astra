using Astra.Core.Foundation.Common;
using Astra.Core.Nodes.Models;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Astra.UI.Services
{
    public interface IWorkflowExecutionSessionService
    {
        event EventHandler<WorkflowNodeExecutionChangedEventArgs> NodeExecutionChanged;

        bool IsRunning { get; }

        string CurrentExecutionId { get; }

        Task<WorkflowExecutionSessionStartResult> StartAsync(
            string workflowKey,
            WorkFlowNode workflow,
            NodeContext context,
            CancellationToken cancellationToken = default);

        OperationResult Pause();

        OperationResult Resume();

        OperationResult Stop();

        /// <summary>
        /// 暂停当前会话已启动且仍跟踪的全部运行中工作流（多路并行时逐个暂停）。
        /// </summary>
        OperationResult PauseAllTrackedSessions();

        /// <summary>
        /// 恢复当前会话已启动且仍跟踪的全部已暂停工作流。
        /// </summary>
        OperationResult ResumeAllTrackedSessions();
    }

    public sealed class WorkflowNodeExecutionChangedEventArgs : EventArgs
    {
        public string ExecutionId { get; init; }

        public string WorkflowKey { get; init; }

        public string NodeId { get; init; }

        public NodeExecutionState State { get; init; }

        /// <summary>
        /// 节点完成或运行中时附带的说明（错误、跳过原因、成功附加信息等）；由引擎 <see cref="ExecutionResult"/> 汇总。
        /// </summary>
        public string? DetailMessage { get; init; }

        /// <summary>
        /// 结构化 UI 载荷（来自 <c>ExecutionResult.OutputData</c> 中 <see cref="Astra.Core.Nodes.Ui.NodeUiOutputKeys"/> 约定键）。
        /// </summary>
        public IReadOnlyDictionary<string, object>? UiPayload { get; init; }
    }

    public sealed class WorkflowExecutionSessionStartResult
    {
        public bool Success { get; init; }

        public string Message { get; init; } = string.Empty;

        public string ExecutionId { get; init; }

        public Task<OperationResult<ExecutionResult>> ExecutionTask { get; init; }
    }
}
