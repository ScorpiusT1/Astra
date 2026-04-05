using System;
using System.Threading;
using System.Threading.Tasks;

namespace Astra.Core.Orchestration
{
    public interface IMasterWorkflowOrchestrator
    {
        event EventHandler<SubWorkflowProgressEventArgs>? SubWorkflowProgressChanged;

        Task<MasterExecutionResult> ExecuteAsync(
            MasterExecutionPlan plan,
            MasterExecutionOptions options,
            CancellationToken cancellationToken);
    }
}
