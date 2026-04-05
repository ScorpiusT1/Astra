using System.Numerics;
using NVHAlgorithms.Abstractions;
using NVHAlgorithms.Algorithms.Internal;
using NVHAlgorithms.Axes;
using NVHAlgorithms.Transforms;

namespace NVHAlgorithms.Algorithms.Spectral;

/// <summary>
/// 自功率谱：按 scipy.signal.periodogram 等价标定（|X|²/Σw²，单边内点×2），
/// 支持单块或 Welch 滑窗平均（与 Simcenter Testlab 重叠/稳态平均概念对齐）；
/// 并对齐 Power / Linear / PSD 与 Peak/RMS/Pk-Pk 幅值模式。
/// </summary>
public sealed class NvhAutopowerSpectrumAlgorithm : INvhAlgorithm<NvhAutopowerInput, NvhAutopowerOutput>
{
    public NvhAlgorithmDescriptor Descriptor { get; } = new(
        AlgorithmId: "NVH.Spectral.Autopower",
        DisplayName: "自功率谱",
        SchemaVersion: 6,
        TestlabFeatureHint: "Spectral / Autopower (Welch overlap; Power, Linear, PSD)",
        SupportsProgress: true);

    public Task<NvhAlgorithmResult<NvhAutopowerOutput>> RunAsync(
        NvhAutopowerInput input,
        NvhRunOptions? options,
        CancellationToken cancellationToken)
    {
        return Task.Run(() => RunCore(input, options, cancellationToken), cancellationToken);
    }

    private static NvhAlgorithmResult<NvhAutopowerOutput> RunCore(
        NvhAutopowerInput input,
        NvhRunOptions? options,
        CancellationToken cancellationToken)
    {
        var samples = input.Samples;
        if (samples.Length == 0)
            return NvhAlgorithmResult<NvhAutopowerOutput>.Fail(new ArgumentException("样本为空。"));

        if (input.NonFinitePolicy == NvhNonFinitePolicy.ReplaceWithZero)
        {
            var copy = (double[])samples.Clone();
            NvhNumericGuard.ReplaceNonFinite(copy);
            samples = copy;
        }
        else if (input.NonFinitePolicy == NvhNonFinitePolicy.ReplaceWithNaN)
        {
            var copy = (double[])samples.Clone();
            NvhNumericGuard.ReplaceNonFiniteWithNaN(copy);
            samples = copy;
        }
        else if (!NvhNumericGuard.Apply(samples, input.NonFinitePolicy, out var err0) && err0 != null)
            return NvhAlgorithmResult<NvhAutopowerOutput>.Fail(err0);

        double fs = input.Signal.SampleRateHz;
        if (fs <= 0)
            return NvhAlgorithmResult<NvhAutopowerOutput>.Fail(new ArgumentException("SampleRateHz 须大于 0。"));

        int n = input.FftSegmentLength > 0 ? input.FftSegmentLength : samples.Length;
        if (!NvhDoubleFft.IsPowerOfTwo(n))
            return NvhAlgorithmResult<NvhAutopowerOutput>.Fail(new ArgumentException("FFT 段长须为 2 的幂（请设置 FftSegmentLength 或使样本长度为 2 的幂）。"));

        if (input.BlockMode == NvhAutopowerBlockMode.WelchAverage && input.FftSegmentLength <= 0)
            return NvhAlgorithmResult<NvhAutopowerOutput>.Fail(new ArgumentException("Welch 模式须指定 FftSegmentLength（2 的幂）。"));

        if (input.BlockMode == NvhAutopowerBlockMode.FirstBlockOnly)
        {
            var seg = ExtractSegment(samples, 0, n);
            if (input.RemoveBlockMean)
                SubtractMeanInPlace(seg.AsSpan());
            return BuildOutputFromSegment(seg, input, fs, cancellationToken);
        }

        // Welch：仅对整段上完整长度为 n 的窗做平均；末尾不足 n 的样本丢弃（与 scipy welch 默认整段对齐常见行为一致）。
        int hop;
        if (input.WelchStrideMode == NvhAutopowerWelchStrideMode.TimeIncrementSeconds)
        {
            double dt = input.WelchTimeIncrementSeconds ?? double.NaN;
            if (double.IsNaN(dt) || double.IsInfinity(dt) || dt <= 0)
                return NvhAlgorithmResult<NvhAutopowerOutput>.Fail(new ArgumentException("WelchTimeIncrementSeconds 须为有限正数。"));

            hop = (int)Math.Round(dt * fs);
            if (hop < 1 || hop > n)
                return NvhAlgorithmResult<NvhAutopowerOutput>.Fail(
                    new ArgumentException($"Welch 时间步进折算样本步 hop={hop} 须满足 1 ≤ hop ≤ FFT 段长 N={n}。"));
        }
        else
        {
            double overlapFr = input.WelchOverlapFraction ?? 0.5;
            if (double.IsNaN(overlapFr) || double.IsInfinity(overlapFr))
                return NvhAlgorithmResult<NvhAutopowerOutput>.Fail(new ArgumentException("WelchOverlapFraction 无效。"));
            overlapFr = Math.Clamp(overlapFr, 0.0, 1.0);
            int noverlap = overlapFr <= 0 ? 0 : Math.Min(n - 1, (int)Math.Round(n * overlapFr));
            hop = Math.Max(1, n - noverlap);
        }

        int len = samples.Length;
        if (len < n)
        {
            var seg = ExtractSegment(samples, 0, n);
            if (input.RemoveBlockMean)
                SubtractMeanInPlace(seg.AsSpan());
            return BuildOutputFromSegment(seg, input, fs, cancellationToken);
        }

        int half = input.SingleSided ? n / 2 + 1 : n;
        var sum = new double[half];
        int count = 0;
        for (int start = 0; start + n <= len; start += hop)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var seg = ExtractSegment(samples, start, n);
            if (input.RemoveBlockMean)
                SubtractMeanInPlace(seg.AsSpan());

            var step = TryComputeOneSidedPowerBeforeFormat(seg, input, fs, cancellationToken, out var os);
            if (step.State != NvhTaskCompletionState.Succeeded || os is null)
                return step.State == NvhTaskCompletionState.Canceled
                    ? NvhAlgorithmResult<NvhAutopowerOutput>.Canceled()
                    : NvhAlgorithmResult<NvhAutopowerOutput>.Fail(step.Error ?? new InvalidOperationException("Welch 段计算失败。"));

            for (int i = 0; i < half; i++)
                sum[i] += os[i];
            count++;
        }

        if (count <= 0)
            return NvhAlgorithmResult<NvhAutopowerOutput>.Fail(new ArgumentException("无可用 Welch 段。"));

        double inv = 1.0 / count;
        for (int i = 0; i < half; i++)
            sum[i] *= inv;

        double df = fs / n;
        double[] outY = ApplySpectrumFormat(sum, input.SpectrumFormat, df);
        double[] freq = new double[half];
        for (int k = 0; k < half; k++)
            freq[k] = k * df;

        return NvhAlgorithmResult<NvhAutopowerOutput>.Ok(new NvhAutopowerOutput
        {
            Autopower = outY,
            FrequencyResolutionHz = df,
            ValueAxisUnitHint = GetValueUnitHint(input.SpectrumFormat),
            FrequencyAxis = new NvhAxisDescriptor
            {
                Kind = NvhAxisKind.FrequencyHz,
                Label = "Frequency",
                Coordinates = freq,
                PhysicalUnit = "Hz",
            },
        });
    }

    private static NvhAlgorithmResult<NvhAutopowerOutput> BuildOutputFromSegment(
        double[] segmentN,
        NvhAutopowerInput input,
        double fs,
        CancellationToken cancellationToken)
    {
        var step = TryComputeOneSidedPowerBeforeFormat(segmentN, input, fs, cancellationToken, out var os);
        if (step.State != NvhTaskCompletionState.Succeeded || os is null)
            return step.State == NvhTaskCompletionState.Canceled
                ? NvhAlgorithmResult<NvhAutopowerOutput>.Canceled()
                : NvhAlgorithmResult<NvhAutopowerOutput>.Fail(step.Error ?? new InvalidOperationException("自功率谱计算失败。"));

        int n = segmentN.Length;
        double df = fs / n;
        double[] outY = ApplySpectrumFormat(os, input.SpectrumFormat, df);
        int half = os.Length;
        var freq = new double[half];
        for (int k = 0; k < half; k++)
            freq[k] = k * df;

        return NvhAlgorithmResult<NvhAutopowerOutput>.Ok(new NvhAutopowerOutput
        {
            Autopower = outY,
            FrequencyResolutionHz = df,
            ValueAxisUnitHint = GetValueUnitHint(input.SpectrumFormat),
            FrequencyAxis = new NvhAxisDescriptor
            {
                Kind = NvhAxisKind.FrequencyHz,
                Label = "Frequency",
                Coordinates = freq,
                PhysicalUnit = "Hz",
            },
        });
    }

    /// <summary>单边或双边「功率」谱线（未做 PSD/Linear 变换）。</summary>
    private static NvhAlgorithmResult<double[]> TryComputeOneSidedPowerBeforeFormat(
        double[] segmentN,
        NvhAutopowerInput input,
        double fs,
        CancellationToken cancellationToken,
        out double[]? oneSidedOrFullPower)
    {
        oneSidedOrFullPower = null;
        int n = segmentN.Length;
        if (!NvhDoubleFft.IsPowerOfTwo(n))
            return NvhAlgorithmResult<double[]>.Fail(new ArgumentException("段长须为 2 的幂。"));

        var windowed = new double[n];
        double sumW2 = NvhAutopowerWindowUtil.ApplyAndAccumulateSumW2(segmentN.AsSpan(), windowed.AsSpan(), input.FftWindow);
        if (sumW2 <= 0)
            return NvhAlgorithmResult<double[]>.Fail(new ArgumentException("窗能量 Σw² 无效。"));

        var spec = new Complex[n];
        try
        {
            NvhDoubleFft.Forward(windowed.AsSpan(), spec.AsSpan(), new NvhFftOptions(), cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return NvhAlgorithmResult<double[]>.Canceled();
        }

        double ampPowerFactor = input.AmplitudeMode switch
        {
            NvhAutopowerAmplitudeMode.Peak => 1.0,
            NvhAutopowerAmplitudeMode.Rms => 0.5,
            NvhAutopowerAmplitudeMode.PeakToPeak => 4.0,
            _ => 1.0,
        };

        double invSumW2 = 1.0 / sumW2;
        var twoSidedPower = new double[n];
        for (int i = 0; i < n; i++)
        {
            double m = spec[i].Magnitude;
            twoSidedPower[i] = m * m * invSumW2 * ampPowerFactor;
        }

        if (input.SingleSided)
        {
            int half = n / 2 + 1;
            var os = new double[half];
            os[0] = twoSidedPower[0];
            if ((n & 1) == 0)
            {
                for (int k = 1; k < n / 2; k++)
                    os[k] = 2.0 * twoSidedPower[k];
                os[n / 2] = twoSidedPower[n / 2];
            }
            else
            {
                for (int k = 1; k < half; k++)
                    os[k] = 2.0 * twoSidedPower[k];
            }

            oneSidedOrFullPower = os;
        }
        else
            oneSidedOrFullPower = twoSidedPower;

        return NvhAlgorithmResult<double[]>.Ok(oneSidedOrFullPower);
    }

    private static double[] ExtractSegment(IReadOnlyList<double> source, int start, int n)
    {
        var buf = new double[n];
        int copy = Math.Min(n, Math.Max(0, source.Count - start));
        for (int i = 0; i < copy; i++)
            buf[i] = source[start + i];
        return buf;
    }

    private static double[] ApplySpectrumFormat(double[] oneSidedOrFullPower, NvhAutopowerSpectrumFormat format, double df)
    {
        var y = new double[oneSidedOrFullPower.Length];
        switch (format)
        {
            case NvhAutopowerSpectrumFormat.Power:
                Array.Copy(oneSidedOrFullPower, y, y.Length);
                break;
            case NvhAutopowerSpectrumFormat.Psd:
                for (int i = 0; i < y.Length; i++)
                    y[i] = oneSidedOrFullPower[i] / df;
                break;
            case NvhAutopowerSpectrumFormat.Linear:
                for (int i = 0; i < y.Length; i++)
                {
                    double p = oneSidedOrFullPower[i];
                    y[i] = p >= 0 ? Math.Sqrt(p) : double.NaN;
                }

                break;
            default:
                Array.Copy(oneSidedOrFullPower, y, y.Length);
                break;
        }

        return y;
    }

    private static void SubtractMeanInPlace(Span<double> data)
    {
        if (data.Length == 0)
            return;
        double sum = 0;
        for (int i = 0; i < data.Length; i++)
            sum += data[i];
        double mean = sum / data.Length;
        for (int i = 0; i < data.Length; i++)
            data[i] -= mean;
    }

    private static string? GetValueUnitHint(NvhAutopowerSpectrumFormat format) =>
        format switch
        {
            NvhAutopowerSpectrumFormat.Power => "²",
            NvhAutopowerSpectrumFormat.Linear => string.Empty,
            NvhAutopowerSpectrumFormat.Psd => "²/Hz",
            _ => null,
        };
}
