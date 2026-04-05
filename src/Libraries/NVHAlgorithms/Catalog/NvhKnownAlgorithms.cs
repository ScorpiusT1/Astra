using NVHAlgorithms.Abstractions;
using NVHAlgorithms.Algorithms.Deferred;
using NVHAlgorithms.Algorithms.Envelope;
using NVHAlgorithms.Algorithms.Octave;
using NVHAlgorithms.Algorithms.SoundLevel;
using NVHAlgorithms.Algorithms.Spectral;

namespace NVHAlgorithms.Catalog;

/// <summary>已登记算法描述符（用于 UI/日志/Testlab 映射表维护）。</summary>
public static class NvhKnownAlgorithms
{
    public static IReadOnlyList<NvhAlgorithmDescriptor> All { get; } =
    [
        new NvhFftAlgorithm().Descriptor,
        new NvhAutopowerSpectrumAlgorithm().Descriptor,
        new NvhWaveletStubAlgorithm().Descriptor,
        new NvhWaveletSliceStubAlgorithm().Descriptor,
        new NvhModulationStubAlgorithm().Descriptor,
        new NvhHilbertEnvelopeAlgorithm().Descriptor,
        new NvhEnvelopeSpectrumAlgorithm().Descriptor,
        new NvhThirdOctaveApproxAlgorithm().Descriptor,
        new NvhOverallLevelAlgorithm().Descriptor,
        new NvhSoundQualityStubAlgorithm().Descriptor,
        new NvhOrderTrackingStubAlgorithm().Descriptor,
        new NvhOrderSliceStubAlgorithm().Descriptor,
    ];
}
