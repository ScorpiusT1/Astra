namespace NVHAlgorithms.Axes;

/// <summary>非有限值处理策略（与金数据脚本一致）。</summary>
public enum NvhNonFinitePolicy
{
    /// <summary>遇 NaN/Inf 即失败。</summary>
    Reject,

    /// <summary>替换为 0。</summary>
    ReplaceWithZero,

    /// <summary>跳过含非有限值的块（算法需支持）。</summary>
    SkipBlock,

    /// <summary>将 NaN/Inf 统一替换为 <see cref="double.NaN"/>，便于下游识别无效点（插件/流程默认推荐）。</summary>
    ReplaceWithNaN,
}
