namespace NVHAlgorithms.Algorithms.Spectral;

/// <summary>与 Simcenter Testlab「FFT Format Conversion」中 Spectrum Format 对应。</summary>
public enum NvhAutopowerSpectrumFormat
{
    /// <summary>Autopower Power：每谱线功率（幅值²），随 Δf 变化。</summary>
    Power,

    /// <summary>Autopower Linear：sqrt(Power)，幅值量级。</summary>
    Linear,

    /// <summary>Autopower PSD：Power/Δf（如 g²/Hz），宽带随机对比用。</summary>
    Psd,
}

/// <summary>FFT 前时域窗；Uniform 等价矩形窗。</summary>
public enum NvhAutopowerWindowKind
{
    Uniform,
    Hanning,
}

/// <summary>自功率谱时域分块方式：单块或与 Welch 类似的滑窗平均。</summary>
public enum NvhAutopowerBlockMode
{
    /// <summary>仅使用从起点起第一个长度为 N 的 FFT 块（不足补零）。</summary>
    FirstBlockOnly,

    /// <summary>沿整段数据以步长 hop = N − overlap 滑窗，对单边功率周期图算术平均（Welch）。</summary>
    WelchAverage,
}

/// <summary>
/// Welch 滑窗步进如何确定：按重叠率（hop 由 N 与重叠比例推出）或按时间步进（hop=round(Δt·fs)，等效重叠=(N−hop)/N）。
/// </summary>
public enum NvhAutopowerWelchStrideMode
{
    /// <summary>使用 <see cref="NvhAutopowerInput.WelchOverlapFraction"/>（0–1 小数）；hop = N − round(N·overlap)。</summary>
    OverlapFraction,

    /// <summary>使用 <see cref="NvhAutopowerInput.WelchTimeIncrementSeconds"/>；hop = clamp(round(Δt·fs), 1..N)。</summary>
    TimeIncrementSeconds,
}

/// <summary>与 Testlab 轴上「Amplitude mode」(Peak / RMS / Peak-to-Peak) 对应的谱幅值标尺。</summary>
public enum NvhAutopowerAmplitudeMode
{
    /// <summary>正弦峰值分量 A。</summary>
    Peak,

    /// <summary>正弦 RMS 幅值 A/√2，功率为 Peak 的 1/2。</summary>
    Rms,

    /// <summary>以 2A 为线谱幅值时，功率为 Peak 的 4 倍。</summary>
    PeakToPeak,
}

/// <summary>
/// 应用窗并返回 Σw²；输出 dest[i]=input[i]*w[i]。Hanning 为对称 N 点窗，与常见分析仪一致。
/// </summary>
public static class NvhAutopowerWindowUtil
{
    public static double ApplyAndAccumulateSumW2(ReadOnlySpan<double> input, Span<double> dest, NvhAutopowerWindowKind kind)
    {
        if (input.Length != dest.Length)
            throw new ArgumentException("input 与 dest 长度须一致。");

        int n = input.Length;
        double sumW2 = 0;

        if (kind == NvhAutopowerWindowKind.Uniform)
        {
            for (int i = 0; i < n; i++)
            {
                dest[i] = input[i];
                sumW2 += 1.0;
            }

            return sumW2;
        }

        for (int i = 0; i < n; i++)
        {
            double w = n <= 1 ? 1.0 : 0.5 - 0.5 * Math.Cos(2 * Math.PI * i / (n - 1));
            sumW2 += w * w;
            dest[i] = input[i] * w;
        }

        return sumW2;
    }
}
