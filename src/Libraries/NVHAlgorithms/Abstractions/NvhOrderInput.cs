namespace NVHAlgorithms.Abstractions;

/// <summary>阶次分析输入；与 Testlab Order 模块选项对齐时扩展属性。</summary>
public sealed class NvhOrderInput
{
    /// <summary>与振动/声学通道对齐的时间轴（秒），长度与样本一致。</summary>
    public required double[] TimeSeconds { get; init; }

    /// <summary>瞬时转速曲线（RPM），与 <see cref="TimeSeconds"/> 对齐。</summary>
    public double[]? RpmProfile { get; init; }

    /// <summary>Tacho 脉冲时间戳（秒）。</summary>
    public double[]? TachoPulseTimesSeconds { get; init; }

    /// <summary>每转脉冲数（与 tacho 一起使用时）。</summary>
    public int? PulsesPerRevolution { get; init; }
}
