using System.Numerics;
using NVHAlgorithms.Abstractions;
using NVHAlgorithms.Axes;
using NVHAlgorithms.Transforms;

namespace NVHAlgorithms.Algorithms.Octave;

/// <summary>
/// 由 FFT 自功率近似 1/3 倍频程带通能量（矩形带，非 IEC 61260 完整滤波器链）。
/// 用于管线占位与后续替换为合规实现。
/// </summary>
public sealed class NvhThirdOctaveApproxInput
{
    public required double[] Samples { get; init; }

    public required NvhSignalDescriptor Signal { get; init; }
}

public sealed class NvhThirdOctaveApproxOutput
{
    public required double[] BandPower { get; init; }

    public required NvhAxisDescriptor BandCenterFrequencyAxis { get; init; }
}

public sealed class NvhThirdOctaveApproxAlgorithm : INvhAlgorithm<NvhThirdOctaveApproxInput, NvhThirdOctaveApproxOutput>
{
    public NvhAlgorithmDescriptor Descriptor { get; } = new(
        AlgorithmId: "NVH.Octave.ThirdApprox",
        DisplayName: "1/3 倍频程 (FFT 近似)",
        SchemaVersion: 1,
        TestlabFeatureHint: "Octave / 1/3",
        SupportsProgress: true);

    public Task<NvhAlgorithmResult<NvhThirdOctaveApproxOutput>> RunAsync(
        NvhThirdOctaveApproxInput input,
        NvhRunOptions? options,
        CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            var s = input.Samples;
            if (s.Length == 0 || !NvhDoubleFft.IsPowerOfTwo(s.Length))
                return NvhAlgorithmResult<NvhThirdOctaveApproxOutput>.Fail(new ArgumentException("样本须为 2 的幂。"));

            var spec = new Complex[s.Length];
            try
            {
                NvhDoubleFft.Forward(s.AsSpan(), spec.AsSpan(), new NvhFftOptions(), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return NvhAlgorithmResult<NvhThirdOctaveApproxOutput>.Canceled();
            }

            var ap = new double[s.Length];
            for (int i = 0; i < ap.Length; i++)
                ap[i] = spec[i].Magnitude * spec[i].Magnitude;

            double df = input.Signal.SampleRateHz / s.Length;
            double nyq = input.Signal.SampleRateHz / 2;

            const double fRef = 1000;
            var centers = new List<double>();
            for (int band = -20; band <= 20; band++)
            {
                double fc = fRef * Math.Pow(2, band / 3.0);
                if (fc < df || fc >= nyq * 0.98)
                    continue;
                centers.Add(fc);
            }

            var power = new double[centers.Count];
            for (int b = 0; b < centers.Count; b++)
            {
                double fc = centers[b];
                double fLo = fc / Math.Pow(2, 1.0 / 6);
                double fHi = fc * Math.Pow(2, 1.0 / 6);
                int k0 = Math.Max(1, (int)Math.Floor(fLo / df));
                int k1 = Math.Min(s.Length / 2, (int)Math.Ceiling(fHi / df));
                double sum = 0;
                for (int k = k0; k <= k1; k++)
                    sum += ap[k];
                power[b] = sum;
            }

            return NvhAlgorithmResult<NvhThirdOctaveApproxOutput>.Ok(new NvhThirdOctaveApproxOutput
            {
                BandPower = power,
                BandCenterFrequencyAxis = new NvhAxisDescriptor
                {
                    Kind = NvhAxisKind.BandThirdOctave,
                    Label = "1/3 octave fc",
                    Coordinates = centers.ToArray(),
                    PhysicalUnit = "Hz",
                },
            });
        }, cancellationToken);
    }
}
