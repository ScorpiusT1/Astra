namespace NVHAlgorithms.Transforms;

public sealed class NvhFftOptions
{
    public NvhFftNormalization Normalization { get; init; } = NvhFftNormalization.InverseUnitary;

    /// <summary>蝶形阶段之间检查取消的间隔（阶段数）。</summary>
    public int CancellationStrideStages { get; init; } = 2;
}
