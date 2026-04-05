namespace Astra.Core.Nodes.Models
{
    /// <summary>
    /// 上游节点「测试是否通过」类逻辑的统一语义，供逻辑聚合、报表等复用。
    /// <list type="bullet">
    /// <item><description><b>严格通过</b>：<see cref="ExecutionResult.Success"/> 且非 <see cref="ExecutionResult.IsSkipped"/>（视为有效测试项通过）。</description></item>
    /// <item><description><b>已执行且失败</b>：非 Success 且非 Skipped（取消/超时/验证失败等均归入此类，表示未「通过」）。</description></item>
    /// <item><description><b>跳过</b>：<see cref="ExecutionResult.IsSkipped"/> 为 true（是否计入聚合由调用方策略决定）。</description></item>
    /// </list>
    /// </summary>
    public static class UpstreamTestResultSemantics
    {
        /// <summary>有效测试通过（不含「仅跳过」）。</summary>
        public static bool IsStrictPass(ExecutionResult? result) =>
            result != null && result.Success && !result.IsSkipped;

        /// <summary>已调度执行且结果为失败类（非跳过）。</summary>
        public static bool IsExecutedFailure(ExecutionResult? result) =>
            result != null && !result.Success && !result.IsSkipped;

        /// <summary>跳过执行。</summary>
        public static bool IsSkippedResult(ExecutionResult? result) =>
            result != null && result.IsSkipped;

        /// <summary>无执行结果（例如上游被策略阻断未运行）。</summary>
        public static bool IsMissingResult(ExecutionResult? result) => result == null;
    }
}
