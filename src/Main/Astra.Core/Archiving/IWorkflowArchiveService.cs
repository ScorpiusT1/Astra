using System.Threading;
using System.Threading.Tasks;

namespace Astra.Core.Archiving
{
    /// <summary>
    /// 工作流测试结果归档：TDMS / WAV / CSV / HTML 报告等由实现类完成；
    /// 引擎在清理 Raw 存储前、流程节点在成功路径末尾均可调用同一实现。
    /// </summary>
    public interface IWorkflowArchiveService
    {
        Task<WorkflowArchiveResult> ArchiveAsync(WorkflowArchiveRequest request, CancellationToken cancellationToken);
    }
}
