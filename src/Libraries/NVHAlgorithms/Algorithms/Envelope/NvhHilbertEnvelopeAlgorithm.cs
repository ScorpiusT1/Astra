using System.Numerics;
using NVHAlgorithms.Abstractions;
using NVHAlgorithms.Axes;
using NVHAlgorithms.Transforms;

namespace NVHAlgorithms.Algorithms.Envelope;

/// <summary>基于解析信号幅值的包络（频域 Hilbert）；长度须为 2 的幂。</summary>
public sealed class NvhHilbertEnvelopeAlgorithm : INvhAlgorithm<NvhEnvelopeInput, NvhEnvelopeOutput>
{
    public NvhAlgorithmDescriptor Descriptor { get; } = new(
        AlgorithmId: "NVH.Envelope.Hilbert",
        DisplayName: "包络线 (Hilbert)",
        SchemaVersion: 1,
        TestlabFeatureHint: "Envelope",
        SupportsProgress: true);

    public Task<NvhAlgorithmResult<NvhEnvelopeOutput>> RunAsync(
        NvhEnvelopeInput input,
        NvhRunOptions? options,
        CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            var s = input.Samples;
            if (s.Length == 0 || !NvhDoubleFft.IsPowerOfTwo(s.Length))
                return NvhAlgorithmResult<NvhEnvelopeOutput>.Fail(new ArgumentException("样本须为 2 的幂长度。"));

            var analytic = new Complex[s.Length];
            try
            {
                NvhAnalyticSignal.Compute(s.AsSpan(), analytic.AsSpan(), new NvhFftOptions(), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return NvhAlgorithmResult<NvhEnvelopeOutput>.Canceled();
            }

            var env = new double[s.Length];
            for (int i = 0; i < env.Length; i++)
                env[i] = analytic[i].Magnitude;

            double dt = 1.0 / input.Signal.SampleRateHz;
            var t = new double[s.Length];
            for (int i = 0; i < t.Length; i++)
                t[i] = i * dt;

            return NvhAlgorithmResult<NvhEnvelopeOutput>.Ok(new NvhEnvelopeOutput
            {
                Envelope = env,
                TimeAxis = new NvhAxisDescriptor
                {
                    Kind = NvhAxisKind.TimeSeconds,
                    Label = "Time",
                    Coordinates = t,
                    PhysicalUnit = "s",
                },
            });
        }, cancellationToken);
    }
}
