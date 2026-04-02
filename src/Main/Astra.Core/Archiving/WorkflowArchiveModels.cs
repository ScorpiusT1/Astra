using Astra.Core.Nodes.Management;
using Astra.Core.Nodes.Models;
using Astra.Core.Reporting;

namespace Astra.Core.Archiving
{
    /// <summary>
    /// 归档触发来源：引擎在释放 Raw 存储前，或流程中的显式归档节点。
    /// </summary>
    public enum WorkflowArchiveTrigger
    {
        EngineBeforeRawCleanup = 0,
        WorkflowNode = 1
    }

    /// <summary>
    /// 单次工作流归档请求（节点与引擎共用）。
    /// </summary>
    public sealed class WorkflowArchiveRequest
    {
        public WorkflowArchiveTrigger Trigger { get; init; }

        public string ExecutionId { get; init; } = string.Empty;

        public string WorkFlowKey { get; init; } = string.Empty;

        public string WorkFlowName { get; init; } = string.Empty;

        /// <summary>执行期上下文；引擎与节点调用时均应提供。</summary>
        public NodeContext? NodeContext { get; init; }

        public WorkFlowExecutionStatus? ExecutionStatus { get; init; }

        /// <summary>
        /// 完整结果链；仅在引擎写入 <see cref="IWorkFlowManager"/> 后可用。
        /// 流程内节点触发时通常为空，第二轮由引擎补写报告与 JSON。
        /// </summary>
        public WorkFlowRunRecord? RunRecord { get; init; }

        /// <summary>
        /// 报告单值/曲线/图表过滤；为 null 时与「全部写入」一致（由 <see cref="ITestReportGenerator"/> 使用默认）。
        /// </summary>
        public ReportGenerationOptions? ReportOptions { get; init; }
    }

    /// <summary>
    /// 归档结果摘要。
    /// </summary>
    public sealed class WorkflowArchiveResult
    {
        public bool Success { get; init; }

        public string? Message { get; init; }

        public string? OutputDirectory { get; init; }

        /// <summary>本轮未执行任何写入（例如无 Raw 且无结果链）。</summary>
        public bool Skipped { get; init; }

        public bool WroteRawArtifacts { get; init; }

        public bool WroteRunRecordArtifacts { get; init; }
    }
}
