namespace NVHAlgorithms.Axes;

/// <summary>输出轴定义，便于与 Testlab 导出列对齐。</summary>
public sealed class NvhAxisDescriptor
{
    public required NvhAxisKind Kind { get; init; }

    public required string Label { get; init; }

    /// <summary>单调坐标（Hz、阶次、s 等）。</summary>
    public required double[] Coordinates { get; init; }

    public string? PhysicalUnit { get; init; }
}
