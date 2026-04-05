namespace NVHAlgorithms.Abstractions;

/// <summary><see cref="Runtime.NvhJobOrchestrator"/> 批处理选项。</summary>
public sealed class NvhParallelOptions
{
    public int MaxDegreeOfParallelism { get; init; } = NvhExecutionDefaults.DefaultMaxDegreeOfParallelism;

    /// <summary>默认 <see langword="true"/>：单任务失败时仍执行其余任务。</summary>
    public bool ContinueOnTaskFailure { get; init; } = true;

    /// <summary>批级默认超时（仍与每任务超时合成策略由编排器实现）。</summary>
    public TimeSpan? PerTaskTimeout { get; init; }

    public bool SuppressDefaultPerTaskTimeout { get; init; }
}
