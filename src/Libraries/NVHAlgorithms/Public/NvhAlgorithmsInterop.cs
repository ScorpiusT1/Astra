using System.Numerics;
using NVHAlgorithms.Abstractions;
using NVHAlgorithms.Algorithms.Envelope;
using NVHAlgorithms.Algorithms.Octave;
using NVHAlgorithms.Algorithms.SoundLevel;
using NVHAlgorithms.Algorithms.Spectral;
using NVHAlgorithms.Axes;

namespace NVHAlgorithms;

/// <summary>
/// 供宿主/脚本/插件调用的门面：公开 API 仅使用 .NET 内置类型（标量、数组、字符串、元组），
/// 不暴露本库自定义 DTO。整型枚举码见各 <c>*Code</c> 常量。
/// 插件节点应优先使用 <c>*Async</c> 方法以支持取消。
/// </summary>
public static class NvhAlgorithmsInterop
{
    #region Spectrum format (spectrumFormat)

    public const int SpectrumFormatPower = 0;
    public const int SpectrumFormatLinear = 1;
    public const int SpectrumFormatPsd = 2;

    #endregion

    #region FFT window (fftWindow)

    public const int FftWindowUniform = 0;
    public const int FftWindowHanning = 1;

    #endregion

    #region Amplitude mode (amplitudeMode)

    public const int AmplitudePeak = 0;
    public const int AmplitudeRms = 1;
    public const int AmplitudePeakToPeak = 2;

    #endregion

    #region Non-finite policy (nonFinitePolicy)

    public const int NonFiniteReject = 0;
    public const int NonFiniteReplaceWithZero = 1;
    public const int NonFiniteSkipBlock = 2;

    /// <summary>将非有限值替换为 <see cref="double.NaN"/>；与插件节点默认策略一致。</summary>
    public const int NonFiniteReplaceWithNaN = 3;

    #endregion

    #region Autopower block mode (blockMode)

    /// <summary>仅首块 FFT（与旧版一致）。</summary>
    public const int AutopowerBlockFirstOnly = 0;

    /// <summary>整段 Welch 滑窗平均，须指定 <paramref name="fftSegmentLength"/>。</summary>
    public const int AutopowerBlockWelch = 1;

    /// <summary>Welch 步进由 <paramref name="welchOverlapFraction"/>（及段长 N）决定。</summary>
    public const int AutopowerWelchStrideOverlap = 0;

    /// <summary>Welch 步进由 <paramref name="welchTimeIncrementSeconds"/> 与采样率决定；等效重叠 = (N−hop)/N。</summary>
    public const int AutopowerWelchStrideTimeIncrement = 1;

    #endregion

    #region Autopower

    /// <summary>
    /// 计算自功率谱（单边/双边、Power/Linear/PSD、窗与幅值模式与库内算法一致）。
    /// </summary>
    /// <param name="removeBlockMean">true 时各 FFT 段加窗前减段内均值。</param>
    /// <param name="blockMode"><see cref="AutopowerBlockFirstOnly"/> 或 <see cref="AutopowerBlockWelch"/>。</param>
    /// <param name="welchOverlapFraction">
    /// Welch 相邻段重叠比例 <b>0–1 小数</b>；单块模式忽略。
    /// 为 <see cref="double.NaN"/> 时由算法使用默认 0.5（与未在节点填写重叠时一致）。
    /// 仅当 <paramref name="welchStrideMode"/> 为 <see cref="AutopowerWelchStrideOverlap"/> 时参与 hop 计算。
    /// </param>
    /// <param name="fftSegmentLength">FFT 段长（2 的幂）。Welch 必填；单块为 0 时表示使用 <paramref name="samples"/>.Length。</param>
    /// <param name="welchStrideMode"><see cref="AutopowerWelchStrideOverlap"/> 或 <see cref="AutopowerWelchStrideTimeIncrement"/>；单块忽略。</param>
    /// <param name="welchTimeIncrementSeconds">Welch 相邻段起点时间间隔（秒）；须有限且 &gt;0。为 <see cref="double.NaN"/> 表示未使用。仅 <see cref="AutopowerWelchStrideTimeIncrement"/> 时必填。</param>
    public static async Task<(bool Ok, double[]? FrequencyHz, double[]? SpectrumValues, double FrequencyResolutionHz, string? ValueAxisUnitHint, string? ErrorMessage)>
        TryAutopowerSpectrumAsync(
            double[] samples,
            double sampleRateHz,
            int spectrumFormat,
            int fftWindow,
            int amplitudeMode,
            bool singleSided,
            int nonFinitePolicy,
            bool removeBlockMean,
            int blockMode,
            double welchOverlapFraction,
            int fftSegmentLength,
            int welchStrideMode = AutopowerWelchStrideOverlap,
            double welchTimeIncrementSeconds = double.NaN,
            CancellationToken cancellationToken = default)
    {
        if (samples is null)
            return (false, null, null, 0, null, "samples 不能为 null。");

        if (!TryMapSpectrumFormat(spectrumFormat, out var fmt, out var err) ||
            !TryMapFftWindow(fftWindow, out var win, out err) ||
            !TryMapAmplitudeMode(amplitudeMode, out var amp, out err) ||
            !TryMapNonFinitePolicy(nonFinitePolicy, out var nfp, out err))
            return (false, null, null, 0, null, err);

        if (!TryMapAutopowerBlockMode(blockMode, out var bm, out err))
            return (false, null, null, 0, null, err);

        if (!TryMapWelchStrideMode(welchStrideMode, out var stride, out err))
            return (false, null, null, 0, null, err);

        if (stride == NvhAutopowerWelchStrideMode.OverlapFraction)
        {
            if (!double.IsNaN(welchOverlapFraction) && (welchOverlapFraction < 0 || welchOverlapFraction > 1))
                return (false, null, null, 0, null, "welchOverlapFraction 须在 [0,1] 内，或使用 NaN 表示默认重叠 0.5。");
        }
        else
        {
            if (double.IsNaN(welchTimeIncrementSeconds) || double.IsInfinity(welchTimeIncrementSeconds) || welchTimeIncrementSeconds <= 0)
                return (false, null, null, 0, null, "welchStrideMode 为按时间步进时，welchTimeIncrementSeconds 须为有限正数。");
        }

        var input = new NvhAutopowerInput
        {
            Samples = samples,
            Signal = new NvhSignalDescriptor
            {
                SampleRateHz = sampleRateHz,
                SampleCount = samples.Length,
            },
            NonFinitePolicy = nfp,
            SpectrumFormat = fmt,
            FftWindow = win,
            AmplitudeMode = amp,
            SingleSided = singleSided,
            RemoveBlockMean = removeBlockMean,
            BlockMode = bm,
            WelchOverlapFraction = double.IsNaN(welchOverlapFraction) ? null : welchOverlapFraction,
            FftSegmentLength = fftSegmentLength,
            WelchStrideMode = stride,
            WelchTimeIncrementSeconds = double.IsNaN(welchTimeIncrementSeconds) ? null : welchTimeIncrementSeconds,
        };

        var algo = new NvhAutopowerSpectrumAlgorithm();
        var result = await algo.RunAsync(input, null, cancellationToken).ConfigureAwait(false);

        if (result.State != NvhTaskCompletionState.Succeeded || result.Value is null)
            return (false, null, null, 0, null, result.Error?.Message ?? "自功率谱计算失败。");

        var v = result.Value;
        return (true, v.FrequencyAxis.Coordinates, v.Autopower, v.FrequencyResolutionHz, v.ValueAxisUnitHint, null);
    }

    /// <summary>
    /// 同步版（不支持中途取消）；脚本/简易宿主使用。Welch 步进固定为按重叠率。
    /// </summary>
    public static bool TryAutopowerSpectrum(
        double[] samples,
        double sampleRateHz,
        int spectrumFormat,
        int fftWindow,
        int amplitudeMode,
        bool singleSided,
        int nonFinitePolicy,
        bool removeBlockMean,
        int blockMode,
        double welchOverlapFraction,
        int fftSegmentLength,
        out double[]? frequencyHz,
        out double[]? spectrumValues,
        out double frequencyResolutionHz,
        out string? valueAxisUnitHint,
        out string? errorMessage) =>
        TryAutopowerSpectrum(
            samples,
            sampleRateHz,
            spectrumFormat,
            fftWindow,
            amplitudeMode,
            singleSided,
            nonFinitePolicy,
            removeBlockMean,
            blockMode,
            welchOverlapFraction,
            fftSegmentLength,
            AutopowerWelchStrideOverlap,
            double.NaN,
            out frequencyHz,
            out spectrumValues,
            out frequencyResolutionHz,
            out valueAxisUnitHint,
            out errorMessage);

    /// <summary>
    /// 同步版；可指定 Welch 按重叠率或按时间步进（与 <see cref="TryAutopowerSpectrumAsync"/> 一致）。
    /// </summary>
    public static bool TryAutopowerSpectrum(
        double[] samples,
        double sampleRateHz,
        int spectrumFormat,
        int fftWindow,
        int amplitudeMode,
        bool singleSided,
        int nonFinitePolicy,
        bool removeBlockMean,
        int blockMode,
        double welchOverlapFraction,
        int fftSegmentLength,
        int welchStrideMode,
        double welchTimeIncrementSeconds,
        out double[]? frequencyHz,
        out double[]? spectrumValues,
        out double frequencyResolutionHz,
        out string? valueAxisUnitHint,
        out string? errorMessage)
    {
        var t = TryAutopowerSpectrumAsync(
            samples,
            sampleRateHz,
            spectrumFormat,
            fftWindow,
            amplitudeMode,
            singleSided,
            nonFinitePolicy,
            removeBlockMean,
            blockMode,
            welchOverlapFraction,
            fftSegmentLength,
            welchStrideMode,
            welchTimeIncrementSeconds,
            CancellationToken.None).GetAwaiter().GetResult();
        frequencyHz = t.FrequencyHz;
        spectrumValues = t.SpectrumValues;
        frequencyResolutionHz = t.FrequencyResolutionHz;
        valueAxisUnitHint = t.ValueAxisUnitHint;
        errorMessage = t.ErrorMessage;
        return t.Ok;
    }

    /// <summary>
    /// 与 <see cref="TryAutopowerSpectrum"/> 相同，失败时抛出 <see cref="InvalidOperationException"/>。
    /// </summary>
    public static (double[] FrequencyHz, double[] Spectrum, double FrequencyResolutionHz, string? ValueAxisUnitHint)
        ComputeAutopowerSpectrum(
            double[] samples,
            double sampleRateHz,
            int spectrumFormat,
            int fftWindow,
            int amplitudeMode,
            bool singleSided,
            int nonFinitePolicy,
            bool removeBlockMean = false,
            int blockMode = AutopowerBlockFirstOnly,
            double welchOverlapFraction = double.NaN,
            int fftSegmentLength = 0,
            int welchStrideMode = AutopowerWelchStrideOverlap,
            double welchTimeIncrementSeconds = double.NaN)
    {
        if (!TryAutopowerSpectrum(
                samples,
                sampleRateHz,
                spectrumFormat,
                fftWindow,
                amplitudeMode,
                singleSided,
                nonFinitePolicy,
                removeBlockMean,
                blockMode,
                welchOverlapFraction,
                fftSegmentLength,
                welchStrideMode,
                welchTimeIncrementSeconds,
                out var f,
                out var y,
                out var df,
                out var hint,
                out var err) || f is null || y is null)
            throw new InvalidOperationException(err ?? "自功率谱计算失败。");

        return (f, y, df, hint);
    }

    #endregion

    #region FFT magnitude

    /// <summary>
    /// 对实序列做 FFT，输出幅值谱；<paramref name="singleSided"/> 为 true 时仅保留 0…Nyquist（与 Testlab 频谱图常见显示一致）。
    /// </summary>
    public static async Task<(bool Ok, double[]? FrequencyHz, double[]? Magnitude, string? ErrorMessage)> TryFftMagnitudeSpectrumAsync(
        double[] samples,
        double sampleRateHz,
        int nonFinitePolicy,
        bool singleSided,
        CancellationToken cancellationToken = default)
    {
        if (samples is null)
            return (false, null, null, "samples 不能为 null。");

        if (!TryMapNonFinitePolicy(nonFinitePolicy, out var nfp, out var err))
            return (false, null, null, err);

        var input = new NvhFftInput
        {
            Samples = samples,
            Signal = new NvhSignalDescriptor { SampleRateHz = sampleRateHz, SampleCount = samples.Length },
            NonFinitePolicy = nfp,
        };

        var algo = new NvhFftAlgorithm();
        var result = await algo.RunAsync(input, null, cancellationToken).ConfigureAwait(false);
        if (result.State != NvhTaskCompletionState.Succeeded || result.Value is null)
            return (false, null, null, result.Error?.Message ?? "FFT 失败。");

        var v = result.Value;
        var mag = MagnitudeSpectrum(v.Spectrum);
        var freq = v.FrequencyAxis.Coordinates ?? Array.Empty<double>();
        if (!singleSided)
            return (true, freq, mag, null);

        var (xf, yf) = ToSingleSided(freq, mag);
        return (true, xf, yf, null);
    }

    #endregion

    #region Hilbert envelope

    public static async Task<(bool Ok, double[]? TimeSeconds, double[]? Envelope, string? ErrorMessage)> TryHilbertEnvelopeAsync(
        double[] samples,
        double sampleRateHz,
        CancellationToken cancellationToken = default)
    {
        if (samples is null)
            return (false, null, null, "samples 不能为 null。");

        var input = new NvhEnvelopeInput
        {
            Samples = samples,
            Signal = new NvhSignalDescriptor { SampleRateHz = sampleRateHz, SampleCount = samples.Length },
        };

        var algo = new NvhHilbertEnvelopeAlgorithm();
        var result = await algo.RunAsync(input, null, cancellationToken).ConfigureAwait(false);
        if (result.State != NvhTaskCompletionState.Succeeded || result.Value is null)
            return (false, null, null, result.Error?.Message ?? "Hilbert 包络失败。");

        var v = result.Value;
        return (true, v.TimeAxis.Coordinates, v.Envelope, null);
    }

    #endregion

    #region Envelope spectrum (Hilbert + FFT autopower of envelope)

    /// <summary>
    /// 先 Hilbert 包络，再对包络做 FFT 自功率；<paramref name="singleSided"/> 控制是否截到 Nyquist 显示。
    /// </summary>
    public static async Task<(bool Ok, double[]? FrequencyHz, double[]? Autopower, string? ErrorMessage)> TryEnvelopeSpectrumAsync(
        double[] samples,
        double sampleRateHz,
        bool singleSided,
        CancellationToken cancellationToken = default)
    {
        var envStep = await TryHilbertEnvelopeAsync(samples, sampleRateHz, cancellationToken).ConfigureAwait(false);
        if (!envStep.Ok || envStep.Envelope is null)
            return (false, null, null, envStep.ErrorMessage ?? "包络失败。");

        var specInput = new NvhEnvelopeSpectrumInput
        {
            Envelope = envStep.Envelope,
            Signal = new NvhSignalDescriptor { SampleRateHz = sampleRateHz, SampleCount = envStep.Envelope.Length },
        };

        var specAlgo = new NvhEnvelopeSpectrumAlgorithm();
        var specResult = await specAlgo.RunAsync(specInput, null, cancellationToken).ConfigureAwait(false);
        if (specResult.State != NvhTaskCompletionState.Succeeded || specResult.Value is null)
            return (false, null, null, specResult.Error?.Message ?? "包络谱失败。");

        var v = specResult.Value;
        var f = v.FrequencyAxis.Coordinates ?? Array.Empty<double>();
        var ap = v.Autopower;
        if (!singleSided)
            return (true, f, ap, null);

        var (xf, yf) = ToSingleSided(f, ap);
        return (true, xf, yf, null);
    }

    #endregion

    #region 1/3 octave (FFT approx)

    public static async Task<(bool Ok, double[]? BandCenterHz, double[]? BandPower, string? ErrorMessage)> TryThirdOctaveApproxAsync(
        double[] samples,
        double sampleRateHz,
        CancellationToken cancellationToken = default)
    {
        if (samples is null)
            return (false, null, null, "samples 不能为 null。");

        var input = new NvhThirdOctaveApproxInput
        {
            Samples = samples,
            Signal = new NvhSignalDescriptor { SampleRateHz = sampleRateHz, SampleCount = samples.Length },
        };

        var algo = new NvhThirdOctaveApproxAlgorithm();
        var result = await algo.RunAsync(input, null, cancellationToken).ConfigureAwait(false);
        if (result.State != NvhTaskCompletionState.Succeeded || result.Value is null)
            return (false, null, null, result.Error?.Message ?? "1/3 倍频程近似失败。");

        var v = result.Value;
        return (true, v.BandCenterFrequencyAxis.Coordinates, v.BandPower, null);
    }

    #endregion

    #region Overall SPL

    /// <summary>
    /// 假设样本为声压 (Pa)，计算整体声压级 dB re <paramref name="referencePressurePa"/>（Simcenter 常用 20 µPa = 2e-5）。
    /// </summary>
    public static async Task<(bool Ok, double OverallDbSpl, string? ErrorMessage)> TryOverallSplDbAsync(
        double[] samples,
        double sampleRateHz,
        double referencePressurePa,
        CancellationToken cancellationToken = default)
    {
        if (samples is null)
            return (false, 0, "samples 不能为 null。");

        var input = new NvhOverallLevelInput
        {
            Samples = samples,
            Signal = new NvhSignalDescriptor
            {
                SampleRateHz = sampleRateHz,
                SampleCount = samples.Length,
                ChannelCount = 1,
                Interleaved = false,
                QuantityKind = NvhSignalQuantityKind.SoundPressurePa,
                ReferencePressurePa = referencePressurePa,
            },
        };

        var algo = new NvhOverallLevelAlgorithm();
        var result = await algo.RunAsync(input, null, cancellationToken).ConfigureAwait(false);
        if (result.State != NvhTaskCompletionState.Succeeded || result.Value is null)
            return (false, 0, result.Error?.Message ?? "整体声压级计算失败。");

        return (true, result.Value.OverallDbSpl, null);
    }

    #endregion

    private static (double[] X, double[] Y) ToSingleSided(double[] fullAxis, double[] fullY)
    {
        int n = fullY.Length;
        int half = n / 2 + 1;
        var x = new double[half];
        var y = new double[half];
        Array.Copy(fullAxis, x, half);
        Array.Copy(fullY, y, half);
        return (x, y);
    }

    private static double[] MagnitudeSpectrum(Complex[] spectrum)
    {
        var m = new double[spectrum.Length];
        for (int i = 0; i < spectrum.Length; i++)
            m[i] = spectrum[i].Magnitude;
        return m;
    }

    private static bool TryMapSpectrumFormat(int code, out NvhAutopowerSpectrumFormat format, out string? err)
    {
        err = null;
        format = default;
        switch (code)
        {
            case SpectrumFormatPower:
                format = NvhAutopowerSpectrumFormat.Power;
                return true;
            case SpectrumFormatLinear:
                format = NvhAutopowerSpectrumFormat.Linear;
                return true;
            case SpectrumFormatPsd:
                format = NvhAutopowerSpectrumFormat.Psd;
                return true;
            default:
                err = $"spectrumFormat 无效：{code}（期望 {SpectrumFormatPower}..{SpectrumFormatPsd}）。";
                return false;
        }
    }

    private static bool TryMapFftWindow(int code, out NvhAutopowerWindowKind kind, out string? err)
    {
        err = null;
        kind = default;
        switch (code)
        {
            case FftWindowUniform:
                kind = NvhAutopowerWindowKind.Uniform;
                return true;
            case FftWindowHanning:
                kind = NvhAutopowerWindowKind.Hanning;
                return true;
            default:
                err = $"fftWindow 无效：{code}（期望 {FftWindowUniform} 或 {FftWindowHanning}）。";
                return false;
        }
    }

    private static bool TryMapAmplitudeMode(int code, out NvhAutopowerAmplitudeMode mode, out string? err)
    {
        err = null;
        mode = default;
        switch (code)
        {
            case AmplitudePeak:
                mode = NvhAutopowerAmplitudeMode.Peak;
                return true;
            case AmplitudeRms:
                mode = NvhAutopowerAmplitudeMode.Rms;
                return true;
            case AmplitudePeakToPeak:
                mode = NvhAutopowerAmplitudeMode.PeakToPeak;
                return true;
            default:
                err = $"amplitudeMode 无效：{code}（期望 {AmplitudePeak}..{AmplitudePeakToPeak}）。";
                return false;
        }
    }

    private static bool TryMapAutopowerBlockMode(int code, out NvhAutopowerBlockMode mode, out string? err)
    {
        err = null;
        mode = default;
        switch (code)
        {
            case AutopowerBlockFirstOnly:
                mode = NvhAutopowerBlockMode.FirstBlockOnly;
                return true;
            case AutopowerBlockWelch:
                mode = NvhAutopowerBlockMode.WelchAverage;
                return true;
            default:
                err = $"blockMode 无效：{code}（期望 {AutopowerBlockFirstOnly} 或 {AutopowerBlockWelch}）。";
                return false;
        }
    }

    private static bool TryMapWelchStrideMode(int code, out NvhAutopowerWelchStrideMode mode, out string? err)
    {
        err = null;
        mode = default;
        switch (code)
        {
            case AutopowerWelchStrideOverlap:
                mode = NvhAutopowerWelchStrideMode.OverlapFraction;
                return true;
            case AutopowerWelchStrideTimeIncrement:
                mode = NvhAutopowerWelchStrideMode.TimeIncrementSeconds;
                return true;
            default:
                err = $"welchStrideMode 无效：{code}（期望 {AutopowerWelchStrideOverlap} 或 {AutopowerWelchStrideTimeIncrement}）。";
                return false;
        }
    }

    private static bool TryMapNonFinitePolicy(int code, out NvhNonFinitePolicy policy, out string? err)
    {
        err = null;
        policy = default;
        switch (code)
        {
            case NonFiniteReject:
                policy = NvhNonFinitePolicy.Reject;
                return true;
            case NonFiniteReplaceWithZero:
                policy = NvhNonFinitePolicy.ReplaceWithZero;
                return true;
            case NonFiniteSkipBlock:
                policy = NvhNonFinitePolicy.SkipBlock;
                return true;
            case NonFiniteReplaceWithNaN:
                policy = NvhNonFinitePolicy.ReplaceWithNaN;
                return true;
            default:
                err = $"nonFinitePolicy 无效：{code}（期望 {NonFiniteReject}..{NonFiniteReplaceWithNaN}）。";
                return false;
        }
    }
}
