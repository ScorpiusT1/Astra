using System.Numerics;
using NVHAlgorithms.Abstractions;
using NVHAlgorithms.Axes;

namespace NVHAlgorithms.Algorithms.Spectral;

public sealed class NvhFftInput
{
    public required double[] Samples { get; init; }

    public required NvhSignalDescriptor Signal { get; init; }

    public NvhNonFinitePolicy NonFinitePolicy { get; init; } = NvhNonFinitePolicy.ReplaceWithNaN;
}

public sealed class NvhFftOutput
{
    public required Complex[] Spectrum { get; init; }

    public required NvhAxisDescriptor FrequencyAxis { get; init; }
}

public sealed class NvhAutopowerInput
{
    public required double[] Samples { get; init; }

    public required NvhSignalDescriptor Signal { get; init; }

    public NvhNonFinitePolicy NonFinitePolicy { get; init; } = NvhNonFinitePolicy.ReplaceWithNaN;

    /// <summary>默认 Power；与 Testlab Autopower Power / Linear / PSD 对齐。</summary>
    public NvhAutopowerSpectrumFormat SpectrumFormat { get; init; } = NvhAutopowerSpectrumFormat.Power;

    /// <summary>默认 Hanning，与 Testlab 常见在线处理一致；Uniform 可复现旧版矩形窗行为。</summary>
    public NvhAutopowerWindowKind FftWindow { get; init; } = NvhAutopowerWindowKind.Hanning;

    public NvhAutopowerAmplitudeMode AmplitudeMode { get; init; } = NvhAutopowerAmplitudeMode.Peak;

    /// <summary>
    /// true（默认）：单边 0…Nyquist，内点对功率×2，与 Simcenter Testlab / NVH 自功率显示一致；false 为完整 FFT 索引（实信号会在 Nyquist 两侧镜像，仅作诊断）。
    /// </summary>
    public bool SingleSided { get; init; } = true;

    /// <summary>
    /// true：在加窗前对本块样本减块均值（去直流），抑制 0 Hz 尖峰，便于与 Testlab 等 AC 耦合频谱显示习惯一致；false：保留直流分量。
    /// </summary>
    public bool RemoveBlockMean { get; init; }

    /// <summary>
    /// <see cref="NvhAutopowerBlockMode.FirstBlockOnly"/>：仅用首块；
    /// <see cref="NvhAutopowerBlockMode.WelchAverage"/>：整段 <see cref="Samples"/> 上滑窗平均。
    /// </summary>
    public NvhAutopowerBlockMode BlockMode { get; init; } = NvhAutopowerBlockMode.FirstBlockOnly;

    /// <summary>
    /// 每段 FFT 长度 N，须为 2 的幂。0 表示使用 <see cref="Samples"/> 的长度（须为 2 的幂），用于单块模式。
    /// Welch 模式下须显式指定 N 且 <see cref="Samples"/>.Length ≥ N（不足 N 时退化为单块补零）。
    /// </summary>
    public int FftSegmentLength { get; init; }

    /// <summary>
    /// Welch 相邻段重叠比例，<b>0–1 小数</b>（如 0.5≈段长一半重叠）。为 null 时使用算法默认 0.5。
    /// 仅 <see cref="BlockMode"/> 为 Welch 且 <see cref="WelchStrideMode"/> 为 <see cref="NvhAutopowerWelchStrideMode.OverlapFraction"/> 时用作步进依据。
    /// </summary>
    public double? WelchOverlapFraction { get; init; }

    /// <summary>
    /// Welch 步进模式；默认按重叠率（与 Testlab Free Run 显式重叠一致）。按时间步进时与 Simcenter「Time 跟踪」下由 increment 与帧长共同决定重叠的思路一致。
    /// </summary>
    public NvhAutopowerWelchStrideMode WelchStrideMode { get; init; } = NvhAutopowerWelchStrideMode.OverlapFraction;

    /// <summary>
    /// 按时间指定相邻两段起点间隔 Δt（秒）；hop = round(Δt·fs)，须满足 1 ≤ hop ≤ N。
    /// 仅 <see cref="BlockMode"/> 为 Welch 且 <see cref="WelchStrideMode"/> 为 <see cref="NvhAutopowerWelchStrideMode.TimeIncrementSeconds"/> 时使用。
    /// </summary>
    public double? WelchTimeIncrementSeconds { get; init; }
}

public sealed class NvhAutopowerOutput
{
    public required double[] Autopower { get; init; }

    public required NvhAxisDescriptor FrequencyAxis { get; init; }

    /// <summary>本块频率分辨率 Δf = fs/N，与 PSD 换算一致。</summary>
    public double FrequencyResolutionHz { get; init; }

    /// <summary>纵轴物理单位提示（如 空 / ² / ²/Hz），便于宿主绘图。</summary>
    public string? ValueAxisUnitHint { get; init; }
}
