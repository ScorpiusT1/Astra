namespace NVHAlgorithms.Abstractions;

/// <summary>算法元数据：与 Testlab 映射、版本、默认超时覆盖。</summary>
public sealed record NvhAlgorithmDescriptor(
    string AlgorithmId,
    string DisplayName,
    int SchemaVersion,
    string? TestlabFeatureHint = null,
    TimeSpan? DefaultTimeoutOverride = null,
    bool SupportsProgress = true);
