using System.Threading;
using System.Threading.Tasks;

namespace Astra.Services.Home
{
    /// <summary>
    /// Home 独立执行入口：由服务层统一调度主流程中的子流程执行。
    /// </summary>
    public interface IHomeWorkflowExecutionService
    {
        Task ExecuteCurrentConfiguredMasterAsync(CancellationToken cancellationToken);
    }
}
