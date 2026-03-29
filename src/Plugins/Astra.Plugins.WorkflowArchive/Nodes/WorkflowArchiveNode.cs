using Astra.Core.Archiving;
using Astra.Core.Nodes.Management;
using Astra.Core.Nodes.Models;
using Microsoft.Extensions.DependencyInjection;
using System.ComponentModel.DataAnnotations;

namespace Astra.Plugins.WorkflowArchive.Nodes
{
    /// <summary>
    /// 在流程末尾显式触发与引擎相同的归档逻辑（成功路径下可先落盘 Raw；完整报告由引擎在释放 Raw 前补写）。
    /// </summary>
    public sealed class WorkflowArchiveNode : Node
    {
        [Display(Name = "说明", Order = 0, Description = "与引擎在 Raw 清理前调用同一 IWorkflowArchiveService；首轮通常无结果链，第二轮由引擎写入 report.html / run_record.json。")]
        public string? Note { get; set; }

        protected override async Task<ExecutionResult> ExecuteCoreAsync(NodeContext context, CancellationToken cancellationToken)
        {
            var log = context.CreateExecutionLogger($"结果归档:{Name}");
            if (context.ServiceProvider == null)
            {
                return ExecutionResult.Failed("ServiceProvider 为空，无法解析 IWorkflowArchiveService");
            }

            var svc = context.ServiceProvider.GetService(typeof(IWorkflowArchiveService)) as IWorkflowArchiveService;
            if (svc == null)
            {
                return ExecutionResult.Failed("未注册 IWorkflowArchiveService");
            }

            WorkFlowRunRecord? runRecord = null;
            if (context.ServiceProvider.GetService(typeof(IWorkFlowManager)) is IWorkFlowManager mgr &&
                !string.IsNullOrWhiteSpace(context.ExecutionId))
            {
                var rr = mgr.GetWorkFlowRunRecord(context.ExecutionId);
                if (rr.Success && rr.Data != null)
                {
                    runRecord = rr.Data;
                }
            }

            var wfKey = context.GetMetadata<string>("WorkFlowKey", null) ?? string.Empty;
            var request = new WorkflowArchiveRequest
            {
                Trigger = WorkflowArchiveTrigger.WorkflowNode,
                ExecutionId = context.ExecutionId ?? string.Empty,
                WorkFlowKey = wfKey,
                WorkFlowName = context.ParentWorkFlow?.Name ?? string.Empty,
                NodeContext = context,
                ExecutionStatus = null,
                RunRecord = runRecord
            };

            try
            {
                var ar = await svc.ArchiveAsync(request, cancellationToken).ConfigureAwait(false);
                var msg = ar.Message ?? (ar.Success ? "OK" : "失败");
                log.Info($"归档: {msg}，目录={ar.OutputDirectory}");
                return ExecutionResult.Successful(msg);
            }
            catch (Exception ex)
            {
                log.Error($"归档异常: {ex.Message}");
                return ExecutionResult.Failed($"归档异常: {ex.Message}", ex);
            }
        }
    }
}
