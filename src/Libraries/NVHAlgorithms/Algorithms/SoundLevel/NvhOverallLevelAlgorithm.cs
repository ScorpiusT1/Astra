using NVHAlgorithms.Abstractions;

namespace NVHAlgorithms.Algorithms.SoundLevel;

public sealed class NvhOverallLevelInput
{
    public required double[] Samples { get; init; }

    public required NvhSignalDescriptor Signal { get; init; }
}

public sealed class NvhOverallLevelOutput
{
    /// <summary>假设样本为声压 (Pa) 时的整体声压级 dB re 20 µPa。</summary>
    public double OverallDbSpl { get; init; }
}

/// <summary>简单 RMS → dB SPL（不计权、时间计权）；与 Testlab Overall 对齐需扩展。</summary>
public sealed class NvhOverallLevelAlgorithm : INvhAlgorithm<NvhOverallLevelInput, NvhOverallLevelOutput>
{
    public NvhAlgorithmDescriptor Descriptor { get; } = new(
        AlgorithmId: "NVH.SoundLevel.Overall",
        DisplayName: "噪声/声压总值 (RMS→dB)",
        SchemaVersion: 1,
        TestlabFeatureHint: "Overall / Level",
        SupportsProgress: false);

    public Task<NvhAlgorithmResult<NvhOverallLevelOutput>> RunAsync(
        NvhOverallLevelInput input,
        NvhRunOptions? options,
        CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            var x = input.Samples;
            if (x.Length == 0)
                return NvhAlgorithmResult<NvhOverallLevelOutput>.Fail(new ArgumentException("样本为空。"));

            double sum = 0;
            for (int i = 0; i < x.Length; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                sum += x[i] * x[i];
            }

            double rms = Math.Sqrt(sum / x.Length);
            double p0 = input.Signal.ReferencePressurePa;
            if (p0 <= 0)
                return NvhAlgorithmResult<NvhOverallLevelOutput>.Fail(new ArgumentException("参考声压无效。"));

            double db = 20 * Math.Log10(rms / p0 + 1e-30);
            return NvhAlgorithmResult<NvhOverallLevelOutput>.Ok(new NvhOverallLevelOutput { OverallDbSpl = db });
        }, cancellationToken);
    }
}
