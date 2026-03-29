using Astra.Core.Triggers;
using System.Threading;
using System.Threading.Tasks;

namespace Astra.Services.Home
{
    /// <summary>
    /// Home 独立执行入口：由服务层统一调度主流程中的子流程执行。
    /// </summary>
    public interface IHomeWorkflowExecutionService : IConfiguredMasterWorkflowExecutor
    {
        /// <param name="manualBarcode">
        /// Home 手动扫码模式下输入的条码；写入各子流程 <see cref="Astra.Core.Nodes.Models.NodeContext"/> 的全局变量 <c>SN</c>。
        /// 自动模式传 null；PLC 自动触发时可传入触发 SN（如 IO 名称）。
        /// </param>
        Task ExecuteCurrentConfiguredMasterAsync(CancellationToken cancellationToken, string? manualBarcode = null);

    }
}
