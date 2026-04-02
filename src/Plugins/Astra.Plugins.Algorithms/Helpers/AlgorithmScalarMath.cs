using System;

namespace Astra.Plugins.Algorithms.Helpers
{
    /// <summary>从算法中间结果提取代表性标量（如峰值），供 <see cref="NodeScalarOutputContracts"/> 输出。</summary>
    internal static class AlgorithmScalarMath
    {
        public static double MaxAbs(double[]? data)
        {
            if (data == null || data.Length == 0)
                return double.NaN;
            var m = 0.0;
            foreach (var v in data)
            {
                var a = Math.Abs(v);
                if (a > m)
                    m = a;
            }
            return m;
        }

        public static double Max(double[]? data)
        {
            if (data == null || data.Length == 0)
                return double.NaN;
            var m = data[0];
            for (var i = 1; i < data.Length; i++)
            {
                if (data[i] > m)
                    m = data[i];
            }
            return m;
        }

        public static double Max(double[,]? z)
        {
            if (z == null || z.Length == 0)
                return double.NaN;
            var rows = z.GetLength(0);
            var cols = z.GetLength(1);
            var m = z[0, 0];
            for (var i = 0; i < rows; i++)
            {
                for (var j = 0; j < cols; j++)
                {
                    if (z[i, j] > m)
                        m = z[i, j];
                }
            }
            return m;
        }
    }
}
