using System.Numerics;

namespace NVHAlgorithms.Transforms;

/// <summary>通过频域构造解析信号（Hilbert 对偶的实部为原信号）；长度须为 2 的幂。</summary>
public static class NvhAnalyticSignal
{
    public static void Compute(ReadOnlySpan<double> real, Span<Complex> analytic, NvhFftOptions? fftOptions = null, CancellationToken cancellationToken = default)
    {
        if (real.Length != analytic.Length)
            throw new ArgumentException("长度须一致。");
        if (!NvhDoubleFft.IsPowerOfTwo(real.Length))
            throw new ArgumentException("长度须为 2 的幂。");

        var opt = fftOptions ?? new NvhFftOptions();
        Span<Complex> spectrum = analytic.Length < 1024
            ? stackalloc Complex[analytic.Length]
            : new Complex[analytic.Length];

        NvhDoubleFft.Forward(real, spectrum, opt, cancellationToken);

        ApplyHilbertMultiplier(spectrum);

        NvhDoubleFft.Inverse(spectrum, analytic, opt, cancellationToken);
    }

    private static void ApplyHilbertMultiplier(Span<Complex> spectrum)
    {
        int n = spectrum.Length;
        spectrum[0] *= 1;
        if (n == 1)
            return;

        int half = n / 2;
        for (int k = 1; k < half; k++)
            spectrum[k] *= 2;

        if ((n & 1) == 0)
            spectrum[half] *= 1;

        for (int k = half + 1; k < n; k++)
            spectrum[k] = Complex.Zero;
    }
}
