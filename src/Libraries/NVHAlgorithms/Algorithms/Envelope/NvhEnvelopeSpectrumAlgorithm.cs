using System.Numerics;
using NVHAlgorithms.Abstractions;
using NVHAlgorithms.Axes;
using NVHAlgorithms.Transforms;

namespace NVHAlgorithms.Algorithms.Envelope;

public sealed class NvhEnvelopeSpectrumAlgorithm : INvhAlgorithm<NvhEnvelopeSpectrumInput, NvhEnvelopeSpectrumOutput>
{
    public NvhAlgorithmDescriptor Descriptor { get; } = new(
        AlgorithmId: "NVH.Envelope.Spectrum",
        DisplayName: "包络谱",
        SchemaVersion: 1,
        TestlabFeatureHint: "Envelope spectrum",
        SupportsProgress: true);

    public Task<NvhAlgorithmResult<NvhEnvelopeSpectrumOutput>> RunAsync(
        NvhEnvelopeSpectrumInput input,
        NvhRunOptions? options,
        CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            var e = input.Envelope;
            if (e.Length == 0 || !NvhDoubleFft.IsPowerOfTwo(e.Length))
                return NvhAlgorithmResult<NvhEnvelopeSpectrumOutput>.Fail(new ArgumentException("包络序列须为 2 的幂长度。"));

            var spec = new Complex[e.Length];
            try
            {
                NvhDoubleFft.Forward(e.AsSpan(), spec.AsSpan(), new NvhFftOptions(), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return NvhAlgorithmResult<NvhEnvelopeSpectrumOutput>.Canceled();
            }

            var ap = new double[e.Length];
            for (int i = 0; i < ap.Length; i++)
                ap[i] = spec[i].Magnitude * spec[i].Magnitude;

            double df = input.Signal.SampleRateHz / e.Length;
            var f = new double[e.Length];
            for (int i = 0; i < f.Length; i++)
                f[i] = i * df;

            return NvhAlgorithmResult<NvhEnvelopeSpectrumOutput>.Ok(new NvhEnvelopeSpectrumOutput
            {
                Autopower = ap,
                FrequencyAxis = new NvhAxisDescriptor
                {
                    Kind = NvhAxisKind.FrequencyHz,
                    Label = "Frequency",
                    Coordinates = f,
                    PhysicalUnit = "Hz",
                },
            });
        }, cancellationToken);
    }
}
