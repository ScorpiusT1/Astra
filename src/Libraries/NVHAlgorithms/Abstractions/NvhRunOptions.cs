namespace NVHAlgorithms.Abstractions;

/// <summary>单次算法调用选项（缓冲所有权由具体算法文档约定）。</summary>
public sealed class NvhRunOptions
{
    /// <summary>覆盖默认的每任务超时；<see langword="null"/> 表示使用 <see cref="NvhExecutionDefaults.AlgorithmTimeout"/>。</summary>
    public TimeSpan? Timeout { get; init; }

    /// <summary>不附加库默认超时，仅使用外部 <see cref="CancellationToken"/>。</summary>
    public bool SuppressDefaultTimeout { get; init; }

    public IProgress<NvhProgress>? Progress { get; init; }
}
