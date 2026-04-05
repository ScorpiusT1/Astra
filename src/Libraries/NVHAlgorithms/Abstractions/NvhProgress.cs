namespace NVHAlgorithms.Abstractions;

/// <summary>算法进度（可映射到 UI；建议在实现侧节流）。Ratio 建议保持在 0..1。</summary>
public readonly record struct NvhProgress(double Ratio, string? Stage = null);
