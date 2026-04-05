namespace NVHAlgorithms.Transforms;

/// <summary>与 FFTW/Testlab 对齐时须在算法文档中固定归一化语义。</summary>
public enum NvhFftNormalization
{
    /// <summary>正逆变换均不缩放（调用方自管）。</summary>
    None,

    /// <summary>逆变换乘以 1/N。</summary>
    InverseUnitary,

    /// <summary>正逆均乘以 1/sqrt(N)（酉归一）。</summary>
    SymmetricSqrtN,
}
