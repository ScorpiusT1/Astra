namespace NVHAlgorithms.Abstractions;

/// <summary>采样与物理量上下文，用于与 Testlab 校准/计权对齐。</summary>
public sealed class NvhSignalDescriptor
{
    public required double SampleRateHz { get; init; }

    public int ChannelCount { get; init; } = 1;

    /// <summary>每通道样本数（单通道时即总长度）。</summary>
    public required int SampleCount { get; init; }

    /// <summary>是否为交织多通道（channel-major vs sample-major 由宿主约定）。</summary>
    public bool Interleaved { get; init; }

    /// <summary>参考声压 Pa，默认 20e-6。</summary>
    public double ReferencePressurePa { get; init; } = 20e-6;

    /// <summary>可选每通道校准因子（线性，含义由 <see cref="QuantityKind"/> 约定）。</summary>
    public double[]? ChannelCalibration { get; init; }

    public NvhSignalQuantityKind QuantityKind { get; init; } = NvhSignalQuantityKind.Dimensionless;

    /// <summary>A/C/Z 等是否在输入已施加（避免重复计权）。</summary>
    public NvhFrequencyWeighting AppliedInputWeighting { get; init; } = NvhFrequencyWeighting.None;
}

public enum NvhSignalQuantityKind
{
    Dimensionless,
    SoundPressurePa,
    CalibratedEngineeringUnit,
}

public enum NvhFrequencyWeighting
{
    None,
    A,
    C,
    Z,
}
