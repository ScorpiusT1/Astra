namespace NVHAlgorithms.Abstractions;

/// <summary>
/// NVH 算法入口。实现须在长循环中协作响应 <paramref name="cancellationToken"/>。
/// </summary>
public interface INvhAlgorithm<TIn, TOut>
{
    NvhAlgorithmDescriptor Descriptor { get; }

    Task<NvhAlgorithmResult<TOut>> RunAsync(
        TIn input,
        NvhRunOptions? options,
        CancellationToken cancellationToken);
}
