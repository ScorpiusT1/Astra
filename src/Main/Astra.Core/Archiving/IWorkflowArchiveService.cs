using System.Threading;
using System.Threading.Tasks;
using Astra.Core.Nodes.Models;

namespace Astra.Core.Archiving
{
    /// <summary>
    /// 工作流测试结果归档：TDMS、WAV、run_record.json、HTML/PDF 报告等由实现类完成；
    /// 引擎在清理 Raw 存储前、流程节点在成功路径末尾均可调用同一实现。
    /// </summary>
    public interface IWorkflowArchiveService
    {
        Task<WorkflowArchiveResult> ArchiveAsync(WorkflowArchiveRequest request, CancellationToken cancellationToken);

        /// <summary>Home 多子流程批次归档开始时调用，清空上一轮累积的合并 Raw TDMS 缓冲。</summary>
        void OnBatchArchiveStarted();

        /// <summary>批次全部子流程归档结束后，将累积的原始波形写入单个 TDMS（单组、多通道）。</summary>
        void FlushBatchCombinedRawTdms(string outputDirectory, string combinedFilePrefix);

        /// <summary>
        /// 在引擎托管执行开始时调用：按与 <see cref="ArchiveAsync"/> 相同的规则创建「报告根\测试数据\日期\SN\序号」目录并登记本次 <paramref name="executionId"/>，
        /// 使运行日志等与 TDMS/报告落在同一文件夹。返回目录绝对路径；无法分配时返回 null。
        /// </summary>
        string? AllocateRunOutputDirectoryIfNeeded(string executionId, NodeContext context);
    }
}
