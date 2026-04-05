using NVHAlgorithms.Abstractions;

namespace NVHAlgorithms.Algorithms.Deferred;

/// <summary>占位输出：序号 3–5、10、12 等完整实现前的管线挂钩。</summary>
public sealed record NvhDeferredModule(string ModuleName);

public sealed class NvhMinimalSignalInput
{
    public required double[] Samples { get; init; }

    public required NvhSignalDescriptor Signal { get; init; }
}

public sealed class NvhWaveletStubAlgorithm : INvhAlgorithm<NvhMinimalSignalInput, NvhDeferredModule>
{
    public NvhAlgorithmDescriptor Descriptor { get; } = new(
        "NVH.Wavelet.Stub", "小波 (占位)", 1, "Wavelet", SupportsProgress: false);

    public Task<NvhAlgorithmResult<NvhDeferredModule>> RunAsync(
        NvhMinimalSignalInput input,
        NvhRunOptions? options,
        CancellationToken cancellationToken) =>
        Task.FromResult(NvhAlgorithmResult<NvhDeferredModule>.Ok(new NvhDeferredModule("Wavelet")));
}

public sealed class NvhWaveletSliceStubAlgorithm : INvhAlgorithm<NvhMinimalSignalInput, NvhDeferredModule>
{
    public NvhAlgorithmDescriptor Descriptor { get; } = new(
        "NVH.WaveletSlice.Stub", "小波切片 (占位)", 1, "Wavelet slice", SupportsProgress: false);

    public Task<NvhAlgorithmResult<NvhDeferredModule>> RunAsync(
        NvhMinimalSignalInput input,
        NvhRunOptions? options,
        CancellationToken cancellationToken) =>
        Task.FromResult(NvhAlgorithmResult<NvhDeferredModule>.Ok(new NvhDeferredModule("WaveletSlice")));
}

public sealed class NvhModulationStubAlgorithm : INvhAlgorithm<NvhMinimalSignalInput, NvhDeferredModule>
{
    public NvhAlgorithmDescriptor Descriptor { get; } = new(
        "NVH.Modulation.Stub", "调制分析 (占位)", 1, "Modulation", SupportsProgress: false);

    public Task<NvhAlgorithmResult<NvhDeferredModule>> RunAsync(
        NvhMinimalSignalInput input,
        NvhRunOptions? options,
        CancellationToken cancellationToken) =>
        Task.FromResult(NvhAlgorithmResult<NvhDeferredModule>.Ok(new NvhDeferredModule("Modulation")));
}

public sealed class NvhSoundQualityStubAlgorithm : INvhAlgorithm<NvhMinimalSignalInput, NvhDeferredModule>
{
    public NvhAlgorithmDescriptor Descriptor { get; } = new(
        "NVH.SoundQuality.Stub", "声品质 (占位)", 1, "Sound quality", SupportsProgress: false);

    public Task<NvhAlgorithmResult<NvhDeferredModule>> RunAsync(
        NvhMinimalSignalInput input,
        NvhRunOptions? options,
        CancellationToken cancellationToken) =>
        Task.FromResult(NvhAlgorithmResult<NvhDeferredModule>.Ok(new NvhDeferredModule("SoundQuality")));
}

public sealed class NvhOrderTrackingStubAlgorithm : INvhAlgorithm<NvhOrderInput, NvhDeferredModule>
{
    public NvhAlgorithmDescriptor Descriptor { get; } = new(
        "NVH.Order.Stub", "阶次 (占位)", 1, "Order tracking", SupportsProgress: false);

    public Task<NvhAlgorithmResult<NvhDeferredModule>> RunAsync(
        NvhOrderInput input,
        NvhRunOptions? options,
        CancellationToken cancellationToken) =>
        Task.FromResult(NvhAlgorithmResult<NvhDeferredModule>.Ok(new NvhDeferredModule("Order")));
}

public sealed class NvhOrderSliceStubAlgorithm : INvhAlgorithm<NvhMinimalSignalInput, NvhDeferredModule>
{
    public NvhAlgorithmDescriptor Descriptor { get; } = new(
        "NVH.OrderSlice.Stub", "阶次切片 (占位)", 1, "Order slice", SupportsProgress: false);

    public Task<NvhAlgorithmResult<NvhDeferredModule>> RunAsync(
        NvhMinimalSignalInput input,
        NvhRunOptions? options,
        CancellationToken cancellationToken) =>
        Task.FromResult(NvhAlgorithmResult<NvhDeferredModule>.Ok(new NvhDeferredModule("OrderSlice")));
}
