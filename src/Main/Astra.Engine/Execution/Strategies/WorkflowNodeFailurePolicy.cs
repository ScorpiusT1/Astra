using Astra.Core.Nodes.Models;

namespace Astra.Engine.Execution.Strategies
{
    /// <summary>
    /// 与「遇错是否停止整单」相关的统一判定，避免策略里用到的 Node 引用与 <see cref="WorkFlowNode.Nodes"/> 中实例不一致
    /// （例如检测列表与画布不同步时，应以工作流图上的节点为准读取 ContinueOnFailure）。
    /// </summary>
    internal static class WorkflowNodeFailurePolicy
    {
        /// <summary>
        /// 解析本次执行应绑定的节点：始终以 <see cref="WorkFlowNode.Nodes"/> 中的实例为准，避免策略列表与图数据不是同一引用导致标志位读错。
        /// </summary>
        public static Node ResolveExecutionNode(WorkFlowNode workflow, Node node)
        {
            if (workflow == null || node == null)
            {
                return node;
            }

            if (string.IsNullOrWhiteSpace(node.Id))
            {
                return node;
            }

            return workflow.GetNode(node.Id) ?? node;
        }

        /// <summary>
        /// 某步已失败时，是否应终止后续节点执行（未勾选失败继续且开启 StopOnError）。
        /// </summary>
        public static bool ShouldAbortRemainingAfterFailedStep(WorkFlowNode workflow, Node node)
        {
            if (workflow == null || node == null)
            {
                return true;
            }

            if (!(workflow.Configuration?.StopOnError ?? true))
            {
                return false;
            }

            var graphNode = string.IsNullOrWhiteSpace(node.Id) ? null : workflow.GetNode(node.Id);
            // 任一侧引用勾选「失败继续」即不终止（兼容策略列表与 Nodes 集合引用不一致）
            var continueAfterFail = (graphNode?.ContinueOnFailure ?? false) || node.ContinueOnFailure;
            return !continueAfterFail;
        }

        /// <summary>
        /// 因上游失败被标记为阻断而跳过的节点，不释放下游（与「条件跳过」区分）。
        /// </summary>
        public const string SkipReasonBlockedByUpstream = "BlockedByUpstream";

        /// <summary>
        /// 是否在拓扑上释放指向下游的依赖边（成功、跳过、或失败但勾选失败继续时释放）。
        /// 用于并行/分层调度：并行分支上某节点失败不应挡住仅依赖其它分支的下游节点。
        /// </summary>
        public static bool ShouldReleaseDownstreamEdges(WorkFlowNode workflow, Node node, ExecutionResult result)
        {
            if (result == null)
            {
                return false;
            }

            if (result.IsSkipped &&
                result.OutputData != null &&
                result.OutputData.TryGetValue("SkipReason", out var sr) &&
                SkipReasonBlockedByUpstream.Equals(sr as string, StringComparison.Ordinal))
            {
                return false;
            }

            if (result.IsSkipped)
            {
                return true;
            }

            if (result.Success)
            {
                return true;
            }

            return !ShouldAbortRemainingAfterFailedStep(workflow, node);
        }
    }
}
