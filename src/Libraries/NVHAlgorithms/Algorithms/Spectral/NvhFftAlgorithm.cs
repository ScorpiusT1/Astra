using System.Numerics;
using NVHAlgorithms.Abstractions;
using NVHAlgorithms.Algorithms.Internal;
using NVHAlgorithms.Axes;
using NVHAlgorithms.Transforms;

namespace NVHAlgorithms.Algorithms.Spectral;

public sealed class NvhFftAlgorithm : INvhAlgorithm<NvhFftInput, NvhFftOutput>
{
    public NvhAlgorithmDescriptor Descriptor { get; } = new(
        AlgorithmId: "NVH.Spectral.Fft",
        DisplayName: "FFT (C#)",
        SchemaVersion: 1,
        TestlabFeatureHint: "FFT",
        SupportsProgress: true);

    public Task<NvhAlgorithmResult<NvhFftOutput>> RunAsync(
        NvhFftInput input,
        NvhRunOptions? options,
        CancellationToken cancellationToken)
    {
        return Task.Run(() => RunCore(input, options, cancellationToken), cancellationToken);
    }

    private static NvhAlgorithmResult<NvhFftOutput> RunCore(
        NvhFftInput input,
        NvhRunOptions? options,
        CancellationToken cancellationToken)
    {
        var samples = input.Samples;
        if (samples.Length == 0)
            return NvhAlgorithmResult<NvhFftOutput>.Fail(new ArgumentException("样本为空。"));

        if (!NvhDoubleFft.IsPowerOfTwo(samples.Length))
            return NvhAlgorithmResult<NvhFftOutput>.Fail(new ArgumentException("样本长度须为 2 的幂。"));

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
        else if (!NvhNumericGuard.Apply(samples, input.NonFinitePolicy, out var err) && err != null)
            return NvhAlgorithmResult<NvhFftOutput>.Fail(err);

        options?.Progress?.Report(new NvhProgress(0.1, "FFT"));

        var spectrum = new Complex[samples.Length];
        try
        {
            NvhDoubleFft.Forward(samples.AsSpan(), spectrum.AsSpan(), new NvhFftOptions(), cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return NvhAlgorithmResult<NvhFftOutput>.Canceled();
        }

        options?.Progress?.Report(new NvhProgress(1, "FFT"));

        double df = input.Signal.SampleRateHz / samples.Length;
        var freq = new double[samples.Length];
        for (int i = 0; i < freq.Length; i++)
            freq[i] = i * df;

        var axis = new NvhAxisDescriptor
        {
            Kind = NvhAxisKind.FrequencyHz,
            Label = "Frequency",
            Coordinates = freq,
            PhysicalUnit = "Hz",
        };

        return NvhAlgorithmResult<NvhFftOutput>.Ok(new NvhFftOutput
        {
            Spectrum = spectrum,
            FrequencyAxis = axis,
        });
    }
}
