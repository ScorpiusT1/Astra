using NVHAlgorithms.Axes;

namespace NVHAlgorithms.Algorithms.Internal;

internal static class NvhNumericGuard
{
    public static bool Apply(ReadOnlySpan<double> data, NvhNonFinitePolicy policy, out Exception? error)
    {
        error = null;
        for (int i = 0; i < data.Length; i++)
        {
            if (double.IsFinite(data[i]))
                continue;

            switch (policy)
            {
                case NvhNonFinitePolicy.Reject:
                    error = new InvalidOperationException($"索引 {i} 处为非有限值。");
                    return false;
                case NvhNonFinitePolicy.ReplaceWithZero:
                    break;
                case NvhNonFinitePolicy.ReplaceWithNaN:
                    break;
                case NvhNonFinitePolicy.SkipBlock:
                    error = new NotSupportedException("SkipBlock 需算法级支持。");
                    return false;
            }
        }

        return true;
    }

    public static void ReplaceNonFinite(Span<double> data)
    {
        for (int i = 0; i < data.Length; i++)
        {
            if (!double.IsFinite(data[i]))
                data[i] = 0;
        }
    }

    public static void ReplaceNonFiniteWithNaN(Span<double> data)
    {
        for (int i = 0; i < data.Length; i++)
        {
            if (!double.IsFinite(data[i]))
                data[i] = double.NaN;
        }
    }
}
