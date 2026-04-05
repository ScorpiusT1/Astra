using NVHAlgorithms.Abstractions;
using NVHAlgorithms.Axes;

namespace NVHAlgorithms.Algorithms.Envelope;

public sealed class NvhEnvelopeInput
{
    public required double[] Samples { get; init; }

    public required NvhSignalDescriptor Signal { get; init; }
}

public sealed class NvhEnvelopeOutput
{
    public required double[] Envelope { get; init; }

    public required NvhAxisDescriptor TimeAxis { get; init; }
}

public sealed class NvhEnvelopeSpectrumInput
{
    public required double[] Envelope { get; init; }

    public required NvhSignalDescriptor Signal { get; init; }
}

public sealed class NvhEnvelopeSpectrumOutput
{
    public required double[] Autopower { get; init; }

    public required NvhAxisDescriptor FrequencyAxis { get; init; }
}
