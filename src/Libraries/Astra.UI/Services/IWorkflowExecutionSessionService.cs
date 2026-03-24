using Astra.Core.Foundation.Common;
using Astra.Core.Nodes.Models;
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
    }

    public sealed class WorkflowNodeExecutionChangedEventArgs : EventArgs
    {
        public string ExecutionId { get; init; }

        public string WorkflowKey { get; init; }

        public string NodeId { get; init; }

        public NodeExecutionState State { get; init; }
    }

    public sealed class WorkflowExecutionSessionStartResult
    {
        public bool Success { get; init; }

        public string Message { get; init; } = string.Empty;

        public string ExecutionId { get; init; }

        public Task<OperationResult<ExecutionResult>> ExecutionTask { get; init; }
    }
}
