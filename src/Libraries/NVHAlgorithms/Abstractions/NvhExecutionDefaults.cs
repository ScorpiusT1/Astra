namespace NVHAlgorithms.Abstractions;

/// <summary>库级默认执行参数（可被 <see cref="NvhParallelOptions"/> / <see cref="NvhRunOptions"/> 覆盖）。</summary>
public static class NvhExecutionDefaults
{
    /// <summary>每算法任务默认墙钟超时（独立计时）。</summary>
    public static readonly TimeSpan AlgorithmTimeout = TimeSpan.FromSeconds(30);

    /// <summary>默认最大并行任务数。</summary>
    public static int DefaultMaxDegreeOfParallelism => Environment.ProcessorCount;
}
