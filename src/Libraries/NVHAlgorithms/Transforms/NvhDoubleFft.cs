using System.Numerics;

namespace NVHAlgorithms.Transforms;

/// <summary>
/// 基 2 Cooley–Tukey，长度须为 2 的幂。符号：前向 exp(-2π i k n / N)，逆变换 exp(+2π i k n / N)。
/// 参照 FFTW 3.3.5 文档核对缩放与布局；本实现纯托管、无本机依赖。
/// </summary>
public static class NvhDoubleFft
{
    public static bool IsPowerOfTwo(int n) => n > 0 && (n & (n - 1)) == 0;

    public static void Forward(ReadOnlySpan<double> real, Span<Complex> spectrum, NvhFftOptions? options = null, CancellationToken cancellationToken = default)
    {
        if (real.Length != spectrum.Length)
            throw new ArgumentException("real 与 spectrum 长度须一致。");
        if (!IsPowerOfTwo(real.Length))
            throw new ArgumentException("FFT 长度须为 2 的幂。", nameof(real));

        var opt = options ?? new NvhFftOptions();
        for (int i = 0; i < spectrum.Length; i++)
            spectrum[i] = new Complex(real[i], 0);

        TransformInternal(spectrum, inverse: false, opt, cancellationToken);

        if (opt.Normalization == NvhFftNormalization.SymmetricSqrtN)
        {
            double s = 1.0 / Math.Sqrt(spectrum.Length);
            for (int i = 0; i < spectrum.Length; i++)
                spectrum[i] *= s;
        }
    }

    public static void Forward(ReadOnlySpan<Complex> time, Span<Complex> frequency, NvhFftOptions? options = null, CancellationToken cancellationToken = default)
    {
        if (time.Length != frequency.Length)
            throw new ArgumentException("输入输出长度须一致。");
        if (!IsPowerOfTwo(time.Length))
            throw new ArgumentException("FFT 长度须为 2 的幂。");

        var opt = options ?? new NvhFftOptions();
        time.CopyTo(frequency);
        TransformInternal(frequency, inverse: false, opt, cancellationToken);

        if (opt.Normalization == NvhFftNormalization.SymmetricSqrtN)
        {
            double s = 1.0 / Math.Sqrt(frequency.Length);
            for (int i = 0; i < frequency.Length; i++)
                frequency[i] *= s;
        }
    }

    public static void Inverse(ReadOnlySpan<Complex> frequency, Span<Complex> time, NvhFftOptions? options = null, CancellationToken cancellationToken = default)
    {
        if (frequency.Length != time.Length)
            throw new ArgumentException("输入输出长度须一致。");
        if (!IsPowerOfTwo(frequency.Length))
            throw new ArgumentException("FFT 长度须为 2 的幂。");

        var opt = options ?? new NvhFftOptions();
        frequency.CopyTo(time);
        TransformInternal(time, inverse: true, opt, cancellationToken);

        double scale = opt.Normalization switch
        {
            NvhFftNormalization.InverseUnitary => 1.0 / time.Length,
            NvhFftNormalization.SymmetricSqrtN => 1.0 / Math.Sqrt(time.Length),
            _ => 1.0,
        };

        if (scale != 1.0)
        {
            for (int i = 0; i < time.Length; i++)
                time[i] *= scale;
        }
    }

    private static void TransformInternal(Span<Complex> data, bool inverse, NvhFftOptions opt, CancellationToken cancellationToken)
    {
        int n = data.Length;
        BitReversePermute(data, cancellationToken, opt.CancellationStrideStages);

        int stagesDone = 0;
        for (int s = 1; s <= Log2(n); s++)
        {
            int m = 1 << s;
            int m2 = m >> 1;
            Complex wlen = Complex.FromPolarCoordinates(1.0, (inverse ? 1 : -1) * 2 * Math.PI / m);

            for (int k = 0; k < n; k += m)
            {
                Complex w = Complex.One;
                for (int j = 0; j < m2; j++)
                {
                    Complex t = data[k + j + m2] * w;
                    Complex u = data[k + j];
                    data[k + j] = u + t;
                    data[k + j + m2] = u - t;
                    w *= wlen;
                }
            }

            stagesDone++;
            if (opt.CancellationStrideStages > 0 && stagesDone % opt.CancellationStrideStages == 0)
                cancellationToken.ThrowIfCancellationRequested();
        }
    }

    private static void BitReversePermute(Span<Complex> data, CancellationToken cancellationToken, int stride)
    {
        int n = data.Length;
        int bits = Log2(n);
        int j = 0;
        int stepCount = 0;
        for (int i = 0; i < n - 1; i++)
        {
            if (i < j)
                (data[i], data[j]) = (data[j], data[i]);

            int k = n >> 1;
            while (k <= j)
            {
                j -= k;
                k >>= 1;
            }

            j += k;
            stepCount++;
            if (stride > 0 && stepCount % (n / 8 + 1) == 0)
                cancellationToken.ThrowIfCancellationRequested();
        }
    }

    private static int Log2(int n)
    {
        int c = 0;
        int x = n;
        while (x > 1)
        {
            x >>= 1;
            c++;
        }

        return c;
    }
}
